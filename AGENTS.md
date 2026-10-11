# Harvest Codex agent rules

Read GAME_ARCHITECTURE.md and AGENT_TASKS.md before work. Chief assigns ownership and approves shared interface changes. Each agent edits only its assigned worktree and paths. Never implement in the shared main checkout. Use small cohesive modules, explicit contracts, structured errors and bounded player-code execution. No host eval, hidden failures, duplicated domain rules or shortcut tutorial solutions.

Make atomic commits with related meaningful tests and docs. Report commit SHA, checks and risks to Chief. Do not update main or push unless Chief delegates it. Independent Reviewer reviews the exact integrated candidate SHA; fixes return to owners and require renewed review. Chief alone advances main to the approved candidate and pushes normally. Never force push or destructively reset.
