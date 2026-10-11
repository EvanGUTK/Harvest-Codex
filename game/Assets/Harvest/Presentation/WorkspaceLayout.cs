using System;
namespace Harvest.Presentation
{
    // Content-independent allocation: paragraphs and source text cannot compete for panel width.
    public sealed class WorkspaceLayout
    {
        public const float Margin = 12, Gap = 8, ScrollbarAllowance = 20;
        public float ContentWidth { get; }
        public float HeaderHeight { get; }
        public float BodyTop => Margin + HeaderHeight + 6;
        public float BodyHeight { get; }
        public float PanelHeight { get; }
        public float EditorWidth { get; }
        public float FarmWidth { get; }
        public float InspectorWidth { get; }
        public float LineHeight { get; }
        public bool Tabbed { get; }
        public bool HasInspector { get; }
        public float TabContentWidth { get; }
        public WorkspaceLayout(float width, float height, float textScale)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0 ||
                float.IsNaN(height) || float.IsInfinity(height) || height <= 0 ||
                float.IsNaN(textScale) || textScale < 1 || textScale > 1.5f) throw new ArgumentOutOfRangeException(nameof(width));
            ContentWidth = Math.Max(0, width - Margin * 2 - ScrollbarAllowance);
            LineHeight = (float)Math.Ceiling(30 * textScale);
            HeaderHeight = Math.Max(0, Math.Min(height - Margin * 2, LineHeight * 3 + 12));
            BodyHeight = Math.Max(0, height - Margin * 2 - HeaderHeight - 6);
            PanelHeight = Math.Max(240 * textScale, Math.Min(440 * textScale, height * .42f));
            float inspectorMinimum = Math.Max(280, 224 * textScale);
            float farmMinimum = 400 * textScale, editorMinimum = 520 * textScale;
            Tabbed = ContentWidth < inspectorMinimum + farmMinimum + editorMinimum + Gap * 2;
            HasInspector = !Tabbed || ContentWidth >= 520 + inspectorMinimum + Gap;
            if (!Tabbed)
            {
                InspectorWidth = inspectorMinimum;
                float flexible = ContentWidth - InspectorWidth - Gap * 2;
                EditorWidth = Math.Max(editorMinimum, Math.Min(flexible * .56f, flexible - farmMinimum));
                FarmWidth = flexible - EditorWidth;
                TabContentWidth = EditorWidth;
            }
            else
            {
                InspectorWidth = HasInspector ? inspectorMinimum : ContentWidth;
                TabContentWidth = HasInspector ? ContentWidth - InspectorWidth - Gap : ContentWidth;
                EditorWidth = FarmWidth = TabContentWidth;
            }
        }
    }
}
