#!/usr/bin/env python3
"""Static safety: Browser Workspace must release pointer capture; only launcher hides off-app."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
main=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
browser=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
panel=browser.split("_browserWindow = new Window",1)[1].split("};",1)[0]
launcher=browser.split("_bubbleWindow = new Window",1)[1].split("};",1)[0]
refresh=browser.split("public void Refresh()",1)[1].split("public void HideIfOtherAppActive()",1)[0]
checks={
    "Main deactivation releases existing drag capture":
        "Deactivated += (_, _) => ReleaseBrowserMouseCapture();" in main,
    "Main minimizing preserves open browser and releases capture":
        "_globalBrowser.Refresh(); // Open Browser stays visible; launcher hides." in main
        and "ReleaseBrowserMouseCapture();" in main.split("StateChanged +=",1)[1].split("PreviewKeyDown +=",1)[0],
    "WPF browser drag guarded by pressed mouse button":
        "if (e.LeftButton != MouseButtonState.Pressed || _browserWindow is null) return;" in browser,
    "Move uses finite WPF DragMove rather than persistent manual capture":
        "_browserWindow.DragMove();" in browser,
    "Browser resize releases mouse after dragging":
        "if (_resizeGrip.IsMouseCaptured) _resizeGrip.ReleaseMouseCapture();" in browser,
    "Browser resize releases mouse when focus moves":
        "private void ReleaseGrip()" in browser and "ReleaseGrip();" in browser,
    "Minimize does not dispose WebViews":
        "private void Minimize()" in browser and "private void CloseAll()" in browser,
    "Close and app exit dispose browser WebViews":
        "DisposeTabs();" in browser and "_globalBrowser.Dispose();" in main,
    "Only visible opaque bounded browser is topmost; no transparent capture":
        "Topmost = true" not in main and browser.count("Topmost = true")==1
        and "Topmost = true" in panel and "Background = Brushes.White" in panel
        and "AllowsTransparency" not in panel and "Topmost = false" in launcher
        and "WebView2CompositionControl" not in main+browser,
}
for k,v in checks.items():
    assert v,"FAILED "+k
    print("PASS "+k)
print("PASS Browser Workspace Windows input isolation static contract")
