"""Fits the jib's collision boxes to hod_jib.obj by connected part (LHM-77 follow-up).

Run:  blender -b --python tools/jib/fit_col.py -- [--write]
Reads assets/hod_jib.obj, builds about 15 oriented boxes, prints the quality numbers, and with
--write replaces assets/hod_jib.col. Blender is only the Python that ships numpy.

Components are picked by sorting the mesh's connected parts by height; every pick asserts the group
and the triangle count, so a changed mesh fails loudly instead of boxing the wrong part.

Why this exists: the first .col was six axis-aligned boxes measured off vertex height bands. The
mast leans, so its box bloated into invisible walls, and an earlier version of the same approach
left a box hanging in the air.
"""
import sys, os, math, collections, json
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
OBJ = os.path.join(ROOT, 'assets', 'hod_jib.obj')
COL = os.path.join(ROOT, 'assets', 'hod_jib.col')
SCRATCH = os.path.join(ROOT, 'scratch-build', 'col')
CELL = 0.05


def load(p):
    V = []; F = []; G = []; m = None
    for l in open(p):
        if l.startswith('v '): V.append([float(x) for x in l.split()[1:4]])
        elif l.startswith('usemtl'): m = l.split()[1]
        elif l.startswith('f '):
            F.append([int(t.split('/')[0]) - 1 for t in l.split()[1:]]); G.append(m)
    return np.array(V), np.array(F), G


def components(V, F):
    key = {}; w = np.zeros(len(V), int)
    for i, v in enumerate(V):
        w[i] = key.setdefault(tuple(np.round(v, 4)), len(key))
    par = list(range(len(key)))
    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]; a = par[a]
        return a
    for f in F:
        for j in range(1, len(f)):
            a, b = find(w[f[0]]), find(w[f[j]])
            if a != b: par[a] = b
    return [find(w[f[0]]) for f in F]


def build_parts(V, F, G):
    comp = components(V, F)
    groups = collections.defaultdict(list)
    for i, c in enumerate(comp): groups[c].append(i)
    rows = []
    for c, fs in groups.items():
        idx = np.unique(F[fs].ravel()); P = V[idx]
        g = collections.Counter(G[i] for i in fs).most_common(1)[0][0]
        cen = P.mean(0); _, _, vt = np.linalg.svd(P - cen, full_matrices=False)
        ax = vt[0] if vt[0][1] >= 0 else -vt[0]
        rows.append(dict(cy=cen[1], g=g, n=len(fs), P=P, axis=ax))
    rows.sort(key=lambda r: r['cy'])
    return rows


def frame(axis):
    e1 = axis / np.linalg.norm(axis)
    order = np.argsort(np.abs(e1))
    w = np.zeros(3); w[order[0]] = 1.0
    e2 = w - e1 * np.dot(w, e1); e2 /= np.linalg.norm(e2)
    e3 = np.cross(e1, e2)
    return np.stack([e1, e2, e3], axis=1)


def quat(R):
    t = R[0, 0] + R[1, 1] + R[2, 2]
    if t > 0:
        s = math.sqrt(t + 1) * 2; w = 0.25 * s
        x = (R[2, 1] - R[1, 2]) / s; y = (R[0, 2] - R[2, 0]) / s; z = (R[1, 0] - R[0, 1]) / s
    elif R[0, 0] > R[1, 1] and R[0, 0] > R[2, 2]:
        s = math.sqrt(1 + R[0, 0] - R[1, 1] - R[2, 2]) * 2
        w = (R[2, 1] - R[1, 2]) / s; x = 0.25 * s; y = (R[0, 1] + R[1, 0]) / s; z = (R[0, 2] + R[2, 0]) / s
    elif R[1, 1] > R[2, 2]:
        s = math.sqrt(1 + R[1, 1] - R[0, 0] - R[2, 2]) * 2
        w = (R[0, 2] - R[2, 0]) / s; x = (R[0, 1] + R[1, 0]) / s; y = 0.25 * s; z = (R[1, 2] + R[2, 1]) / s
    else:
        s = math.sqrt(1 + R[2, 2] - R[0, 0] - R[1, 1]) * 2
        w = (R[1, 0] - R[0, 1]) / s; x = (R[0, 2] + R[2, 0]) / s; y = (R[1, 2] + R[2, 1]) / s; z = 0.25 * s
    q = np.array([x, y, z, w]); q /= np.linalg.norm(q)
    if q[3] < 0: q = -q
    return q


