#nullable enable
using UnityEngine;

namespace Harvest.Presentation
{
    public sealed class HarvestWorkspace : MonoBehaviour
    {
        private IHarvestSessionView? session;
        private string unboundDraft = "", draftError = "";
        private readonly PresentationPreferences preferences = new PresentationPreferences();
        private Vector2 editorScroll, diagnosticScroll, consoleScroll, referenceScroll, recoveryScroll, inspectorScroll, bodyScroll;
        private int activeTab;
        private bool showReference, wasTabbed;
        public void Bind(IHarvestSessionView value) { session = value; }
        private void Update() { if (session is ApplicationSessionView bound) bound.Pump(Time.unscaledDeltaTime); }
        private static Rect RectFor(float x, float y, float width, float height) => new Rect(x,y,width,height);
        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab && GUI.GetNameOfFocusedControl() == "Program draft")
            { GUI.FocusControl(null); Event.current.Use(); }
            var state = session?.Read() ?? SessionViewState.Unbound;
            var layout = new WorkspaceLayout(Screen.width, Screen.height, preferences.TextScale);
            if (layout.Tabbed && !wasTabbed) activeTab = showReference ? 2 : 0;
            if (!layout.Tabbed && wasTabbed) showReference = activeTab == 2;
            if (layout.HasInspector && activeTab == 3) activeTab = 0;
            wasTabbed = layout.Tabbed;
            GUI.skin.label.fontSize = GUI.skin.button.fontSize = GUI.skin.textArea.fontSize = Mathf.RoundToInt(16 * preferences.TextScale);
            GUI.backgroundColor = new Color(.96f, .93f, .86f);
            GUILayout.BeginArea(RectFor(12,12,Screen.width-24,layout.HeaderHeight));
            GUILayout.Label("HARVEST CODEX | " + (state.IsBound ? state.Status.ToString() : "Session binding pending") + " | Tick " + state.Tick, GUILayout.Height(layout.LineHeight));
            GUILayout.BeginHorizontal();
            Button("Run / Resume", state.CanRun, () => session!.Run(), layout.LineHeight);
            Button("Pause", state.CanPause, () => session!.Pause(), layout.LineHeight);
            Button("Step: one tick", state.CanStep, () => session!.Step(), layout.LineHeight);
            Button("Stop", state.CanStop, () => session!.Stop(), layout.LineHeight);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (session is ISessionStorageView storage)
            {
                var saved = storage.ReadStorage();
                Button("Save (stops)", saved.CanSave, storage.Save, layout.LineHeight);
                Button("Load farm…", saved.CanLoad, storage.RequestLoad, layout.LineHeight);
            }
            else GUILayout.Label("Storage unavailable in this session.", GUILayout.Height(layout.LineHeight));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Only the body scrolls: execution/save/load remain reachable even with long recovery messages.
            GUILayout.BeginArea(RectFor(12,layout.BodyTop,Screen.width-24,layout.BodyHeight));
            bodyScroll = GUILayout.BeginScrollView(bodyScroll, false, true);
            GUILayout.BeginVertical(GUILayout.Width(layout.ContentWidth));
            StorageDetails(layout);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode: " + state.Mode);
            Button("Campaign", state.CanSelectMode, () => session!.SelectMode(FarmMode.Campaign));
            Button("Sandbox", state.CanSelectMode, () => session!.SelectMode(FarmMode.Sandbox));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (!layout.Tabbed) showReference = GUILayout.Toggle(showReference, "Reference");
            preferences.ShowGrid = GUILayout.Toggle(preferences.ShowGrid, "Grid");
            preferences.ReducedMotion = GUILayout.Toggle(preferences.ReducedMotion, "Reduced motion");
            GUILayout.Label("Text");
            preferences.TextScale = GUILayout.HorizontalSlider(preferences.TextScale, 1, 1.5f, GUILayout.Width(90));
            GUILayout.EndHorizontal();
            if (layout.Tabbed)
            {
                var names = layout.HasInspector ? new[] { "Code", "Farm", "Reference" } : new[] { "Code", "Farm", "Reference", "Tractor" };
                activeTab = GUILayout.Toolbar(Mathf.Min(activeTab,names.Length-1),names,GUILayout.Height(layout.LineHeight));
            }
            DrawPanels(state, layout);
            GUILayout.Label("Diagnostics (line / column supplied by runtime)");
            diagnosticScroll = GUILayout.BeginScrollView(diagnosticScroll, GUILayout.Height(80 * preferences.TextScale));
            if (draftError.Length > 0) Wrapped(draftError);
            Wrapped(state.Diagnostics);
            GUILayout.EndScrollView();
            GUILayout.Label("Event console");
            consoleScroll = GUILayout.BeginScrollView(consoleScroll, GUILayout.Height(90 * preferences.TextScale));
            Wrapped(state.Console);
            GUILayout.EndScrollView();
            Wrapped("Step advances a simulation tick, not a source statement. Tab leaves the editor; no run shortcuts intercept typing.");
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        private void DrawPanels(SessionViewState state, WorkspaceLayout layout)
        {
            // Keep panels in the body's normal layout flow, after storage/settings/tabs.
            // Fixed widths prevent reference content from competing with sibling panes;
            // each pane's own scroll view clips long content within the allocated height.
            var panelStyle = new GUIStyle(GUI.skin.box) { margin = new RectOffset(0,0,0,0) };
            GUILayout.BeginHorizontal(GUILayout.Width(layout.ContentWidth),GUILayout.Height(layout.PanelHeight));
            if (!layout.Tabbed)
            {
                GUILayout.BeginVertical(panelStyle,GUILayout.Width(layout.EditorWidth),GUILayout.Height(layout.PanelHeight));
                if (showReference) Reference(); else Editor(state);
                GUILayout.EndVertical();
                GUILayout.Space(WorkspaceLayout.Gap);
                GUILayout.BeginVertical(panelStyle,GUILayout.Width(layout.FarmWidth),GUILayout.Height(layout.PanelHeight));
                Farm(state);
                GUILayout.EndVertical();
                GUILayout.Space(WorkspaceLayout.Gap);
                GUILayout.BeginVertical(panelStyle,GUILayout.Width(layout.InspectorWidth),GUILayout.Height(layout.PanelHeight));
                Inspector(state); GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical(panelStyle,GUILayout.Width(layout.TabContentWidth),GUILayout.Height(layout.PanelHeight));
                switch (activeTab) { case 1: Farm(state); break; case 2: Reference(); break; case 3: Inspector(state); break; default: Editor(state); break; }
                GUILayout.EndVertical();
                if (layout.HasInspector)
                {
                    GUILayout.Space(WorkspaceLayout.Gap);
                    GUILayout.BeginVertical(panelStyle,GUILayout.Width(layout.InspectorWidth),GUILayout.Height(layout.PanelHeight));
                    Inspector(state); GUILayout.EndVertical();
                }
            }
            GUILayout.EndHorizontal();
        }
        private static void Wrapped(string text) => GUILayout.Label(text,new GUIStyle(GUI.skin.label) { wordWrap=true });
        private void StorageDetails(WorkspaceLayout layout)
        {
            if (!(session is ISessionStorageView storageView)) return;
            var state=storageView.ReadStorage();
            Wrapped("Storage: " + state.Code + " | " + state.Message);
            if (state.Confirmation.Length>0)
            {
                Wrapped(state.Confirmation);
                GUILayout.BeginHorizontal();
                Button("Confirm replacement",state.CanConfirm,storageView.ConfirmReplacement,layout.LineHeight);
                Button("Cancel",true,storageView.CancelReplacement,layout.LineHeight);
                GUILayout.EndHorizontal();
            }
            if (state.RecoveryChoices.Count>0)
            {
                Wrapped("Recovery choices (selection requires confirmation; no automatic recovery)");
                recoveryScroll=GUILayout.BeginScrollView(recoveryScroll,GUILayout.Height(100*preferences.TextScale));
                foreach (var choice in state.RecoveryChoices)
                {
                    bool previous=GUI.enabled; GUI.enabled=state.CanLoad;
                    if (GUILayout.Button("Recover: " + choice.Label,new GUIStyle(GUI.skin.button) { wordWrap=true })) storageView.RequestRecovery(choice.Id);
                    GUI.enabled=previous;
                }
                GUILayout.EndScrollView();
            }
        }
        private void Reference()
        {
            Wrapped("Programming reference | currently unlocked capabilities");
            referenceScroll=GUILayout.BeginScrollView(referenceScroll);
            if (session is IProgrammingReferenceView reference)
                foreach (var entry in reference.ReadReference())
                {
                    Wrapped(entry.Title); Wrapped(entry.Description);
                    GUILayout.Label(entry.Example,new GUIStyle(GUI.skin.textArea) { wordWrap=false });
                    GUILayout.Space(12);
                }
            else Wrapped("Reference becomes available when the session is bound.");
            GUILayout.EndScrollView();
        }
        private static void Button(string title,bool enabled,System.Action action,float height=0)
        {
            bool previous=GUI.enabled; GUI.enabled=enabled;
            bool clicked=height>0 ? GUILayout.Button(title,GUILayout.Height(height)) : GUILayout.Button(title);
            if (clicked) action(); GUI.enabled=previous;
        }
        private void Editor(SessionViewState state)
        {
            Wrapped("Program draft (changes apply on next Run)"); Wrapped("Active source: " + state.ActiveSource);
            editorScroll=GUILayout.BeginScrollView(editorScroll);
            var draft=session?.Draft ?? unboundDraft;
            GUI.SetNextControlName("Program draft");
            var edited=GUILayout.TextArea(draft,new GUIStyle(GUI.skin.textArea) { wordWrap=false },GUILayout.ExpandHeight(true));
            if (edited!=draft) DraftEdit.TryApply(edited,value => { if (session!=null) session.Draft=value; else unboundDraft=value; },out draftError);
            GUILayout.EndScrollView();
        }
        private void Farm(SessionViewState state)
        {
            Wrapped("Farm | east → north ↑");
            var area=GUILayoutUtility.GetRect(0,0,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            if (state.Width==0 || state.Height==0) GUI.Label(area,"Awaiting authoritative world snapshot");
            else
            {
                float size=Mathf.Min(area.width/state.Width,area.height/state.Height);
                foreach (var cell in state.Cells)
                {
                    var rect=RectFor(area.x+cell.X*size,area.y+(state.Height-1-cell.Y)*size,size,size);
                    if (preferences.ShowGrid) GUI.Box(rect,cell.Label); else GUI.Label(rect,cell.Label);
                }
                var tractor=state.Tractor;
                if (tractor!=null) GUI.Box(RectFor(area.x+tractor.X*size,area.y+(state.Height-1-tractor.Y)*size,size,size),"T"+tractor.Id+"\n"+tractor.Heading);
            }
        }
        private void Inspector(SessionViewState state)
        {
            inspectorScroll=GUILayout.BeginScrollView(inspectorScroll);
            Wrapped("Selected tractor");
            var tractor=state.Tractor;
            Wrapped(tractor==null ? "No tractor snapshot" : "Tractor " + tractor.Id + "\nPosition " + tractor.X + ", " + tractor.Y + "\nHeading " + tractor.Heading + "\n" + tractor.Details);
            Wrapped("Objective / modifiers\n"+state.Objective);
            GUILayout.EndScrollView();
        }
    }
}
