#!/usr/bin/env python3
"""Static guard for global multi-tab WebView2 HWND and ghost-click isolation."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
main=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
browser=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
panel=browser.split("_browserWindow = new Window",1)[1].split("};",1)[0]
launcher=browser.split("_bubbleWindow = new Window",1)[1].split("};",1)[0]
refresh=browser.split("public void Refresh()",1)[1].split("public void HideIfOtherAppActive()",1)[0]
checks={
    "Dashboard remains HWND-hosted": "private readonly WebView2 _workspaceView = new();" in main,
    "Each browser tab uses HWND WebView2": "public WebView2 View { get; } = new();" in browser,
    "No composition WebView2 anywhere": "WebView2CompositionControl" not in main+browser,
    "No always-on-top overlay": "Topmost = true" not in main
        and "Topmost = true" not in browser and "Topmost = false" in panel
        and "Topmost = false" in launcher and "Background = Brushes.White" in panel
        and "AllowsTransparency" not in panel and "Width = _settings.PanelWidth" in panel
        and "Height = _settings.PanelHeight" in panel,
    "Browser bubble owned by dashboard": "Owner = _owner, Content = bubble," in browser,
    "Browser native window owned by dashboard": "Owner = _owner" in panel
        and "ShowActivated = false" in panel,
    "Open browser stays visible when focus moves": refresh.index("WindowState == WindowState.Minimized") < refresh.index("if (_panelOpen)")
        and "_browserWindow.Show();" in refresh and "_browserWindow.Activate()" not in refresh,
    "Main window deactivation refreshes launcher visibility": "new Action(_globalBrowser.HideIfOtherAppActive)" in main,
    "Main minimize follows owner; shutdown hides browser": "_globalBrowser.Refresh(); // Owned Browser follows accounting minimize/restore." in main
        and "_globalBrowser.Hide();" in main.split("Closing +=",1)[1].split("Closed +=",1)[0],
    "Browser never depends on Invoice iframe scope": "private void ApplyInvoiceScope(" not in browser
        and 'SourceInitialized += (_, _) => _globalBrowser.Initialize();' in main,
    "Native browser disposes all tabs when fully closed": "private void DisposeTabs()" in browser
        and "tab.View.Dispose();" in browser,
    "Do not leave old Invoice bubble active": "BuildBrowserPanel();\n        BuildBubble();" not in main,
    "No old Invoice scope listener is registered": "web.WebMessageReceived += OnWorkspaceMessage;" not in main,
    "Cloud-independent local dashboard remains pinned": 'web.SetVirtualHostNameToFolderMapping("app.bigbrother.test"' in main,
}
for k,v in checks.items():
    assert v,"FAILED "+k
    print("PASS "+k)
print("PASS standard WebView2 and global native window focus isolation")