def obb(P, R):
    L = P @ R
    lo = L.min(0); hi = L.max(0)
    return R @ ((lo + hi) / 2), hi - lo


def yaw(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def lift_to_ground(b, ymin=0.0):
    """A tilted box's bottom face is square to its long axis, so on a leg cut flat on the ground its
    lowest corner dips below it. Shorten the box from that end until no corner is below the ground."""
    h = b['s'] / 2
    low = b['c'][1] - np.abs(b['R'][1]) @ h
    if low >= ymin - 1e-4:
        return
    k = int(np.argmax(np.abs(b['R'][1])))
    dip = ymin - low
    shrink = dip / abs(b['R'][1][k])
    sign = 1.0 if b['R'][1][k] > 0 else -1.0     # the end that lies low is -sign along axis k
    b['s'][k] -= shrink
    b['c'] = b['c'] + b['R'][:, k] * sign * shrink / 2


def fit():
    V, F, G = load(OBJ)
    rows = build_parts(V, F, G)

    def sel(g, n, pred):
        found = [i for i, r in enumerate(rows) if r['g'] == g and r['n'] == n and pred(r['P'])]
        return found

    def one(g, n, pred, what):
        found = sel(g, n, pred)
        assert len(found) == 1, (what, found)
        return found[0]

    def union(ids): return np.vstack([rows[i]['P'] for i in ids])

    boxes = []

    def add(name, P, axis=None):
        R = frame(axis) if axis is not None else np.eye(3)
        c, s = obb(P, R)
        boxes.append(dict(name=name, c=c, s=s, R=R))

    def timber(name, ids):
        for i in ids: add(name, rows[i]['P'], rows[i]['axis'])

    # Parts are found by what they are (group, triangle count, where they stand), not by their rank
    # in a height sort, so a changed number of lashings does not shift every pick onto the wrong part.
    timber('mast timber', sel('wood', 36, lambda P: P[:, 1].max() > 2.5))
    assert len(sel('wood', 36, lambda P: P[:, 1].max() > 2.5)) == 2
    left = sel('wood', 36, lambda P: P[:, 1].max() < 1.2 and P[:, 0].mean() < 0)
    right = sel('wood', 36, lambda P: P[:, 1].max() < 1.2 and P[:, 0].mean() > 0)
    assert len(left) == 1 and len(right) == 1
    timber('buttress left', left); timber('buttress right', right)
    braces = sel('wood', 44, lambda P: 1.1 < P[:, 1].min() < 1.2 and P[:, 1].max() < 1.7 and abs(P[:, 0].mean()) > 0.15
                 and P[:, 0].max() - P[:, 0].min() > 0.55)
    assert len(braces) == 2, braces
    timber('deck brace', braces)
    planks = sel('wood', 44, lambda P: 1.55 < P[:, 1].min() and P[:, 1].max() < 1.7 and P[:, 0].max() - P[:, 0].min() > 1.0)
    assert len(planks) == 5, planks
    add('deck', union(planks))
    boom = one('wood', 44, lambda P: P[:, 0].min() < -1.1 and P[:, 1].min() > 2.2 and P[:, 0].max() - P[:, 0].min() > 1.0, 'boom')
    timber('hoist boom', [boom])
    brace = one('wood', 44, lambda P: P[:, 0].min() < -0.8 and 1.9 < P[:, 1].min() < 2.0, 'boom brace')
    timber('boom brace', [brace])
    bk = [one('wicker', 72, lambda P: P[:, 0].max() < -0.8, 'basket'),
          one('wicker', 32, lambda P: P[:, 0].max() < -0.8, 'basket floor'),
          one('cord', 72, lambda P: P[:, 0].max() < -0.8 and P[:, 1].max() < 1.4, 'basket rim')]
    add('basket', union(bk))

    cage_ids = [i for i, r in enumerate(rows)
                if r['g'] in ('wicker', 'cord', 'core') and r['P'][:, 1].min() >= 2.34
                and r['P'][:, 1].max() <= 3.01 and -0.6 < r['P'][:, 0].min() and r['P'][:, 0].max() < 0.5]
    assert len(cage_ids) >= 25, cage_ids
    cage = union(cage_ids)
    cx = (cage[:, 0].min() + cage[:, 0].max()) / 2; cz = (cage[:, 2].min() + cage[:, 2].max()) / 2
    ylo, yhi = cage[:, 1].min(), cage[:, 1].max()
    # The cage is a barrel: wide at the belly, closing to the mouth. Two octagon tiers (each an
    # axis-aligned square plus the same square turned 45 degrees, half side 0.8 r so the star's points
    # and notches sit equally far either side of the circle) follow it; one tier would leave a 30 cm
    # shoulder of empty box round the mouth.
    ymid = ylo + (yhi - ylo) * 0.62
    for lo_y, hi_y, tier in ((ylo, ymid, 'lower'), (ymid, yhi, 'upper')):
        sel = cage[(cage[:, 1] >= lo_y - 1e-6) & (cage[:, 1] <= hi_y + 1e-6)]
        r = max(sel[:, 0].max() - cx, cx - sel[:, 0].min(), sel[:, 2].max() - cz, cz - sel[:, 2].min())
        apo = r * 0.80
        for rot in (np.eye(3), yaw(45)):
            boxes.append(dict(name='cage ' + tier, c=np.array([cx, (lo_y + hi_y) / 2, cz]),
                              s=np.array([2 * apo, hi_y - lo_y, 2 * apo]), R=rot))

    tines = [i for i in range(len(rows)) if rows[i]['g'] == 'bark' and rows[i]['P'][:, 1].min() > 2.85]
    crown = union(tines)
    ccx = (crown[:, 0].min() + crown[:, 0].max()) / 2; ccz = (crown[:, 2].min() + crown[:, 2].max()) / 2
    cr = max(crown[:, 0].max() - ccx, ccx - crown[:, 0].min(), crown[:, 2].max() - ccz, ccz - crown[:, 2].min()) * 0.8
    top = crown[:, 1].max()
    boxes.append(dict(name='prong crown', c=np.array([ccx, (yhi + top) / 2, ccz]),
                      s=np.array([2 * cr, top - yhi, 2 * cr]), R=np.eye(3)))
    for b in boxes:
        lift_to_ground(b)
    return V, F, G, rows, boxes


def corners(b):
    h = b['s'] / 2
    return np.array([b['c'] + b['R'] @ np.array([sx * h[0], sy * h[1], sz * h[2]])
                     for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)])


