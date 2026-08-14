# Node cost curve: recovered values and estimates

Five of ten node costs were recovered. The rest are interpolated.

| Node | XP | Crystite | Resource | x prev (XP) |
|---|---|---|---|---|
| 1 | 4,000 | 100 | none | - |
| 2 | 8,000 | 200 | none | 2.00 |
| 3 | ~13,800 | ~300 | ~375 | 1.73 |
| 4 | ~23,700 | ~450 | ~560 | 1.72 |
| 5 | ~40,700 | ~670 | ~840 | 1.72 |
| 6 | 70,000 | 1,000 | 1,250 | 1.72 |
| 7 | 150,000 | 2,000 | 2,500 | 2.14 |
| 8 | 300,000 | 4,000 | 5,000 | 2.00 |
| 9 | ~600,000 | ~8,000 | ~10,000 | 2.00 |
| 10 | ~1,200,000 | ~16,000 | ~20,000 | 2.00 |

Tildes mark estimates, not recovered data.

## How the estimates were derived

Costs grow by a multiplier per node, not by a fixed increment, so the recovered points sit close to a straight line on a log axis. The multiplier isn't constant though: it's about 1.72x per node across the early gap and 2.0x per node at the top.

Nodes 3-5 bridge the known 8,000 to 70,000 gap at a constant 1.72x per step, which lands exactly on the real node 6. Nodes 9-10 continue the 2.0x step measured across nodes 6 to 8.

A single exponential fit over all five recovered points gives XP = 2,237 x 1.82^n (R2 of 0.997 in log space), but it overshoots node 6 by 17% and undershoots node 8 by 9%, so it's the weaker basis for filling blanks. Interpolating between real anchors keeps the known values exact.

## Structural notes worth checking against the data

- Crystite tracks XP but at a shifting rate. XP/Crystite is 40 at nodes 1-2 and 70-75 at nodes 6-8.
- Resource is exactly 1.25x Crystite at every node where both are known.
- Resource is listed as none at nodes 1-2, so the 3-5 figures assume the cost starts at node 3. It may not exist below node 6 at all.