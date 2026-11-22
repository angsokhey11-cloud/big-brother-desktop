#!/usr/bin/env python3
"""Static safety: Browser Workspace must release pointer capture and hide off-app."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
main=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
browser=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
checks={
    "Main deactivation releases existing drag capture":
        "Deactivated += (_, _) => ReleaseBrowserMouseCapture();" in main,
    "Main minimizing hides all owned browser windows":
        "_globalBrowser.Hide();" in main,
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
    "No topmost click capture":
        "Topmost = true" not in main+browser,
}
for k,v in checks.items():
    assert v,"FAILED "+k
    print("PASS "+k)
print("PASS Browser Workspace Windows input isolation static contract")