def in_box(pts, b, tol=0.0):
    L = (pts - b['c']) @ b['R']
    return np.all(np.abs(L) <= b['s'] / 2 + tol, axis=1)


def in_any(pts, boxes, tol=0.0):
    m = np.zeros(len(pts), bool)
    for b in boxes: m |= in_box(pts, b, tol)
    return m


def dist_to_box(pts, b):
    L = (pts - b['c']) @ b['R']
    return np.linalg.norm(np.maximum(np.abs(L) - b['s'] / 2, 0), axis=1)


def tri_dist(pts, A, B, C):
    out = np.full(len(pts), 1e9)
    AB = B - A; AC = C - A
    n = np.cross(AB, AC); nn = np.linalg.norm(n, axis=1); ok = nn > 1e-12
    n = n / np.where(ok, nn, 1)[:, None]
    d00 = np.einsum('ti,ti->t', AC, AC)[None]; d01 = np.einsum('ti,ti->t', AC, AB)[None]
    d11 = np.einsum('ti,ti->t', AB, AB)[None]
    den = d00 * d11 - d01 * d01; den = np.where(np.abs(den) < 1e-12, 1, den)

    def seg(P, S, E):
        d = E - S
        t = np.clip(np.einsum('pti,ti->pt', P[:, None, :] - S[None], d) /
                    np.maximum(np.einsum('ti,ti->t', d, d), 1e-12)[None], 0, 1)
        q = S[None] + t[..., None] * d[None]
        return np.linalg.norm(P[:, None, :] - q, axis=2)

    for i in range(0, len(pts), 120):
        P = pts[i:i + 120]
        d = np.minimum(np.minimum(seg(P, A, B), seg(P, B, C)), seg(P, C, A))
        h = np.einsum('pti,ti->pt', P[:, None, :] - A[None], n)
        proj = P[:, None, :] - h[..., None] * n[None]
        v2 = proj - A[None]
        d20 = np.einsum('pti,ti->pt', v2, AC); d21 = np.einsum('pti,ti->pt', v2, AB)
        u = (d11 * d20 - d01 * d21) / den; v = (d00 * d21 - d01 * d20) / den
        inside = (u >= 0) & (v >= 0) & (u + v <= 1) & ok[None]
        d = np.where(inside, np.minimum(d, np.abs(h)), d)
        out[i:i + 120] = d.min(1)
    return out


