# Basket round sweep — level 1 (2026-09-19)

Tool: `ai_basket_sweep` (Play Mode), 3 hoops × 6 powers × 3 lateral offsets = 54 shots, 8 balls each. In-band = power ≤ 1.05 (over-power shots are misses by design).

| | min | p25 | median | p75 | max |
|---|---|---|---|---|---|
| Unity rebuild (n=45) | 12 | 17 | 19 | 21 | 28 |
| Original Pota (arastirma/tarama-40-2026-09-18.txt) | 12 | 17 | 21 | 30 | 32 |

Per shot: basket (top-row passes out of 8), offsets −0.15 / 0 / +0.15:

| x | p0.30 | p0.50 | p0.60 | p0.80 | p1.00 | p1.15 (over) |
|---|---|---|---|---|---|---|
| -1.4 | 20 (7) / 20 (8) / 17 (8) | 16 (6) / 20 (8) / 20 (8) | 12 (4) / 21 (8) / 17 (8) | 14 (3) / 20 (8) / 19 (8) | 14 (3) / 17 (7) / 18 (8) | 8 (0) / 8 (0) / 10 (0) |
| +0.0 | 23 (8) / 26 (8) / 23 (8) | 19 (6) / 28 (8) / 18 (6) | 25 (8) / 26 (8) / 15 (5) | 26 (9) / 25 (8) / 20 (7) | 18 (4) / 22 (8) / 14 (5) | 9 (0) / 11 (0) / 8 (0) |
| +1.4 | 19 (8) / 18 (8) / 15 (4) | 23 (8) / 19 (8) / 15 (6) | 16 (8) / 26 (8) / 19 (3) | 20 (8) / 20 (8) / 12 (4) | 18 (8) / 18 (6) / 16 (6) | 11 (0) / 8 (0) / 8 (0) |

Settings that produced this: SlabEntryZ 0 (hand-off at the hoop plane), EntryForwardDamping 0.1, ArrivalLift 0, BackboardHitAbove 0.45 / lateral damping 0.3, OverPowerAbove 1.0 / side kick 1.5, DropGravity 5.0, friendly rim on, pass window R − r/2 evaluated while inside the pass volume after entering from above.

Known difference: the upper tail (best shots) is lower than the original (max 28 vs 32, p75 21 vs 30). Candidates for tuning: lower-row pass leniency, CloneScatter, DropGravity.
