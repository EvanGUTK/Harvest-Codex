# One-tractor creative direction

> Historical 2D/SVG proposal: ART-03 [VOXEL_DIRECTION.md](VOXEL_DIRECTION.md) supersedes the farm geometry, XY coordinates, sprite/heading/import assumptions and asset-production plan. The user selected block-shaped 3D voxel graphics with backbone first; the accepted monochrome primitive view stays until a later renderer milestone. Earlier originals remain provenance/history, not required runtime farm assets. UI accessibility and independent audio-control principles remain applicable; audio production stays deferred.

Status: planning proposal for Chief and Experience; no art, audio, UI implementation or shared contracts are introduced. Target editor reported by Chief: Unity 6000.3.26f1. Experience owns Presentation/Application UI implementation; Creative owns this specification and future Art/Audio assets only after Chief coordinates contracts.

## Identity and palette

A working farm seen through a tidy field notebook: warm soil, practical painted machinery, restrained paper panels and precise technical annotations. Original top-down shapes should make heading, ripe crops and traversable cells easy to read. Avoid copying another game's sprites, interface composition, branding or soundtrack. Tone is patient and constructive; errors explain what happened without scolding.

Use flat, softly textured illustrations with crisp silhouettes rather than tiny decorative detail. The prototype may use primitive shapes. Camera stays overhead; no perspective that obscures cell occupancy. Tractor body fits inside its occupied tile; wheels and a clearly marked front establish heading. Implement decoration must not imply extra occupied cells. Grid, selection and pending-action indicators sit above terrain without hiding crops.

| Role | Proposed color | Use |
| --- | --- | --- |
| Ink | #20332D | Text, outlines, panel background in dark variant |
| Paper | #F5EEDC | Main panel background and light text in dark variant |
| Soil | #6B4936 | Field base |
| Leaf | #426B45 | Growing crop body |
| Straw | #DAB967 | Ripe crop accents and objective emphasis |
| Tractor | #C65C3A | Main vehicle paint |
| Water | #53838E | Moisture cues and secondary accents |
| Focus | #185E78 | Keyboard focus outline on paper |

These are art tokens, not verified UI contrast pairs. Use Ink/Paper for primary text; test final rendered text, focus and state combinations before shipping. Never use Straw on Paper for small text. Status communicates with icon, label and shape as well as color: ripe = full seed head + “Ripe”; selected = outline + tractor name; failure = warning symbol + reason. Growing and harvested cells need distinct silhouettes, not hue shifts alone.

## Proposed desktop UI

Initial reference canvas: 1440 × 900 logical pixels, responsive rather than a fixed-resolution requirement.

- Top strip: mode, session status, Run, Pause, Step and Stop, with plain text labels and shortcut hints. Keep Reset separate and confirm its checkpoint effect. Show tick number beside status.
- Left, about 40% of usable width: editable program, line numbers, active-source indication and inline diagnostic marker. An error summary gives line/column, explanation and suggested next action without supplying a complete tutorial solution.
- Center, about 42%: farm viewport with selected tractor, heading marker, depot marker and optional grid. Zoom and pan never alter selection or simulation state. Provide a visible “Center tractor” control.
- Right, about 18%: selected tractor name, heading, position, fuel, cargo and implement, followed by current objective. Values and limits are text as well as bars. Do not infer rules or objective completion from visuals.
- Bottom, collapsible: bounded event console with source location where available. Reference/help opens beside the editor or replaces the inspector on narrow windows; it must not cover run controls.

Below the width needed for readable editor and field, switch to explicit Code/Farm tabs with persistent controls, diagnostics badge and tractor status. Provide keyboard-accessible panel resizing and minimum usable sizes. Initial proportions are a playtest starting point, not a layout contract.

Run starts the requested program; Pause freezes simulation time; Step advances exactly one simulation tick while paused; Stop cancels at the defined boundary while retaining farm state. Experience must expose actual application status, including stopping and failure states, rather than anticipating completion. Label the distinction between a simulation tick and a source statement near Step. Preserve scripts and selection on errors. Keep the active line distinguishable from selection, breakpoint-like decoration and diagnostic lines.

Focus order: controls → editor → viewport tools → inspector/objective → console → reference. Editor Tab behavior needs a documented way to leave the editor. Run shortcuts must not collide with text editing; list final bindings in help. No control may require pointer hover to reveal essential information.

## Asset inventory and scope

All dimensions below are proposed source sizes; Experience decides import scale and filtering after Chief approves the asset convention. Anchor field assets at cell center. Heading must follow authoritative state; do not rotate gameplay coordinates to accommodate art.