def sample_surface(V, F, per_m2=3000, seed=1):
    rng = np.random.default_rng(seed)
    A, B, C = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    area = 0.5 * np.linalg.norm(np.cross(B - A, C - A), axis=1)
    n = np.maximum(1, np.round(area * per_m2).astype(int))
    tid = np.repeat(np.arange(len(F)), n)
    r1 = np.sqrt(rng.random(len(tid))); r2 = rng.random(len(tid))
    P = (1 - r1)[:, None] * A[tid] + (r1 * (1 - r2))[:, None] * B[tid] + (r1 * r2)[:, None] * C[tid]
    return P, tid


def box_surface_points(b, step=0.07):
    pts = []
    h = b['s'] / 2
    for ax in range(3):
        o = [i for i in range(3) if i != ax]
        for sgn in (-1, 1):
            for a in np.arange(-h[o[0]], h[o[0]] + 1e-9, step):
                for c in np.arange(-h[o[1]], h[o[1]] + 1e-9, step):
                    p = np.zeros(3); p[ax] = sgn * h[ax]; p[o[0]] = a; p[o[1]] = c
                    pts.append(b['c'] + b['R'] @ p)
    return np.array(pts)


def mesh_footprint(V, F):
    P, _ = sample_surface(V, F, per_m2=40000)
    return set(map(tuple, np.floor(P[:, [0, 2]] / CELL).astype(int)))


