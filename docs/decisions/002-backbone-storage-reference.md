# Decision 002: player-facing backbone

User accepted the primitive playable view and requested voxel graphics in the architecture, with backbone implementation first. Base: reviewed main `4be6b78`. No new farming commands, save schema, campaign chapters, paid assets or runtime rendering dependencies in this milestone.

## Storage contract and owners

Chief publishes `src/Harvest.Application/StorageContracts.cs` as a pure application interface/immutable outcomes, then Reviewer checks this boundary. Experience owns all other Application/Presentation work. Persistence is explicitly assigned the new `src/Harvest.Composition/SessionStorage.cs` implementation and `tests/Storage`; Chief retains the Composition project file and other Composition files. Composition may depend on Persistence; Application never does. Chief owns Bootstrap wiring and cross-module acceptance checks.

`ISessionStorage.Save()` returns `StorageOutcome`; it captures through SessionController.CaptureStopped, so saving visibly stops execution before disk I/O. Save failure leaves the farm stopped and preserves the existing primary/backup according to the accepted store semantics. `Load()` requires Stopped, validates the current-mode primary before RestoreStopped, and never runs loaded source. An active load fails without stopping or changing the live session. `Recover(id)` also requires Stopped and only accepts a currently offered opaque recovery ID. `InvalidateRecovery()` clears offered choices without changing world/disk.

`SessionStorage(SessionController controller, string rootDirectory, StoppedSessionCodec codec)` implements this interface. Trusted root storage comes from Unity's persistentDataPath; use fixed `campaign` and `sandbox` child directories and one `farm` slot per mode. Build stores through codec.CreateStore with the expected mode. Bound/validate the root at construction; create directories at operation time. Normalize expected access/I/O/save/compatibility failures into a player-readable immutable outcome. Do not swallow unexpected programming errors or expose raw filesystem paths/exception traces in UI.

Load with a valid primary restores it transactionally. Missing/corrupt primary returns a failure or RecoveryAvailable with up to 65 validated backup/temp choices. Never promote automatically. Labels distinguish previous save and interrupted saves; IDs are 32 hexadecimal characters, allocated per offer and mapped privately to candidate paths. Save, Load, successful/attempted Recover, and mode switching invalidate older offers. Recover checks offer mode, re-reads/revalidates selected bytes and restores only after full validation. Replaced/removed/corrupt candidates must fail safely; stale/unknown IDs cannot touch live state. No rollback of valid world changes from before saving's documented Stop.

Experience adds Save (stops), Load, and explicit recovery selection with visible success/failure text. Load/recovery require a stopped session and a clear confirmation that current farm/draft will be replaced. Switching mode clears pending confirmations/offers. ApplicationSessionView accepts optional ISessionStorage, preserving unbound/test usage; controls disable if storage is unavailable. Use an optional presentation storage interface rather than forcing storage methods onto every IHarvestSessionView. Chief injects SessionStorage in Bootstrap from a trusted fixed root; tests can bind temporary roots.

## Programming reference

Runtime owns `ReferenceEntry` and `LanguageReference` in Harvest.Language. Publish `ReferenceEntry.Id/Title/Example/Description/RequiredCapabilities` as bounded immutable values and `LanguageReference.Available(Capabilities)` returning a read-only list of entries whose entire capability set is granted. Entries describe only implemented syntax/APIs; all examples must compile under their advertised requirements. Use the actual compiler/builtin semantics, with source-size and collection bounds. This is content metadata, never a second interpreter or gameplay-rule implementation.

Experience shows a read-only scrollable reference using ModePolicy.For(current mode,current chapter). Locked syntax must not appear as available. Reference must update after real campaign unlock and mode changes, and explain action results, tick-based wait/Step and bounded sandbox execution. No automatic execution or silent replacement of a draft by examples.

## Other agents and graphics

Creative owns `docs/creative/VOXEL_DIRECTION.md`: original block-shaped 3D direction, one grid cell/Unity unit convention, heading/camera mapping, tractor/crop/depot readability, eventual mesh/material/import metadata, and a staged renderer plan. No production asset/music work yet. Simulation owns new `docs/simulation/CROP_CYCLE_PROPOSAL.md`, proposing the next till/plant/water/growth/resource/versioning contract from existing Domain types; proposal only, no Domain/schema changes in this milestone.

Reviewer first checks this contract, then staged contributions and the exact integrated candidate. Chief stays active through handoffs/fixes/integration and advances main only after exact approval. All development launches remain windowed; do not open another player just to report progress.

## Acceptance

Actual farm/draft/chapter/selection/request sequence roundtrip through UI-facing storage survives a fresh controller/service. Campaign and sandbox disk namespaces remain separate. Save while running uses the real stop/cancel/refund boundary; active Load is rejected unchanged. Invalid/future/wrong-mode/corrupt saves and failed I/O cannot partially replace a live session or silently promote recovery. Explicit backup recovery is revalidated; stale/mode-switched/replayed IDs reject. Reference examples compile, filter with actual policy and refresh on progression. Relevant harnesses and Unity scene binding/build pass; user-validated prior controls remain functional. Primitive visuals remain intentional; voxel rendering and audio are future milestones.
