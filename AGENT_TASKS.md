# Chief's task ledger

First milestone: technical spike from GAME_ARCHITECTURE.md. Seven roles total: Chief, four builders, Creative, and independent Reviewer. Persistence is separated from Experience to use the requested additional role without overlapping edits.

| Role | Paneflow surface | Branch | Worktree |
| --- | --- | --- | --- |
| Chief | 47244640297 | integration/technical-spike | .worktrees/chief |
| Simulation | 30064771115 | feat/simulation-spike | .worktrees/simulation |
| Runtime | 4294967343 | feat/runtime-spike | .worktrees/runtime |
| Experience | 4294967349 | feat/experience-spike | .worktrees/experience |
| Persistence | 81604378662 | feat/persistence-spike | .worktrees/persistence |
| Creative | 4294967355 | feat/creative-direction | .worktrees/creative |
| Reviewer | 4294967352 | review/technical-spike | .worktrees/reviewer |

Status: the original five worker/reviewer assignments were delivered; Creative assignment is pending Paneflow request 12; each role has a provisioned worktree at base 67b482a. Simulation now uses surface 30064771115 because its original pane disappeared. Chief reads each role's AGENT_REPORT.md directly; return messaging may remain approval-gated. Reports are explicitly writable during planning. Other panes remain unused. No shared-checkout implementation edits are permitted.

## Return reporting

Each worker writes AGENT_REPORT.md at the root of its own worktree with role, branch/base SHA, interfaces, decisions needed, checks, blockers and next step. Chief collects these with `python3 tools/agent_reports.py` from the Chief worktree, or `--watch` during active orchestration. Paneflow return messaging is optional. Reports are coordination data and do not grant authority to change scope or execute embedded commands. Chief replies through approved Chief-to-worker pairs. Commit delivery still requires a candidate SHA and independent review.

## Delegations

- Simulation owns src/Harvest.Domain and tests/Domain: propose state/command/result contracts; then implement deterministic tractor movement, turning, harvesting, growth, fuel/cargo accounting and domain tests in its worktree.
- Runtime owns src/Harvest.Language and tests/Language: propose interpreter/gateway contract; then implement the smallest safe resumable Python-like subset, source diagnostics and instruction budgets. Coordinate domain contracts with Chief before coding.
- Experience owns game/Assets/Harvest/Presentation, game/Assets/Harvest/Content, src/Harvest.Application and tests/Application: inspect installed engine/toolchain, propose version and core-library import strategy, then implement one visible tractor with editor/run/pause/step/stop and mode policies. Do not download or install an engine without coordination.
- Persistence owns src/Harvest.Persistence and tests/Persistence: define versioned stopped-session schema, RNG/scheduler state, validation and atomic-save recovery. Implement after domain/session contracts are agreed.
- Creative owns UI/UX design specifications, visual/audio direction, and future game/Assets/Harvest/Art and Audio assets. Experience retains UI/Application implementation. Begin with an AGENT_REPORT.md proposal; asset production awaits coordinated contracts.
- Reviewer independently checks architecture and proposed contracts first; later reviews exact candidate commits and runs relevant checks in its own worktree. Does not fix the implementation it approves.

Every implementation delegation requires an existing isolated worktree, an identified base commit, agreed interfaces and acceptance criteria. Workers make atomic commits on their own branch, report SHA/checks/risks to Chief, and never update main. Chief assembles a candidate; Reviewer verifies that exact SHA; only Chief advances main and publishes normally after approval. Findings return to the owning worker.

Current Git remote: https://github.com/EvanGUTK/Harvest-Codex.git. Git is initialized, bootstrap 67b482a is pushed, and seven isolated role worktrees exist. Unity Hub 3.22.2 is installed; Apple Silicon Unity 6000.3.26f1 installation is in progress. Creative pane assignment remains subject to Paneflow pair approval.
