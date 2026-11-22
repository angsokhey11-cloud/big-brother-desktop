#!/usr/bin/env python3
"""Static guard for global multi-tab WebView2 HWND and ghost-click isolation."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
main=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
browser=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
checks={
    "Dashboard remains HWND-hosted": "private readonly WebView2 _workspaceView = new();" in main,
    "Each browser tab uses HWND WebView2": "public WebView2 View { get; } = new();" in browser,
    "No composition WebView2 anywhere": "WebView2CompositionControl" not in main+browser,
    "No full-screen input-stealing overlay": "Topmost = true" not in main+browser
        and "Topmost = false" in browser,
    "Browser bubble owned by dashboard": "Owner = _owner, Content = bubble," in browser,
    "Browser native window owned by dashboard": 'Owner = _owner, Title = "BIG BROTHER Browser"' in browser,
    "Native browser auto-hides when focus moves to another app": "public void HideIfOtherAppActive()" in browser,
    "Main window deactivation triggers browser hiding": "new Action(_globalBrowser.HideIfOtherAppActive)" in main,
    "Main window minimize triggers browser hiding": "_globalBrowser.Hide();" in main,
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
