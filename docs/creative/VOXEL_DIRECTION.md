# ART-03 voxel direction v1

Status: docs-only handoff. User selected voxel graphics and backbone first; the monochrome primitive prototype is accepted following playtest. Authority: Chief assignment ART-03, reviewed main baseline 4be6b78 and Chief architecture/Decision 002 at 4ffb0af. This document supersedes the field-rendering assumptions in [ONE_TRACTOR_DIRECTION](ONE_TRACTOR_DIRECTION.md) and [ART-02 handoff](IMPLEMENTATION_HANDOFF.md). Earlier SVGs remain historical original reference sources, not the target farm renderer. Existing UI/accessibility and audio-control principles remain relevant. No new renderer, production asset, music, dependency or shared contract is delivered.

## Original block farm

Build a practical miniature farm from rectangular solids with flat faces: a low tractor hood, raised cab, square wheel blocks, wheat stalks with stepped seed heads, shallow soil slabs and a compact unloading pad. Use geometry to communicate state before decoration. Voxel here describes the block-shaped appearance; it does not require a volumetric terrain engine, physics simulation or destructible world. Avoid recognizable borrowed vehicle designs, copied game layouts and unnecessary modeling tools.

The first renderer remains monochrome. Proposed opaque grayscale sRGB roles: ground #404040, wheel/outline #202020, growing crop #909090, tractor hood #C0C0C0, cab/ripe head #E8E8E8, depot pad #707070. These are illustration roles, not guaranteed text contrast pairs. Preserve dark/light UI text contrast from the existing accessible view. Growing/ripe differ in geometry and labels as well as value. The earlier warm palette may be evaluated in a later art milestone; it is not a requirement to replace the accepted monochrome prototype now.

## Units, anchors and headings

The authoritative grid remains integer Domain coordinates: x east, y north. Ground plane is Unity X/Z; Unity Y is vertical. One grid cell = one Unity unit. Tile (x,y) occupies [x,x+1] × [y,y+1] on X/Z. Cell center is (x+0.5, 0, y+0.5); terrain top is Y=0. This supersedes ART-02's XY-plane mapping.

All entity roots use that center at ground height, identity scale (1,1,1), no negative scaling. Local +Z is tractor front, +X its right, +Y up. Unity yaw about +Y: North 0°, East 90°, South 180°, West 270°. Rotation changes art only; heading/position remain snapshot values. Geometry quantum is 1/16 unit (0.0625); cell-sized slabs are allowed. Root pivots are centered at the ground plane, not at mesh bounds center. Keep files and scene instances free from axis conversion surprises.

## Geometry recipes for the later renderer

Dimensions and center positions below are local (X,Y,Z), in Unity units. These are proposed original cuboid recipes, not new occupancy rules. All visible meshes stay within one tile footprint. No colliders/Rigidbody are needed for domain movement. Picking maps to the integer grid or a presentation proxy; it never validates gameplay.

| Element | Size | Center / interpretation |
| --- | --- | --- |
| Terrain slab | (1,0.125,1) | (0,-0.0625,0); top exactly ground |
| Tractor hood | (0.375,0.25,0.375) | (0,0.25,0.1875); front +Z |
| Tractor cab | (0.375,0.375,0.3125) | (0,0.4375,-0.1875); higher rear silhouette |
| Tractor chassis | (0.5,0.125,0.75) | (0,0.125,0) |
| Front wheels, pair | (0.125,0.25,0.1875) | (±0.3125,0.125,0.25) |
| Rear wheels, pair | (0.1875,0.375,0.25) | (±0.34375,0.1875,-0.25); half-quantum centers keep faces on quantum |
| Front heading bar | (0.25,0.0625,0.0625) | (0,0.40625,0.34375); high-value front marker |
| Growing wheat | Stem (0.0625,0.1875,0.0625) | Base at ground, two low side leaf blocks; total height ≤0.25 |
| Ripe wheat | Stem (0.0625,0.3125,0.0625), head (0.1875,0.125,0.125) | Head centered at Y=0.375; total height 0.4375; stepped silhouette |
| Depot pad | (0.875,0.0625,0.875) | (0,0.03125,0); bounded cell marker |
| Depot corner posts | (0.0625,0.1875,0.0625) | At four pad corners, outside central tractor silhouette |

Use at most three wheat stalks per tile at local X/Z offsets (-0.25,-0.125), (0,0.125), (0.25,-0.125). No crop = terrain alone. Show blocked cells with raised corner blocks plus a “Blocked” label; these are visual markers rather than collision geometry. Depot access must stay apparent when a tractor occupies its cell: pad perimeter plus a screen-space “Depot” label. A tall barn obscures the accessible cell and is deferred; any later decorative depot building must respect domain occupancy instead of implying additional walkable or blocked tiles. Harvester remains an inspector label until a coordinated art-only attachment is introduced; do not imply a multi-cell footprint.

## Orthographic camera and readable UI

Default view faces north from the south side of the map. Camera target C=(width/2,0,height/2); position C+(0,10,-7), up vector +Y, orthographic projection, no roll. Look at C; this gives approximately 55° downward pitch, screen-right east, far side north. Initial near/far planes 0.1/100 with the camera pulled back as needed for the bounded map, preserving the viewing angle. Camera distance affects clipping, not orthographic apparent scale.

