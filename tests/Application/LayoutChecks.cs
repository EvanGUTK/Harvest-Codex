using System;
using Harvest.Presentation;
internal static class LayoutChecks
{
    private static void Check(bool value,string message) {if(!value) throw new Exception(message);}
    public static void Run()
    {
        var desktop=new WorkspaceLayout(1280,800,1);
        Check(!desktop.Tabbed && desktop.HasInspector,"1280 default must keep three readable panels");
        Check(desktop.InspectorWidth>=280 && desktop.FarmWidth>=400 && desktop.EditorWidth>=520,"Desktop reference must not squeeze farm/inspector");
        Check(Math.Abs(desktop.EditorWidth+desktop.FarmWidth+desktop.InspectorWidth+2*WorkspaceLayout.Gap-desktop.ContentWidth)<.01,"Three panels exceed content budget");
        var largeText=new WorkspaceLayout(1280,800,1.5f);
        Check(largeText.Tabbed && largeText.HasInspector && largeText.InspectorWidth>=336 && largeText.TabContentWidth>=520,"Large text must keep reserved inspector beside tab content");
        Check(Math.Abs(largeText.TabContentWidth+largeText.InspectorWidth+WorkspaceLayout.Gap-largeText.ContentWidth)<.01,"Tabbed sidebar exceeds width budget");
        foreach(float scale in new[]{1f,1.25f,1.5f})
        {
            var compact=new WorkspaceLayout(800,600,scale);
            Check(compact.Tabbed && !compact.HasInspector,"800x600 needs full-width Code/Farm/Reference/Tractor tabs");
            Check(compact.TabContentWidth==compact.ContentWidth && compact.InspectorWidth==compact.ContentWidth,"Compact inspector must not form an unbounded bottom panel");
            Check(compact.BodyTop+compact.BodyHeight<=600-WorkspaceLayout.Margin+.01,"Scrollable body leaves client area");
            Check(compact.HeaderHeight>=compact.LineHeight*3,"Pinned controls do not fit their header");
        }
        // Exercise transitions and scale budgets; allocation is independent of reference/draft length.
        for(int width=800;width<=1600;width+=20)
            foreach(float scale in new[]{1f,1.25f,1.5f})
            {
                var layout=new WorkspaceLayout(width,600,scale);
                Check(layout.ContentWidth<=width-2*WorkspaceLayout.Margin && layout.BodyHeight>0,"Viewport allocation overflow");
                if(!layout.Tabbed) Check(layout.FarmWidth>=400*scale && layout.InspectorWidth>=Math.Max(280,224*scale),"Width transition sacrifices readable panels");
                else if(layout.HasInspector) Check(layout.TabContentWidth>=520,"Tabbed content too narrow");
            }
        Console.WriteLine("Windowed layout width reservations, font scales, compact tabs and scroll bounds passed");
    }
}