| Asset | Proposed variants | Milestone priority |
| --- | --- | --- |
| Tractor | Four cardinal headings, idle and movement presentation; 128 × 128 source cells | Required silhouette; primitives acceptable |
| Harvest implement | Four heading-aligned overlays matching tractor anchors | Required readable equipped state; simple badge acceptable |
| Starter crop | Bare/harvested soil, growing silhouette, ripe silhouette; 128 × 128 source cells | Required states only where domain exposes them |
| Terrain | Soil tile, field edge, blocked cell marker | Required clear boundaries |
| Depot | One unloading marker, readable at minimum zoom | When depot exists in approved milestone |
| Selection/action overlays | Selection outline, heading arrow, pending/action progress, rejection marker | Required selection and heading; action overlays optional |
| UI icons | Run, pause, step, stop, warning, fuel, cargo, implement, mute | Labels remain usable without icons |
| Crop-cycle additions | Tilled soil, sown state, moisture overlay, further crop types | Later vertical slice; no invented domain states |

Movement interpolation is cosmetic between reported positions; cancellation snaps or settles to the next authoritative snapshot without changing results. Harvest animation follows completion and does not remove crops early. Default action motion should be short and restrained; reduced-motion mode uses immediate state updates and static progress cues. No flashes, camera shake or mandatory animated tutorials.

## Sound and music direction

Farm machinery should sound small and tactile: soft engine pulse, dry wheel/soil movement, brief implement rustle, and a light depot clunk. Avoid loud industrial rumble or reward sounds that become tiring during repeated commands. Music is optional, sparse and instrumental: warm plucked strings, muted keys and long rests, with no vocals or recognizable borrowed melody. Do not commission or generate a track for the technical spike; silence is an acceptable default until controls and contracts exist.

The table names conceptual presentation triggers, not new runtime event identifiers. Experience maps existing snapshots/events; Chief approves missing contract requirements. Never infer success from a click, animation timer or changing resource display.

| Conceptual trigger | Direction | Repetition policy |
| --- | --- | --- |
| Run accepted | Quiet short mechanical click | Once per accepted run |
| Tractor moving | Gentle engine/wheel layer | One loop per audible tractor; stop when motion ends |
| Turn completes | Optional light steering creak | Omit when frequent; no overlapping bursts |
| Harvest completes successfully | Brief dry crop rustle | Bound repeated cues and coalesce bursts |
| Unload completes successfully | Soft bin/depot clunk | Once per completion |
| Action rejected / source diagnostic | Restrained two-note cue with visible reason | Deduplicate same diagnostic; no continuous warning loop |
| Objective completed | Short original musical resolution | Once per confirmed completion |
| Pause / Stop | Fade moving layers out | No simulated engine activity while paused/stopped |

Suggested initial mix: short effects below speech-like prominence, music lower than effects, engine quieter still. Final loudness targets require listening tests; these descriptions are not mastering specifications. Use a bounded voice pool and prioritize user controls/diagnostics over machinery. Audio dropping or coalescing must never drop console events or alter simulation. Keep soundtrack timing independent of simulation ticks; muted music must not affect gameplay.

## Accessibility and independent audio controls

Offer Master, Music, SFX, Engine and Ambience sliders with independent mute controls; Engine is a submix of SFX, so SFX mute silences it too. Set absent ambience to disabled or omit it until assets exist. Remember preferences independently from campaign/sandbox saves. Add “Mute when unfocused” and a UI test sound button; preview plays only on explicit activation, never when opening settings. Music mute must preserve SFX, and SFX mute must preserve music. Master mute silences all categories.

Every audible event has an equivalent visible state or message; gameplay never depends on stereo position, rhythm or hearing. Provide scalable UI/editor text, visible keyboard focus, high-contrast state labels, optional grid and reduced motion. Animation-speed preference changes presentation only; do not confuse it with simulation speed. Confirm final text/control contrast and zoom legibility on actual rendering. Screen-reader support remains an implementation feasibility question for Experience, not a claimed delivered feature; ensure controls have meaningful names and diagnostics have a text representation.

## Provenance and handoff

This specification is original written planning. Hex colors and geometric descriptions are proposals, not downloaded assets. No external assets, generated images, sound recordings, music, fonts or packages were acquired; no asset license is implied. Use existing approved UI fonts for placeholders. Future assets need a manifest with file path, creator/source, creation method, license and redistribution rights, attribution obligations, modifications and approval status. Generated assets also record tool/model, prompt and reference-image rights where available. Chief approves acquisition and contract scope first.

Experience can implement the one-tractor layout with text and primitives using existing approved contracts. Creative supplies visual/audio assets later. Before asset production, agree with Chief and Experience on coordinate orientation, cell scale, sprite anchors, sorting, state availability, event payloads, cancellation behavior, import settings, audio mixer categories and asset ownership paths. No requested item changes Domain/Language rules.

Review the milestone by running a real program that harvests, pauses, steps, stops and produces a source diagnostic. Verify heading/occupancy remain readable; source errors include location; controls show actual state; Pause freezes the world; Step is described as a tick; Stop keeps the farm; reduced motion preserves state; keyboard operation reaches panels; text scales without hiding controls. If audio is present, verify independent mutes, no stuck engine on pause/stop and bounded repeated effects. If no audio exists, record audio checks as deferred rather than passed. Chief/Reviewer approve the exact integrated candidate; this spec is not an implementation acceptance claim.
