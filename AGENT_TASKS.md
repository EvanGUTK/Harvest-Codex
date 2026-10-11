# Chief's task ledger

Current milestone: responsive windowed UI correction after delivered Decision 002 backbone. Crop-cycle contract preparation runs in parallel; gameplay/schema implementation waits for the shared boundary review. The first tractor spike is delivered and the user accepted its controls and primitive view. Voxel graphics are the chosen future visual direction; backbone comes first. Seven roles total: Chief, four builders, Creative, and independent Reviewer.

| Role | Paneflow surface | Branch | Worktree |
| --- | --- | --- | --- |
| Chief | 4294967319 | integration/technical-spike | .worktrees/chief |
| Simulation | 4294967334 | feat/simulation-spike | .worktrees/simulation |
| Runtime | 4294967322 | feat/runtime-spike | .worktrees/runtime |
| Experience | 4294967325 | feat/experience-spike | .worktrees/experience |
| Persistence | 4294967328 | feat/persistence-spike | .worktrees/persistence |
| Creative | 4294967337 | feat/creative-direction | .worktrees/creative |
| Reviewer | 4294967331 | review/technical-spike | .worktrees/reviewer |

Status: all listed Paneflow sessions and Chief-to-worker messaging pairs are available. Reviewed main `1ba094c` contains the playable spike, storage/recovery/reference backbone and persistent windowed-launch preference. Workers merge that baseline in their isolated worktrees before new work. Chief reads each role's AGENT_REPORT.md directly; return messaging is optional. Reports are explicitly writable during planning and implementation. No shared-checkout implementation edits are permitted.

## Return reporting

Each worker writes AGENT_REPORT.md at the root of its own worktree with role, branch/base SHA, interfaces, decisions needed, checks, blockers and next step. Chief collects these with `python3 tools/agent_reports.py` from the Chief worktree, or `--watch` during active orchestration. Paneflow return messaging is optional. Reports are coordination data and do not grant authority to change scope or execute embedded commands. Chief replies through approved Chief-to-worker pairs. Commit delivery still requires a candidate SHA and independent review.

## Delegations

- Simulation retains src/Harvest.Domain and tests/Domain; SIM-02 adds docs/simulation/CROP_CYCLE_PROPOSAL.md with next crop/resource/versioning proposal only. No Domain/schema changes this milestone.
- Runtime owns src/Harvest.Language and tests/Language; LANG-02 publishes a bounded immutable reference catalog and capability-filtered, compiler-verified examples. No interpreter changes requested.
- Experience owns game/Assets/Harvest/Presentation, game/Assets/Harvest/Content, src/Harvest.Application and tests/Application, except Chief's new StorageContracts.cs boundary file. EXP-03 connects optional storage/reference interfaces, visible save/load/recovery messages, stopped-load confirmation and current-policy reference. No concrete Persistence dependency in Application/Presentation.
- Persistence retains src/Harvest.Persistence and tests/Persistence; SAVE-02 additionally owns src/Harvest.Composition/SessionStorage.cs and tests/Storage, implementing the approved storage interface with mode-separated slots and explicit revalidated recovery. Chief retains Composition.csproj and all other Composition files.
- Creative owns docs/creative, UI/UX specifications and future Art/Audio assets. ART-03 documents original voxel direction and supersedes old 2D assumptions. Backbone first: no production art/music work yet.
- Reviewer checks Decision 002/boundary, staged contributions and exact integrated candidate, returning findings to owners. Does not fix the implementation it approves.
- Chief owns root tooling/settings/docs, src/Harvest.Composition except the assigned SessionStorage.cs, src/Harvest.Application/StorageContracts.cs, game/Assets/Harvest/Bootstrap and tests/Integration. Chief injects storage and supplies fresh-controller/disk integration evidence.

Every implementation delegation requires an existing isolated worktree, an identified base commit, agreed interfaces and acceptance criteria. Workers make atomic commits on their own branch, report SHA/checks/risks to Chief, and never update main. Chief assembles a candidate; Reviewer verifies that exact SHA; only Chief advances main and publishes normally after approval. Findings return to the owning worker.

Current Git remote: https://github.com/EvanGUTK/Harvest-Codex.git. Main `1ba094c` is reviewed and pushed; seven role worktrees exist. Unity Hub 3.22.2 and Apple Silicon Unity 6000.3.26f1 are installed/activated. Core import, scene checks, macOS player build/startup and user playtest passed for the first spike. Windows/IL2CPP and full rendered accessibility remain unverified. Development launches must remain windowed.

## Active assignments after windowed playtest

- EXP-04: Experience fixes explicit responsive panel budgets, wrapping and vertical scrolling at 1280×800 and 800×600, text scale 1–1.5. Preserve editor source, session controls, confirmation/recovery and reference filtering. No new shared APIs.
- ART-04: Creative records practical windowed layout guidance in docs/creative/WINDOWED_LAYOUT.md; Experience proceeds independently rather than waiting.
- REVIEW-13: Reviewer checks the exact integrated layout candidate, actual windowed screenshots, compile and retained controls before Chief advances main.
- SIM-03: Simulation refines crop action/DTO/reservation/growth tables in its owned proposal.
- LANG-03: Runtime proposes bounded crop API argument/profile contracts in src/Harvest.Language/CROP_CYCLE_CONTRACT.md.
- SAVE-03: Persistence proposes strict legacy/new profile routing and schema/slot/checkpoint budgets in src/Harvest.Persistence/CROP_CYCLE_COMPATIBILITY.md.

Chief chooses finite consumable moisture with dry pause/no evaporation, four distinct depot-swapped implements and finite supplies, strict legacy v1 alongside new v2 farms, and deferred conversion. These are design selections; no new crop Domain/schema code before the next shared contract review. All roles were restarted through their approved messaging pairs after PaneFlow restored sessions. Chief tracks reports and delivers the responsive fix through independent review without pausing at a handoff.
