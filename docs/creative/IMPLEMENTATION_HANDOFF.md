# ART-02 implementation handoff v1

> Historical 2D/SVG proposal: ART-03 [VOXEL_DIRECTION.md](VOXEL_DIRECTION.md) supersedes the farm geometry, XY coordinates, sprite/heading/import assumptions and asset-production plan. The user selected block-shaped 3D voxel graphics with backbone first; the accepted monochrome primitive view stays until a later renderer milestone. Earlier originals remain provenance/history, not required runtime farm assets. UI accessibility and independent audio-control principles remain applicable; audio production stays deferred.

Authority: Chief delegation ART-02 and decision 001 at baseline 17d383369442fe9271d92f53ec472a8f105cc6d6. This is a presentation-local specification. It introduces no Domain/Application DTOs or event names. Experience owns scene/UI and binding code; Creative owns these originals and metadata. Source SVGs remain in docs/creative/primitives until Experience/Chief chooses the import route. No Unity package installation or game metadata was performed.

## Exact art tokens and geometry

All colors are opaque sRGB hex. Transparent pixels remain transparent; no baked background except soil. Token names are local design identifiers, not shared runtime constants.

| Token | Value | Use |
| --- | --- | --- |
| ink | #20332D | Text, wheels, outlines |
| paper | #F5EEDC | Panel, cab, warning fill |
| soil | #6B4936 | Empty cell, depot door |
| leaf | #426B45 | Growing wheat |
| straw | #DAB967 | Ripe seed heads |
| tractor | #C65C3A | Vehicle paint |
| water | #53838E | Depot roof |
| focus | #185E78 | Focus outline on paper |

Primary text uses ink on paper. Other tokens are illustrations, not arbitrary text pairs. Selection uses a paper outline inside an ink outline, plus tractor name/heading text. Growing wheat has two leaves; ripe wheat has three seed heads. Icons retain visible text labels and meaningful accessible control names. SVG title is descriptive source metadata, not a substitute for UI labels.

Domain cell (x,y) maps to Unity world center (x + 0.5, y + 0.5, 0), one unit per cell. This matches x east, y north. Tile boundaries are integer coordinates. A screen-space farm using downward-positive y maps cell center to (left + (x + 0.5)*cellPixels, top + (height - y - 0.5)*cellPixels). Do not reverse Domain coordinates. Depot uses the actual depot cell; crop artwork is centered on its tile; tractor is centered on its authoritative position.

Every SVG has viewBox 0 0 128 128, source center (64,64), transparent exterior, no external references/fonts/scripts. SVG y increases downward. Tractor points toward source top = North. In Unity positive counterclockwise rotation: North 0°, East -90°, South 180°, West 90°. For downward-positive UI rotation use the equivalent clockwise mapping 0°, 90°, 180°, 270°. Body/wheels remain inside one cell, and the front triangle provides an explicit heading cue. All sprite pivots are normalized (0.5,0.5). No implement footprint or animation is encoded. Display equipped implement as text until its contract and asset are coordinated.

Layers, back to front: terrain, depot marker, crop, tractor, selection/action indicators, text/UI. If using SpriteRenderer, proposed presentation-local sorting layer is Default with orders 0,10,20,30,40; UI uses its existing canvas order. This requires no new project sorting layer. Depot occupancy and movement legality come from Domain, not this layering. An empty crop is rendered with soil-empty; growing/ripe comes only from approved snapshot crop state/maturity, never an independent presentation timer. If those fields are not available, report the missing mapping to Chief instead of inventing DTO fields.

## Sources, import routes and boundaries

[manifest](primitives/manifest.json) lists ten originals and SHA-256 checksums: tractor-north, wheat-growing, wheat-ripe, soil-empty, depot, and run/pause/step/stop/warning icons. They are simple XML geometry suitable as reference for existing primitive drawing code. No downloaded, generated raster, stock, copied, traced or externally referenced material is included. Rights metadata records project-original authorship without asserting a project-wide license that Chief has not selected.

Preferred spike route: Experience reproduces these rectangles/ellipses/paths using its existing primitive renderer or keeps current readable primitives. SVG delivery does not require SVG import support. If an already approved SVG importer exists, use its established compatible settings and validate pivot/scale visually; do not add or pin a package from this spec. If unavailable, defer SVG runtime use. An authorized later rasterization can produce a transparent 128 × 128 PNG per source; no rasterization is delivered here.

