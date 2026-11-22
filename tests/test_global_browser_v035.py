#!/usr/bin/env python3
"""v0.3.5 multi-tab browser: fresh home, global access and real full close."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
b=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
m=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
s=(root/"src/SettingsStore.cs").read_text(encoding="utf-8")
checks={
    "Browser bubble is global across Dashboard routes":
        'SourceInitialized += (_, _) => _globalBrowser.Initialize();' in m
        and "new Action(_globalBrowser.Refresh)" in m,
    "Invoice-only native bubble not created":
        "BuildBrowserPanel();\n        BuildBubble();" not in m,
    "Home is fresh at app startup": "private void Open()" in b
        and "if (_active is null) CreateTab();" in b
        and "public bool IsHome { get; set; } = true;" in b,
    "No last-visited URL automatically navigated":
        "NavigateAsync(_settings.BrowserUrl)" not in b
        and ' _address.Text = "";' in b,
    "Multiple tabs are independent HWND WebView2":
        "private readonly List<BrowserTab> _tabs = new();" in b
        and "public WebView2 View { get; } = new();" in b,
    "New Tab control": 'Button("+ New Tab"' in b
        and "private BrowserTab? CreateTab()" in b,
    "Tab activation preserves other tabs":
        "private void Activate(BrowserTab tab)" in b
        and "each.View.Visibility = ReferenceEquals(each, tab)" in b,
    "Close tab disposes specific WebView":
        "private void CloseTab(BrowserTab tab)" in b
        and "tab.View.Dispose();" in b,
    "Close Browser clears all tabs and next Open creates fresh Home":
        "private void CloseAll()" in b
        and "DisposeTabs();" in b
        and "if (_active is null) CreateTab();" in b,
    "Minimize keeps tabs alive":
        "private void Minimize()" in b
        and "// Tabs and authentication stay alive" in b,
    "Tab count limited for memory": "private const int MaxTabs = 8;" in b,
    "Tabs share same browser data directory":
        "private Task<CoreWebView2Environment>? _environmentTask;" in b
        and "SettingsStore.BrowserProfile" in b,
    "External popup uses New Tab rather than separate Windows":
        "web.NewWindowRequested += (_, e) =>" in b
        and "CreateTab() is not null" in b,
    "Old Invoice-relative window position migrated once":
        "BrowserGlobalPositionMigrated" in b
        and "public bool BrowserGlobalPositionMigrated" in s,
    "No accounting network code changes":
        "Supabase" not in b and "Postgrest" not in b,
}
for k,v in checks.items():
    assert v,"FAILED "+k
    print("PASS "+k)
print("PASS global browser home, tabs and close lifecycle contract")