def hull(pts):
    pts = sorted(map(tuple, pts))
    def cross(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo = []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    up = []
    for p in reversed(pts):
        while len(up) >= 2 and cross(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    return lo[:-1] + up[:-1]


def box_footprint(boxes):
    cells = set()
    for b in boxes:
        poly = hull(corners(b)[:, [0, 2]])
        xs = [p[0] for p in poly]; zs = [p[1] for p in poly]
        for i in range(int(math.floor(min(xs) / CELL)), int(math.floor(max(xs) / CELL)) + 1):
            for j in range(int(math.floor(min(zs) / CELL)), int(math.floor(max(zs) / CELL)) + 1):
                px, pz = (i + .5) * CELL, (j + .5) * CELL
                if all((poly[(k + 1) % len(poly)][0] - poly[k][0]) * (pz - poly[k][1]) -
                       (poly[(k + 1) % len(poly)][1] - poly[k][1]) * (px - poly[k][0]) >= -1e-9
                       for k in range(len(poly))):
                    cells.add((i, j))
    return cells


def report(V, F, G, boxes):
    A, B, C = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    out = {}
    P, tid = sample_surface(V, F)
    inside = in_any(P, boxes, tol=0.005)
    out['surface_covered'] = float(inside.mean())
    comp = components(V, F)
    vol = collections.defaultdict(float); cov = collections.defaultdict(list)
    for t in range(len(F)):
        vol[comp[t]] += float(np.dot(A[t], np.cross(B[t], C[t])) / 6.0)
    for k, c in enumerate(tid): cov[comp[c]].append(inside[k])
    tot = sum(abs(v) for v in vol.values())
    out['volume_covered'] = sum(abs(vol[c]) * np.mean(cov[c]) for c in vol) / tot
    out['mesh_volume_m3'] = tot
    per = []
    for b in boxes:
        d = tri_dist(box_surface_points(b), A, B, C)
        per.append((b['name'], round(float(d.max()), 3), round(float(np.percentile(d, 95)), 3), round(float(d.mean()), 3)))
    out['box_to_mesh_max_p95_mean'] = per
    used = np.unique(F.ravel())
    dd = np.min([dist_to_box(V[used], b) for b in boxes], axis=0)
    out['vertex_outside_max_m'] = float(dd.max())
    out['vertex_outside_over_5cm_frac'] = float((dd > 0.05).mean())
    mf = mesh_footprint(V, F); bf = box_footprint(boxes)
    out['mesh_footprint_m2'] = len(mf) * CELL * CELL
    out['box_footprint_m2'] = len(bf) * CELL * CELL
    out['footprint_ratio'] = len(bf) / len(mf)
    out['mesh_bounds'] = [V[used].min(0).tolist(), V[used].max(0).tolist()]
    cs = np.vstack([corners(b) for b in boxes])
    out['union_min'] = cs.min(0).tolist(); out['union_max'] = cs.max(0).tolist()
    unc = collections.Counter(); uv = 0.0
    for c in vol:
        if np.mean(cov[c]) < 0.5:
            idx = [i for i in range(len(F)) if comp[i] == c]
            unc[G[idx[0]]] += 1; uv += abs(vol[c])
    out['uncovered_parts'] = dict(unc); out['uncovered_volume_m3'] = uv
    return out


def col_text(boxes, out):
    L = ['# box  centre x y z  size x y z  qx qy qz qw   (Unity space, as the mesh)',
         '# Fitted by tools/jib/fit_col.py: one oriented box per physical part of hod_jib.obj, not',
         '# axis-aligned height bands. A rotated box becomes its own child object in the loader.',
         '# Cords, cleats, rails and prong tines carry no box on purpose: they are thinner than a hand or',
         '# out of reach, and a box round them is air a player bumps into. The basket hangs at 0.98 m,',
         '# below head height, so it is solid; the cords it hangs on are not.',
         'layer root',
         'meta footprint %.3f' % out['mesh_footprint_m2'],
         'meta meshtop %.3f' % out['mesh_bounds'][1][1]]
    for b in boxes:
        q = quat(b['R'])
        L.append('# ' + b['name'])
        L.append('box %.3f %.3f %.3f %.3f %.3f %.3f %.5f %.5f %.5f %.5f' % (*b['c'], *b['s'], *q))
    return '\n'.join(L) + '\n'


if __name__ == '__main__':
    V, F, G, rows, boxes = fit()
    out = report(V, F, G, boxes)
    print(json.dumps(out, indent=1, default=str))
    for b in boxes: print('%-14s c %s s %s' % (b['name'], np.round(b['c'], 3), np.round(b['s'], 3)))
    os.makedirs(SCRATCH, exist_ok=True)
    json.dump(dict(boxes=[dict(name=b['name'], c=b['c'].tolist(), s=b['s'].tolist(), q=quat(b['R']).tolist(),
                               R=b['R'].tolist()) for b in boxes], report=out),
              open(os.path.join(SCRATCH, 'boxes.json'), 'w'), default=str)
    if '--write' in sys.argv:
        open(COL, 'w', newline='\n').write(col_text(boxes, out)); print('wrote', COL)