Exact proposed PNG Sprite import settings for that later route: Texture Type Sprite (2D and UI), Sprite Mode Single, pixels per unit 128, pivot Center, mesh Full Rect, extrude edges 1, sRGB enabled, alpha source input, alpha transparency enabled, mipmaps disabled, read/write disabled, wrap Clamp, filter Bilinear, max size 128, compression None and no platform override. One 128px source covers one world unit. Icons are displayed at 24–32 logical pixels with labels; larger accessibility scaling is allowed. At tiny farm zoom retain heading/ripe labels rather than relying on details. SpriteAtlas packing is deferred to avoid introducing configuration. Chief approves the actual import convention before files move into game/Assets/Harvest/Art; Creative then owns corresponding stable .meta files. Do not invent .meta GUIDs in the handoff or duplicate source copies with divergent edits.

## Audio binding and mixer

Silence is the delivered state: no audio files, generated tracks, audio source components or Unity mixers are included. The following specifies Experience's future presentation adapter and configuration only. Audio never submits commands, advances ticks, changes outcomes or evaluates objectives.

Mixer tree: Master → Music and SFX; SFX → Engine and Ambience. General one-shot effects route directly to SFX. Expose normalized sliders plus independent mute flags for all five controls when their categories exist. Persist user preferences outside campaign/sandbox progress. Initial gains: Master 0.8, Music 0.25, SFX 0.65, Engine 0.35, Ambience 0.25. Effective gain is the product along the route; any muted ancestor yields zero. For a dB mixer parameter, use 20*log10(gain) for positive gain, clamp at -80 dB, and disable/pause output at mute/zero. Do not call log10(0). Remember the previous slider value when toggling mute. No ambience control until ambience exists. Music is not slowed by tick duration or Step.

Bindings use semantic categories below; these are adapter labels, not new shared event kinds. Concrete member names await owner DTO publication. Use already approved ordered domain events/terminal results and SessionController status; no success inferred from input clicks or elapsed animation time.

| Binding | Authoritative source | Playback |
| --- | --- | --- |
| Run feedback | Successful application start/resume result | One quiet click; none on failed compile |
| Motion | Pending MoveForward plus Running status | At most one engine loop per actor; silent otherwise |
| Turn | Successful terminal TurnLeft/TurnRight | Optional creak, rate limited |
| Harvest | Successful terminal Harvest | One dry rustle |
| Unload/refuel | Successful terminal Unload/Refuel | One depot clunk |
| Rejection | Terminal result ok=false | One soft warning and visible structured reason |
| Diagnostic | Application compile/runtime diagnostic publication | One warning per diagnostic publication, with source location |
| Objective | Application-confirmed transition incomplete→complete | One restrained original completion cue |
| Pause/Stop/load/session replacement | Application state/boundary notification | Stop engine; clear transient voice queue |

If events and terminal results both describe one action, choose one source for one-shots. Deduplicate by presentation session epoch + actorId + requestId; terminalTick is useful ordering information, not a substitute for request identity. Increment a local epoch on load/new session so restored request numbers do not suppress new cues. Bound dedup memory with a last-seen request per actor for ordered streams; if stream ordering is unavailable, use a capped 256-key queue and never replay historical events from a loaded snapshot. Diagnostic dedup uses publication sequence/source revision, not only message text, so a later deliberate retry may sound again. These keys are local bookkeeping, not requested shared fields. If notifications cannot distinguish publication from repeated polling, report the binding dependency and remain silent.

Limit total audible voices to eight: at most two engine loops plus six one-shots, prioritizing control/diagnostic feedback over farming effects. Current milestone has one engine actor. Coalesce repeated harvest/depot cues within 100ms real time, warnings within 500ms real time; record every authoritative event in the visible bounded console regardless. These are audio rate limits, not simulation rules. A new warning replaces a lower-priority effect at capacity. Pause suppresses ongoing world loops immediately with up to a 50ms presentation fade; one tick via Step may play its completion cue but never leaves an engine loop running. Stop/load/new session discard pending transient cues. Music may continue during Pause; unfocused mute silences all playback when enabled. Explicit UI test-sound activation previews a bounded SFX cue, never on panel open.

## Experience acceptance and concrete dependency

Ready now: original primitive sources, checksums, geometry/color values and presentation/audio specification. Experience can use geometry without importing a package. Next dependency: Experience maps actual published snapshot/result/status members to this specification and reports any missing contract to Chief. Audio binding is deferred until those notifications and approved clips exist. No shared contract edits are requested here.

Verify in Unity before declaring integration complete: North/East/South/West on known cells, depot marker alignment, growing/ripe/empty distinction, readable heading at minimum supported zoom, visible text labels and keyboard focus, Pause/Step/Stop state fidelity and reduced-motion behavior. For later raster import inspect transparent edges and exact one-cell scale. For later audio verify Master/Music/SFX/Engine/Ambience isolation and hierarchy, zero gain, no duplicate terminal cues, no stale cues after load, no stuck loops after Pause/Stop and bounded repetition. Current source validation is not Unity/render/audio acceptance.
