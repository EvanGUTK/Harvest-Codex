# Next milestone: player-facing backbone

The first programmable-tractor spike is delivered on reviewed main `4be6b78`. The user playtested it, accepted the primitive monochrome view and chose voxel graphics for the future presentation. Build dependable game systems before visual polish.

Decision 002 defines the next bounded milestone: connect existing stopped-save APIs to real Save/Load/recovery controls, and provide a programming reference filtered by actual campaign/sandbox capabilities. Preserve all working tractor controls and deterministic simulation. No new crop commands, campaign chapters, save schema or renderer dependencies yet.

Chief publishes/reviews the shared storage boundary, delegates isolated implementation and stays active through report updates, fixes, integration and exact review. Persistence builds the Composition storage facade; Experience wires stopped-load confirmation, explicit recovery and reference UI; Runtime supplies compiler-verified catalog content. Creative writes voxel conventions. Simulation proposes the following crop-cycle contract without changing Domain now.

Acceptance: fresh-session disk roundtrip, mode-separated saves, real save/stop/refund behavior, safe failed/active/future/corrupt loads, explicit revalidated backup recovery and stale offer rejection; accurate examples and reference refresh after unlock/mode change; relevant tests and Unity scene/player checks. Main advances only to the exact reviewed candidate. Do not launch another player just for progress updates; any development launch stays windowed.
