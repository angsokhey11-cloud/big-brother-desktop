#!/usr/bin/env python3
"""Desktop-only Browser launcher follows Dashboard sidebar collapse.

Source-contract regression; the app's native browser and Windows focus still
require real-PC acceptance after build. No accounting or Cloud/PWA changes.
"""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
main=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
browser=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
checks={
    "Dashboard collapse observer injected into installed top-level document":
        "private const string DashboardSidebarVisibilityScript" in main
        and "await web.AddScriptToExecuteOnDocumentCreatedAsync(DashboardSidebarVisibilityScript);" in main
        and "if(window!==window.top" in main,
    "Observer watches body class changes and initial page load":
        "new MutationObserver(report).observe(document.body" in main
        and "attributeFilter:['class']" in main
        and "document.addEventListener('DOMContentLoaded',start" in main,
    "Only explicit sidebar collapse hides Browser launcher":
        "document.body.classList.contains('bb-sidebar-collapsed')" in main
        and "getComputedStyle(side).display==='none'" not in main,
    "Native system menu can reopen Browser without the sidebar":
        '"Open BIG BROTHER Browser"' in main
        and "OpenNativeBrowserMenuCommand" in main
        and "_globalBrowser.OpenFromDesktopMenu" in main
        and "public void OpenFromDesktopMenu()" in browser,
    "Only state, never private accounting data, enters native layer":
        "'bbDesktopSidebarCollapsedV1:'+(hidden?'1':'0')" in main,
    "Message listener validates exact trusted Dashboard origin and page":
        "web.WebMessageReceived += (_, args) =>" in main
        and '"app.bigbrother.test"' in main
        and '"/big-brother-dashboard/index.html"' in main
        and "args.TryGetWebMessageAsString()" in main,
    "No privileged host objects":
        "web.Settings.AreHostObjectsAllowed = false;" in main,
    "Collapsed state explicitly hides launcher HWND":
        "public void SetSidebarCollapsed(bool collapsed)" in browser
        and "if (_bubbleWindow?.IsVisible == true) _bubbleWindow.Hide();" in browser,
    "App refresh cannot redisplay launcher while sidebar is collapsed":
        "if (_sidebarCollapsed)" in browser
        and "if (_bubbleWindow.IsVisible) _bubbleWindow.Hide();" in browser,
    "Expanding restores only minimized launcher":
        "else if (!_panelOpen) Refresh();" in browser,
    "Open browser tabs survive sidebar collapse":
        "SetSidebarCollapsed(bool collapsed)" in browser
        and "DisposeTabs();" not in browser.split("public void SetSidebarCollapsed(bool collapsed)",1)[1].split("public void Refresh()",1)[0],
    "Only bounded opaque Browser may stay on top; launcher remains non-topmost":
        browser.count("Topmost = true") == 1
        and "Topmost = true" in browser.split("_browserWindow = new Window",1)[1].split("};",1)[0]
        and "Topmost = false" in browser.split("_bubbleWindow = new Window",1)[1].split("};",1)[0]
        and "WebView2CompositionControl" not in main+browser,
}
for label, passed in checks.items():
    assert passed, "FAILED "+label
    print("PASS "+label)
print("PASS Browser launcher collapses with native Dashboard sidebar")
