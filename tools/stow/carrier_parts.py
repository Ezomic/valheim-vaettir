"""Retired. The carrier's meshes are Vaettir's, and are copied rather than built here.

This script used to build stow_carrier_heart/hoop/mote.obj, and the comment at the top
of it argued that Stow keeping its own copies was worth "two places to change if the
spirit is ever restyled".

That was the wrong price. The carrier is not a spirit like Vaettir's, it is the same one
- raised at the sapling, folded into the heartwood the post is built around, coming back
out to carry a crate. Two scripts meant two of it, and they had already drifted before
anybody restyled anything:

    Stow          one ring of seven beads, orbit 0.21, its own jitter seed
    Vaettir       two crossed rings of six, orbit 0.34, a different seed

Same character, visibly two different creatures depending on which mod drew it. Seeded
jitter guarantees that outcome - two runs of even identical source produce different
vertices unless they share a seed, which is exactly why the seeds are pinned at all.

So there is one source now:

    vaettir/tools/spirit_core.py

Rebuild there, then copy grove_spirit_{heart,hoop,mote}.{obj,mtl} into stow/assets/.
Copy them; do not regenerate them here. CarrierModel.cs loads those filenames directly
and Carrier.Orbit is the mesh's radius, so the two move together or the beads stop
sitting on their rings.
"""

import sys

print(__doc__, file=sys.stderr)
sys.exit(1)
