#nullable enable
using UnityEngine;

namespace Harvest.Presentation
{
    public sealed class HarvestWorkspace : MonoBehaviour
    {
        private IHarvestSessionView? session;
        private string unboundDraft = "";
        private string draftError = "";
        private readonly PresentationPreferences preferences = new PresentationPreferences();
        private Vector2 editorScroll, diagnosticScroll, consoleScroll, referenceScroll, recoveryScroll;
        private int narrowTab;
        private bool showReference;
        public void Bind(IHarvestSessionView value) { session = value; }
        private void Update() { if (session is ApplicationSessionView bound) bound.Pump(Time.unscaledDeltaTime); }
        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab && GUI.GetNameOfFocusedControl() == "Program draft")
            { GUI.FocusControl(null); Event.current.Use(); }
            var state = session?.Read() ?? SessionViewState.Unbound;
            GUI.skin.label.fontSize = GUI.skin.button.fontSize = GUI.skin.textArea.fontSize =
                Mathf.RoundToInt(16 * preferences.TextScale);
            GUI.backgroundColor = new Color(.96f, .93f, .86f);
            GUILayout.BeginArea(new Rect(12, 12, Screen.width - 24, Screen.height - 24));
            GUILayout.Label("HARVEST CODEX | " + (state.IsBound ? state.Status.ToString() : "Session binding pending") + " | Tick " + state.Tick);
            GUILayout.BeginHorizontal();
            Button("Run / Resume", state.CanRun, () => session!.Run());
            Button("Pause", state.CanPause, () => session!.Pause());
            Button("Step: one tick", state.CanStep, () => session!.Step());
            Button("Stop", state.CanStop, () => session!.Stop());
            GUILayout.EndHorizontal();
            StorageControls();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode: " + state.Mode);
            Button("Campaign", state.CanSelectMode, () => session!.SelectMode(FarmMode.Campaign));
            Button("Sandbox", state.CanSelectMode, () => session!.SelectMode(FarmMode.Sandbox));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Screen.width >= 1050) showReference = GUILayout.Toggle(showReference, "Reference");
            preferences.ShowGrid = GUILayout.Toggle(preferences.ShowGrid, "Grid");
            preferences.ReducedMotion = GUILayout.Toggle(preferences.ReducedMotion, "Reduced motion");
            GUILayout.Label("Text");
            preferences.TextScale = GUILayout.HorizontalSlider(preferences.TextScale, 1, 1.5f, GUILayout.Width(90));
            GUILayout.EndHorizontal();
            bool narrow = Screen.width < 1050;
            if (narrow) narrowTab = GUILayout.Toolbar(narrowTab, new[] { "Code", "Farm", "Reference" });
            GUILayout.BeginHorizontal();
            if ((!narrow && !showReference) || (narrow && narrowTab == 0)) Editor(state);
            if ((!narrow && showReference) || (narrow && narrowTab == 2)) Reference();
            if (!narrow || narrowTab == 1) Farm(state);
            if (!narrow) Inspector(state);
            GUILayout.EndHorizontal();
            if (narrow) Inspector(state);
            GUILayout.Label("Diagnostics (line / column supplied by runtime)");
            diagnosticScroll = GUILayout.BeginScrollView(diagnosticScroll, GUILayout.Height(75));
            if (draftError.Length > 0) GUILayout.Label(draftError);
            GUILayout.Label(state.Diagnostics);
            GUILayout.EndScrollView();
            GUILayout.Label("Event console");
            consoleScroll = GUILayout.BeginScrollView(consoleScroll, GUILayout.Height(70));
            GUILayout.Label(state.Console);
            GUILayout.EndScrollView();
            GUILayout.Label("Step advances a simulation tick, not a source statement. Tab leaves the editor; no run shortcuts intercept typing.");
            GUILayout.EndArea();
        }
        private void StorageControls()
        {
            if (!(session is ISessionStorageView storageView))
            { GUILayout.Label("Storage unavailable in this session."); return; }
            var state = storageView.ReadStorage();
            GUILayout.BeginHorizontal();
            Button("Save (stops)", state.CanSave, storageView.Save);
            Button("Load farm…", state.CanLoad, storageView.RequestLoad);
            GUILayout.EndHorizontal();
            GUILayout.Label("Storage: " + state.Code + " | " + state.Message);
            if (state.Confirmation.Length > 0)
            {
                GUILayout.Label(state.Confirmation);
                GUILayout.BeginHorizontal();
                Button("Confirm replacement", state.CanConfirm, storageView.ConfirmReplacement);
                Button("Cancel", true, storageView.CancelReplacement);
                GUILayout.EndHorizontal();
            }
            if (state.RecoveryChoices.Count > 0)
            {
                GUILayout.Label("Recovery choices (selection requires confirmation; no automatic recovery)");
                recoveryScroll = GUILayout.BeginScrollView(recoveryScroll, GUILayout.Height(90));
                foreach (var choice in state.RecoveryChoices)
                    Button("Recover: " + choice.Label, state.CanLoad, () => storageView.RequestRecovery(choice.Id));
                GUILayout.EndScrollView();
            }
        }
        private void Reference()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
            GUILayout.Label("Programming reference | currently unlocked capabilities");
            referenceScroll = GUILayout.BeginScrollView(referenceScroll, GUILayout.MinHeight(180));
            if (session is IProgrammingReferenceView reference)
                foreach (var entry in reference.ReadReference())
                {
                    GUILayout.Label(entry.Title);
                    GUILayout.Label(entry.Description, new GUIStyle(GUI.skin.label) { wordWrap = true });
                    // Read-only text: reference content never replaces or executes the draft.
                    GUILayout.Label(entry.Example, GUI.skin.textArea);
                    GUILayout.Space(12);
                }
            else GUILayout.Label("Reference becomes available when the session is bound.");
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
        private static void Button(string title, bool enabled, System.Action action)
        {
            bool previous = GUI.enabled; GUI.enabled = enabled;
            if (GUILayout.Button(title)) action();
            GUI.enabled = previous;
        }
        private void Editor(SessionViewState state)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
            GUILayout.Label("Program draft (changes apply on next Run)");
            GUILayout.Label("Active source: " + state.ActiveSource);
            editorScroll = GUILayout.BeginScrollView(editorScroll);
            var draft = session?.Draft ?? unboundDraft;
            GUI.SetNextControlName("Program draft");
            var edited = GUILayout.TextArea(draft, GUILayout.ExpandHeight(true), GUILayout.MinHeight(180));
            if (edited != draft)
                DraftEdit.TryApply(edited, value => { if (session != null) session.Draft = value; else unboundDraft = value; }, out draftError);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
        private void Farm(SessionViewState state)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
            GUILayout.Label("Farm | east → north ↑");
            var area = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (state.Width == 0 || state.Height == 0) GUI.Label(area, "Awaiting authoritative world snapshot");
            else
            {
                float size = Mathf.Min(area.width / state.Width, area.height / state.Height);
                foreach (var cell in state.Cells)
                {
                    var rect = new Rect(area.x + cell.X * size, area.y + (state.Height - 1 - cell.Y) * size, size, size);
                    if (preferences.ShowGrid) GUI.Box(rect, cell.Label); else GUI.Label(rect, cell.Label);
                }
                var tractor = state.Tractor;
                if (tractor != null)
                {
                    var rect = new Rect(area.x + tractor.X * size, area.y + (state.Height - 1 - tractor.Y) * size, size, size);
                    GUI.Box(rect, "T" + tractor.Id + "\n" + tractor.Heading);
                }
            }
            GUILayout.EndVertical();
        }
        private static void Inspector(SessionViewState state)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.MaxWidth(260));
            GUILayout.Label("Selected tractor");
            var tractor = state.Tractor;
            GUILayout.Label(tractor == null ? "No tractor snapshot" : "Tractor " + tractor.Id + "\nPosition " + tractor.X + ", " + tractor.Y + "\nHeading " + tractor.Heading + "\n" + tractor.Details);
            GUILayout.Label("Objective / modifiers\n" + state.Objective);
            GUILayout.EndVertical();
        }
    }
}
