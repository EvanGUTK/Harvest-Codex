# Presentation spike

Open Workspace.unity in Unity 6000.3.26f1 and enter Play. Until an Application adapter calls HarvestWorkspace.Bind(IHarvestSessionView), the editor remains editable but controls are disabled and the viewport explicitly awaits authoritative data. No sample farm or successful execution is fabricated.

The approved presentation-local interface is in SessionViewState.cs. Application mapping must expose actual status and bounded text, immutable world cell views, selected actor details and source location. Coordinates are east/north and screen y is reversed only for rendering. Draft belongs to the session; PresentationPreferences are global and currently in-memory. No audio is implemented. Reduced motion is immediate snapshot rendering for now; there are no animations in either setting.

Narrow windows use Code/Farm tabs with controls and diagnostics retained. Tab leaves the named draft control. Native IMGUI focus styling is provisional; contrast, keyboard traversal, screen readers and actual desktop layout need rendered acceptance. This scaffold does not claim complete accessibility or gameplay acceptance.

Run `dotnet run --project tests/Application/PresentationChecks.csproj` for control-state/snapshot checks. Batchmode `-executeMethod Harvest.Presentation.Editor.PresentationSmoke.CreateScene` checks editor compilation and creates the owned scene. It does not verify rendered play, real harvest, or application bindings.

## Chief Bootstrap composition contract

Create `SessionController(ISimulation simulation, IProgramFactory programs, string simulationId, string contentId, long selectedActorId, SessionMode mode = Campaign)` using concrete Domain simulation and `new LanguageProgramFactory()`. IDs are compatibility IDs, not file paths. Create `new ApplicationSessionView(controller, Func<SessionMode,StoppedSessionSnapshot> createSession)` where the factory returns a validated stopped initial session for the requested mode, using the same simulation/content/rules IDs. Campaign/sandbox replacements retain separate worlds/drafts in the adapter. Unsupported modifiers must be rejected.

Find the scene's `HarvestWorkspace` component and call its public `Bind(IHarvestSessionView)` hook with that adapter. Workspace.Update calls ApplicationSessionView.Pump(Time.unscaledDeltaTime); Bootstrap must NOT pump again. Bind on the Unity main thread before gameplay input. SessionController and the view adapter are synchronous, single-thread consumers; no callbacks mutate world during a tick. Scene path: Assets/Harvest/Presentation/Workspace.unity. Bootstrap remains Chief-owned and can inject/compose without modifying Presentation.

For saving, external wiring calls controller.CaptureStopped() and passes the immutable payload to the Persistence adapter, then storage; no save button is implemented until that adapter is integrated. Loading validates the complete DTO before controller.RestoreStopped(snapshot). A rejected Domain restore preserves the current session. Stop/save clears continuations and keeps world tick.

Actual binding maps ripe/growing/soil and depot labels from world data, tractor heading/position/resources/pending action from authoritative snapshots, and source diagnostics from Language. Current graphics are text/primitives with immediate state updates. Audio and final contrast/rendered accessibility acceptance remain deferred.
