"""
Does every part of the jib touch something? blender --background --python tools/jib/lash_check.py -- <hod_jib.obj>

For each connected part of the mesh, samples its surface densely and measures the distance from
those samples to the nearest surface of any OTHER part. A part that hugs or overlaps a timber has a
minimum of about 0 (the two surfaces cross); a hoop hovering off the wood has a gap. Reports every
part whose minimum is over 2 mm, which on a model made of overlapping parts is a floating piece or
a deliberate tie through air (the cord to the basket), for a human to judge.
"""
import collections
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fit_col as FC   # noqa: E402


def main():
    path = sys.argv[sys.argv.index('--') + 1]
    V, F, G = FC.load(path)
    comp = FC.components(V, F)
    parts = collections.defaultdict(list)
    for i, c in enumerate(comp): parts[c].append(i)
    A, B, C = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    bad = []
    rows = []
    for c, fs in parts.items():
        fs = np.array(fs)
        P, _ = FC.sample_surface(V, F[fs], per_m2=6000, seed=2)
        # candidate triangles of other parts: restrict to a box around the part, grown by 5 cm
        lo, hi = P.min(0) - 0.05, P.max(0) + 0.05
        mask = np.array([comp[i] != c for i in range(len(F))])
        tl = np.minimum(np.minimum(A, B), C).max(1) if False else None
        tmin = np.minimum(np.minimum(A, B), C); tmax = np.maximum(np.maximum(A, B), C)
        near = mask & np.all(tmax >= lo, axis=1) & np.all(tmin <= hi, axis=1)
        if not near.any():
            d = 9.0
        else:
            d = float(FC.tri_dist(P, A[near], B[near], C[near]).min())
        g = collections.Counter(G[i] for i in fs).most_common(1)[0][0]
        rows.append((d, g, len(fs), P.min(0), P.max(0)))
        if d > 0.002: bad.append(rows[-1])
    print('LASH parts', len(rows), 'over 2mm', len(bad))
    for d, g, n, lo, hi in sorted(bad, key=lambda r: -r[0]):
        print('LASH gap %.1f mm  %-6s tris %3d  lo %s hi %s' % (d * 1000, g, n, np.round(lo, 2), np.round(hi, 2)))
    ds = sorted(r[0] for r in rows)
    print('LASH worst', round(ds[-1] * 1000, 1), 'mm')


def bands(path):
    """
    The sector test: for every ring-like part (cord or iron, under 7 cm tall, over 30 cm across),
    split the ring into 24 sectors round its own axis and find, in each, the smallest distance from
    the band to the wood (0 when the band's surface is inside the timber). A band that hugs has most
    sectors at 0 and only the notches between timbers open; a hoop that floats has gaps all round.
    """
    import mathutils
    from mathutils.bvhtree import BVHTree
    V, F, G = FC.load(path)
    comp = FC.components(V, F)
    parts = collections.defaultdict(list)
    for i, c in enumerate(comp): parts[c].append(i)
    wood = [i for i in range(len(F)) if G[i] in ('wood', 'bark', 'wicker')]
    tree = BVHTree.FromPolygons([mathutils.Vector(v) for v in V], [tuple(F[i]) for i in wood])
    out = []
    for c, fs in parts.items():
        g = collections.Counter(G[i] for i in fs).most_common(1)[0][0]
        if g not in ('cord', 'iron'): continue
        P = V[np.unique(F[fs].ravel())]
        lo, hi = P.min(0), P.max(0)
        if hi[1] - lo[1] > 0.07 or max(hi[0] - lo[0], hi[2] - lo[2]) < 0.3: continue
        S, _ = FC.sample_surface(V, F[fs], per_m2=8000, seed=3)
        cx, cz = (lo[0] + hi[0]) / 2, (lo[2] + hi[2]) / 2
        sect = np.floor((np.arctan2(S[:, 2] - cz, S[:, 0] - cx) + np.pi) / (2 * np.pi) * 24).astype(int) % 24
        gaps = np.full(24, 9.0)
        for k in range(24):
            ds = []
            for q in S[sect == k][::3]:
                v = mathutils.Vector(q)
                hit = tree.find_nearest(v)
                if hit[0] is None: continue
                up = tree.ray_cast(v, mathutils.Vector((0, 1, 0)))[0] is not None
                dn = tree.ray_cast(v, mathutils.Vector((0, -1, 0)))[0] is not None
                ds.append(0.0 if (up and dn) else hit[3])
            # The 25th percentile, not the minimum: a ring that crosses a timber's corner touches it
            # there however far it floats off the faces. The inner wall is a quarter or more of a
            # band's surface, so this reads how far the INNER wall is from the wood.
            if ds: gaps[k] = float(np.percentile(ds, 25))
        real = gaps[gaps < 9]
        out.append((float((lo[1] + hi[1]) / 2), g, float(hi[0] - lo[0]), float(hi[2] - lo[2]),
                    int((gaps <= 0.0035).sum()), float(real.max() if len(real) else 9)))
    for y, g, w, d, touch, worst in sorted(out):
        print('BAND y=%.2f %-5s %.2f x %.2f  sectors hugging %2d/24  worst gap %.0f mm' % (y, g, w, d, touch, worst * 1000))
    print('BANDS', len(out))


main()
bands(sys.argv[sys.argv.index('--') + 1])
