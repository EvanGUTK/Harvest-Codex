# Domain contracts

Decision 001 is authoritative. `ISimulation` defines the narrow simulation boundary; concrete `Simulation` follows in the rules implementation commit. DTOs are constructor-validated and defensively copied. World construction orders tiles row-major and actors/depots by stable IDs.

Application owns request allocation. Initialize each actor from `WorldSnapshot.RequestSequences` or `TractorState.LastProcessedRequestId`; `NextRequestId` is checked and throws at sequence exhaustion rather than wrapping. Accepted and gameplay-rejected requests advance the persisted last-processed ID. Duplicate/stale IDs are terminal rejections and cannot reset it. A malformed batch has `BatchStatus.InvalidBatch`, no tick advancement and no processed requests. A gameplay rejection has `SubmissionStatus.Rejected` and an `ActionResult` using shared `Reasons` constants.

`TractorState.Fuel` is available fuel excluding pending reserved cost. `PendingActionView.ReservedQuantity` means wheat yield for harvest, wheat transfer for unload, or fuel taken from depot stock for refuel. Unload retains cargo until completion; refuel removes depot stock at acceptance. Cancellation refunds all unused reservations. Stop snapshots have no pending actions. Runtime wait is not a Domain command.

World snapshots include immutable rules alongside `RulesVersion`, integer growth, xorshift32-v1 RNG state and stable scheduler cursor. `StateFromSeed(0)` is 1. No mechanic draws random numbers yet. All resources are bounded at 1,000,000; grids at 64x64, actors at 16, durations at 10,000 ticks.

Run the package-free executable checks with `dotnet run --project tests/Domain/Harvest.Domain.Tests.csproj`. This harness returns a failing process exit code on any failed assertion; it is not a test-discovery adapter.
