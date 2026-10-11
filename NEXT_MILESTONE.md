# Next milestone: one programmable tractor

Status: proposed implementation plan, informed by all five agent reports. No game implementation authorized to workers by this plan alone; Chief delivers explicit implementation tasks after contracts are reviewed.

## Outcome

One tractor on a small field executes player-written code, moves, turns, harvests, unloads and refuels. The player can run, pause, advance exactly one tick and stop. Errors show the source location. Campaign exposes a small teaching subset; sandbox exposes every implemented capability. Saving restores a stopped farm. A visible engine demonstration is required before claiming the playable milestone complete.

## Sequence and ownership

1. Chief reconciles the seven-role ledger and historical setup status, pins build tooling, owns root solution/configuration, and writes a versioned contract decision. Reviewer checks the contract before parallel implementation.
2. Simulation defines Domain-owned immutable snapshots, commands, request IDs, action results and stop/cancel semantics, then implements domain rules. Runtime starts lexer/parser and bounded continuations in parallel using agreed contracts. These independent commits must build and include meaningful tests.
3. Experience builds the pure Application session controller, capability policy and runtime-to-domain gateway. Chief owns cross-module integration tests. Presentation work follows identification and pinning of an available Unity editor; lack of an editor does not block core implementation, but does block visible-game acceptance.
4. Creative owns docs/creative design handoff and Art/Audio assets with coordinated metadata/import conventions. Experience implements its layout from real state; primitive graphics and silence are acceptable for this spike. Persistence implements validated stopped-session save/load after snapshot contracts stabilize. Verify interrupted-write recovery and campaign/sandbox separation. Experience integrates editor controls and tractor visualization from authoritative snapshots.
5. Chief assembles the candidate branch. Reviewer checks the exact candidate SHA, builds/tests and visible smoke evidence. Fixes return to owners. Chief advances main to the approved SHA and pushes normal atomic history.

## Contracts to settle first

- Application alone coordinates ticks. Runtime consumes snapshot at tick t and yields at most one command per actor. Domain validates/arbitrates before growth, progresses actions, advances growth once and emits snapshot t+1. Duration d accepted at t completes at t+d. Completion resumes script at its next pass; rejection also yields until the next pass.
- Domain owns command/result/snapshot DTOs; Language depends on Domain contracts. Application owns capability enforcement at the gateway and session boundaries; Language checks supported syntax and capability use at compilation/execution. Domain revalidates gameplay legality without depending on Language.
- Reserve resources/targets at action acceptance; consume once at completion. Stop cancels at a boundary without advancing growth, refunds reserved unused costs exactly once, clears runtime continuations/reservations and retains completed changes. Save uses the same stopped boundary.
- Minimal language: literals, variables, calls, arithmetic/comparisons/boolean operators, conditions, counted loops and while loops. Immutable allowlisted result fields; no host eval/imports/arbitrary member access. Functions and richer collections follow later. Infinite farming programs may yield indefinitely; per-tick work, parse depth, memory and logs are bounded. Avoid a blanket lifetime instruction limit that ends valid continuous automation.
- Use one source tree for core libraries and generated Unity imports. Proposed core target is netstandard2.1 with compatible language/library choices; executable test tooling uses the installed .NET SDK. Verify the actual editor compatibility before locking presentation dependencies.
- Fix numeric limits and error codes in the contract decision. Align runtime source-size limits with persistence validation. Stopped saves store current RNG/scheduler state and versioned world/policy data, never execution stacks or active actions.

## Scope boundary

Start with one tractor and one crop, a small bounded map and depot. Fuel/cargo make tractors meaningfully different from drones. Do not build a complete campaign, fleet coordination, all crop types, equipment catalog or platform release in this milestone. Campaign/sandbox infrastructure must share rules and differ through capability/objective policy.

## Acceptance evidence

- A real program drives the tractor through interpreter, gateway and domain; no scripted demo bypasses these modules.
- Deterministic action timing and accounting; same inputs/version/seed produce the same authoritative results.
- Pause freezes world/runtime, Step advances one tick, and Stop stays responsive for an action-free infinite loop.
- Bounded malformed/adversarial programs produce diagnostics without host execution or unbounded work/allocation.
- Campaign gates and sandbox capabilities work with identical safety limits.
- Save/load restores stopped state and invalid input cannot partially replace the live farm.
- Reviewer names the exact accepted SHA and checks performed. Unity/platform checks that cannot run remain explicitly incomplete.

## Current evidence

Git origin is configured and the bootstrap architecture commit is pushed. Seven worktrees exist; all worker/reviewer reports have arrived. The return-report collector is committed on Chief's branch and has reviewer approval with minor follow-up findings. .NET SDK 10.0.401 is installed. Unity Hub3.22.2 and Apple Silicon Unity6000.3.26f1 are installed; a fresh batchmode project was created successfully with exit0. Core assembly import and game rendering are separate acceptance checks.
