# Harvest Codex agent rules

Read GAME_ARCHITECTURE.md and AGENT_TASKS.md before work. Chief assigns ownership and approves shared interface changes. Each agent edits only its assigned worktree and paths. Never implement in the shared main checkout. Use small cohesive modules, explicit contracts, structured errors and bounded player-code execution. No host eval, hidden failures, duplicated domain rules or shortcut tutorial solutions.

Make atomic commits with related meaningful tests and docs. Report commit SHA, checks and risks to Chief. Do not update main or push unless Chief delegates it. Independent Reviewer reviews the exact integrated candidate SHA; fixes return to owners and require renewed review. Chief alone advances main to the approved candidate and pushes normally. Never force push or destructively reset.

## Active orchestration

Chief keeps the work cycle active until the assigned milestone is delivered or a concrete user decision/access blocker prevents progress. Finishing a proposal, review, or worker task is a handoff, not the end of the overall task. Chief reads report changes, resolves routine decisions, issues the next assignments, integrates candidate commits and requests exact-SHA review without waiting for the user to ask for status. If a user answer is required, ask promptly with the blocker and continue independent work.

Workers update AGENT_REPORT.md on start, meaningful blocker, and completion. The report header names task ID, status (working/ready/blocked), base SHA, delivered commit SHA when applicable, checks, blockers, and next dependency. Report completion even when messaging is approval-gated. Do not stop merely because an optional return message failed; the report is the return path. Complete authorized owned work and meaningful checks before handing off. Never invent shared APIs to avoid a dependency; report it promptly so Chief can resolve it.

Reviewer writes exact candidate SHA, approved or changes-requested disposition, checks and concrete findings as soon as review completes. Chief watches that file during review, then delegates fixes or advances to the next stage. Main still requires independent approval of the exact candidate; autonomous work does not bypass review or Paneflow permission gates.