Fit the farm viewport by projecting the four map corners plus maximum geometry height into camera right/up coordinates. Let projected half-extents be hx,hy and viewport aspect a=widthPixels/heightPixels. Set orthographic size to max(hy,hx/a)*1.1, recenter on projected bounds. Near/far and distance must cover those bounds for up to 64×64 cells; do not assume the initial distance fits every map. Fit to the actual farm panel, not the entire application window. UI/editor remain screen-space and do not inherit world zoom. Allow zoom, pan and “Center tractor”; defer camera rotation to keep headings understandable. A north arrow and selected heading/position text remain visible.

At small zoom, simplify crop clusters rather than losing state labels or heading cues. Selection uses a contrasting perimeter bracket just above the tile and a screen-space name; no color-only identification. Crop/depot silhouettes must remain distinguishable in grayscale. Keep shading restrained, with no bloom, camera shake or flashing. First renderer may use flat unlit materials under the current project render pipeline; later lighting needs Chief/Experience approval and cannot hide crop readiness in shadow. Reduced motion uses immediate snapshot placement and static action status. No new pipeline/plugin is requested.

## Immutable snapshot mapping and staged delivery

1. Backbone milestone now: retain accepted primitive renderer. Complete session/storage/recovery/reference work under Decision 002. This direction is documentation only; no voxel renderer replaces the playable view in ART-03.
2. Later delegated renderer spike: Experience builds a presentation-local typed visual mapping from approved immutable world/session snapshots. Current SessionViewState exposes CellView.Label and string TractorView.Heading; do not parse combined “Ripe\nDepot” display text or inspector strings as gameplay state. Experience proposes the narrow typed mapping to Chief before changing a shared presentation boundary. Domain need not change for visual data already present.
3. Render one tile/tractor/crop/depot using primitive cuboids, grayscale materials and the camera above. Stable actor IDs identify views; tile coordinates identify terrain/crops; depot IDs identify depot views. Explicitly map Heading enum; do not guess string casing. Reconcile only accepted published snapshots, retain no mutable domain references, and rebuild transient visuals on load/mode change.
4. Verify real movement, turn, harvest, unload/refuel and Pause/Step/Stop through existing runtime/application/domain. Optional later interpolation uses accepted snapshots/pending-action timing for cosmetic movement only. Domain retains origin occupancy while movement is pending. Pause freezes visual progress; Step presents exactly its published tick; Stop/load settles to authoritative retained/restored state. Unknown states get a visible neutral marker and diagnostic rather than silent fabricated graphics.
5. After exact candidate review and visual playtest, delegate mesh optimization/art refinement and separately audio binding. Produce no production art/music before that handoff.

Existing Domain WorldSnapshot provides Tiles, Tractors, Depots, PendingActions and Rules. TileState supplies Crop, Growth, Blocked, Tilled and Moisture; current wheat readiness uses the authoritative Rules.WheatMaturity comparison as already mapped by ApplicationSessionView. Reuse one owner-approved mapping, not a second growth rule. Do not introduce new crop-cycle states from art. TractorState position/heading/implement and actual depot position remain authoritative. Voxel coordinates, shading, mesh IDs and camera state do not enter stopped world saves; reconstruct them from content/world state. Optional presentation preferences remain outside gameplay progress.

## Eventual asset/material/metadata conventions

After Chief authorizes asset work, Creative owns game/Assets/Harvest/Art/Voxel, materials and associated metadata; Experience owns renderer scripts/scenes and scene-instance wiring. Proposed names: tractor_block_v1, wheat_growing_block_v1, wheat_ripe_block_v1, soil_slab_v1, depot_pad_v1. Prefer code-native cuboid meshes/prefabs for the first renderer; no asset importer dependency is necessary. Shared meshes/materials are reused, not allocated per frame or per voxel. Combine static crop geometry and assess batching only after profiling; bound scene growth to map/entity limits.

Mesh convention: one unit per cell, ground-center pivot, +Z North, applied unit transforms, explicit flat face normals, no smoothing/bevel expectation, no embedded animation/physics, no required texture/UV dependency. For eventual external mesh import, Chief approves format/tool and Experience verifies the axis/unit roundtrip; keep Read/Write off unless a measured need exists, reject automatic collision generation, and document any import scale rather than silently fixing scene transforms. No external models or import settings are delivered now.

Materials: opaque shared grayscale swatches, no per-frame material cloning, no essential transparency. Use current pipeline-compatible approved shader; no shader package is assumed. Creative records asset path, semantic role/version, dimensions/anchor/heading, creator, creation method, source/license/redistribution rights, attribution, checksum and approval status. Original geometry has no third-party asset provenance; project licensing remains Chief-owned. Unity-generated .meta GUIDs accompany assets when actually authored/imported and remain stable on rename. Never fabricate GUIDs or commit caches/build output. Former SVG manifest remains unchanged as historical evidence.

## Handoff checks and remaining risks

Docs checks: validate local links, supersession banners, right-angle heading/cell mapping and owned-path-only changes; no build is required for docs-only delivery. These do not prove camera readability or rendering acceptance.

Later renderer checks: known corner cells and four headings align; camera keeps north/east readable; tractor stays in its tile footprint; empty/growing/ripe/depot/blocked states remain identifiable in monochrome at supported zoom; depot label survives occupancy; reduced motion and text scale work; load/new mode clears old views; Pause/Step/Stop follow actual snapshots. Compare simulation state hashes before/after renderer addition to catch accidental coupling. Profile the bounded largest map before choosing mesh/instance budgets. Risks remain crop occlusion, camera fit at large maps, typed visual boundary approval and actual Unity/shader verification. Chief reviews/integrates this document and delegates subsequent implementation; no shared contract changes are requested by ART-03.
