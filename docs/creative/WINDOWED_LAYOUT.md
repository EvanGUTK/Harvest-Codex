# ART-04 windowed layout budgets

Docs-only proposal for Experience, based on main 1ba094c and inspected `/tmp/harvest-ui-stop-reference.png`. Reference currently expands while inspector has only MaxWidth; the capture shows an approximately 90px inspector. Reserve inspector width before distributing remaining space. Reference intrinsic text width must never set pane width. These budgets supersede the earlier percentage layout for the current windowed prototype; voxel art/audio remain deferred.

All values below are Unity GUI logical pixels for the **client area**, excluding OS chrome. Keep 12px outer margins and 8px between panes. Font = round(16 × textScale), scale 1..1.5 (16..24px). Do not shrink text to preserve a layout. Pane widths include 8px interior padding on each side and a reserved 16px vertical scrollbar. Measure actual control text at the chosen font; switch layout if minimums cannot fit.

## Width allocation

| Client / scale | Available width | Main content | Farm | Inspector |
| --- | --- | --- | --- | --- |
| 1280×800 / 1 | 1256 | 544: Code **or** Reference | 416 | 280 |
| 1280×800 / 1.5 | 1256 | 912: Code/Farm/Reference tabs | In main tab | 336 |
| 800×600 / 1 | 776 | 776: Code/Farm/Reference/Tractor tabs | In main tab | Tractor tab |
| 800×600 / 1.5 | 776 | 776: same tabs | In main tab | Tractor tab |

At 1280/1 the sum is 544+416+280+2×8=1256. Reference replaces Code in the 544px slot and does not add a fourth column. Set explicit pane widths and clip/scroll inside them; do not rely on ExpandWidth competition or MaxWidth-only inspector. In the other wide layout 912+8+336=1256. Main tab switches preserve draft, selection and each panel's scroll offset.

For intermediate font scales, three columns are allowed only if available width ≥ (544×scale)+(416×scale)+max(280,224×scale)+16. Otherwise use the two-column tabbed layout if main width ≥544 and inspector width max(280,224×scale) can be reserved with an 8px gap. Otherwise use full-width tabs. The 1280/1.5 two-column row is intentional. Do not blindly keep a width-only 1050px breakpoint at larger text. Compact layout never places an unbounded inspector under the body.

Inspector text wraps within its reserved width; fuel/cargo/position/heading remain separate labeled rows. Put long objective/modifier text in the inspector's vertical scroll region. At compact widths, keep selected tractor ID, heading and fuel/cargo in a bounded two-line status summary above the body; full details live in the Tractor tab. Summary details are duplicated for readability, not independently computed game state.

## Height allocation

| Client / scale | Available height | Header/controls/status | Body including tabs | Diagnostics/console footer | Gaps |
| --- | --- | --- | --- | --- | --- |
| 1280×800 / 1 | 776 | 196 | 432 | 140 | 8 |
| 1280×800 / 1.5 | 776 | 240 | 376 | 152 | 8 |
| 800×600 / 1 | 576 | 200 | 272 | 96 | 8 |
| 800×600 / 1.5 | 576 | 240 | 224 | 104 | 8 |

There are two 4px vertical gaps. Interpolate these height budgets between scale endpoints. Body is a bounded viewport, not a GUILayout group whose growing children push the footer offscreen. Allow content scrolling instead of maintaining the current editor/reference MinHeight(180) in every configuration. At 800/1.5, a 40px tab strip leaves 184px for active content; reserve panel title/padding within that area.

Header order: title/status/tick; Run/Resume, Pause, Step, Stop; Save/Load; mode; display settings; storage/selected summary. Use 32px minimum button/row height at scale 1, 40px at 1.5. At 800/1.5 combine execution and storage into a measured row only if all six labels fit; otherwise execution uses one row and Save/Load share the mode row. Short visible labels Run/Resume, Step and Load are acceptable with persistent nearby explanations (“Step: one tick”, “Save stops execution”, “Load replaces farm after confirmation”). Never abbreviate labels into ambiguous icons. Move display settings into a bounded Settings tab/popover if header cannot fit; keep keyboard access and current text scale visible. Do not put essential run/storage controls below a page-level scrollbar.

Footer has Diagnostics/Console tabs, each with a separate vertical scroll position. A diagnostics count/status remains visible while viewing console. Show line/column and reason as wrapped text; never reserve both full 75px and 70px scroll regions plus headings at 800px width. The Step explanation and editor Tab hint can live in reference/help instead of consuming an unconditional bottom row.

Storage failures show a short wrapped status summary in the allocated header; full details scroll in a body panel. Load confirmation/recovery replaces the body with a bounded Storage view containing explanation, scrollable choices and pinned Confirm/Cancel controls. It does not append 90px recovery plus confirmation rows above the farm. Preserve explicit confirmation semantics and mode-change invalidation; do not alter storage contracts. On compact/high-scale layouts use “Confirm load” or “Confirm recovery” with the replacement consequence stated above. Restore the prior active tab after cancel/completion. Long recovery labels wrap and stay within the panel.

## Wrapping, farm and scroll rules

- Reference titles/descriptions wrap at actual content width (pane minus padding/scrollbar). Descriptions never request intrinsic horizontal width. Examples preserve newlines/indentation and use a local horizontal scrollbar for long code; do not wrap code into misleading statements. Read-only reference stays inert.
- Draft editor scrolls vertically and horizontally as needed, with no outer-page horizontal overflow. Changing tabs/layout/scale never replaces draft text or moves focus into a Run action. All reference, inspector, storage and diagnostic text is selectable/readable where current UI permits.
- Farm fits inside its bounded viewport; no fixed 200px minimum forces the compact height budget to overflow. At small cells show short shape/state cues and keep full selected-cell/tractor information in inspector/Tractor tab. Do not squeeze “North”, “Depot” and resource text into overlapping tile labels. Zoom/pan or selection details are preferable to illegible per-tile text.
- Each content pane owns its scrollbars and clips its drawing. Focused content remains reachable by keyboard; tab strip/controls stay outside content scroll. Resizing preserves state and clamps scroll offsets to new bounds. No scrollbar should steal width reserved for another pane.

## Acceptance and handoff

Experience implements; Chief/Reviewer verify the exact candidate. Check all four table configurations **in windowed mode**, plus scale 1.25 and a resize across each layout transition. This spec does not claim rendered checks passed.

With Reference open, confirm inspector is ≥280px at 1280/1, ≥336px at 1280/1.5; at 800px, full details remain reachable through Tractor tab. Confirm Run/Pause/Step/Stop/Save/Load remain on screen at 800/1.5; active Load remains disabled according to actual status. Read longest unlocked reference description/example and long objective/diagnostic/recovery text without clipping or widening adjacent panes. Exercise confirmation and recovery, cancel, switch modes, resize and return to draft: no hidden controls, draft replacement, lost selection or state changes caused by layout. Farm cells and selected heading remain readable without inspector overlap. Keyboard reaches tabs, scrolling and Confirm/Cancel; text scaling never gets reset to fit. Capture the four configurations and label any missing verification honestly.

Risks: actual skin padding/glyph metrics may exceed estimates; use measured fit and more compact tabs/settings rather than sacrificing text or inspector width. Farm readability requires rendered playtest. No source/UI contract changes, art/audio or launches are part of ART-04.
