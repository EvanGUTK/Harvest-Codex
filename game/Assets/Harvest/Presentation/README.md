# Presentation spike

Open Workspace.unity in Unity 6000.3.26f1 and enter Play. Until an Application adapter calls HarvestWorkspace.Bind(IHarvestSessionView), the editor remains editable but controls are disabled and the viewport explicitly awaits authoritative data. No sample farm or successful execution is fabricated.

The approved presentation-local interface is in SessionViewState.cs. Application mapping must expose actual status and bounded text, immutable world cell views, selected actor details and source location. Coordinates are east/north and screen y is reversed only for rendering. Draft belongs to the session; PresentationPreferences are global and currently in-memory. No audio is implemented. Reduced motion is immediate snapshot rendering for now; there are no animations in either setting.

Narrow windows use Code/Farm tabs with controls and diagnostics retained. Tab leaves the named draft control. Native IMGUI focus styling is provisional; contrast, keyboard traversal, screen readers and actual desktop layout need rendered acceptance. This scaffold does not claim complete accessibility or gameplay acceptance.

Run `dotnet run --project tests/Application/PresentationChecks.csproj` for control-state/snapshot checks. Batchmode `-executeMethod Harvest.Presentation.Editor.PresentationSmoke.CreateScene` checks editor compilation and creates the owned scene. It does not verify rendered play, real harvest, or application bindings.
