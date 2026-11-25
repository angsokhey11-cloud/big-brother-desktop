#!/usr/bin/env python3
"""v0.3.6: branded Home, transparent globe launcher and global browser lifecycle."""
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
    "Saved shortcuts strip is collapsible and keeps its toggle visible": 
        "SetShortcutVisibility();" in b
        and 'DockPanel.SetDock(shortcutsToggle, Dock.Right);' in b
        and 'new GridLength(expanded ? 43 : 0)' in b
        and "private void SetShortcutVisibility()" in b
        and "BrowserShortcutsExpanded" in b
        and "public bool BrowserShortcutsExpanded { get; set; } = true;" in s,
    "Globe sits in top-right without covering actual brand lettering":
        '_bubbleWindow.Left = origin.X + Math.Min(182' in b
        and '_bubbleWindow.Top = origin.Y + 25;' in b
        and 'Width = 66, Height = 77' in b,
    "Home is branded, not a duplicate bookmark grid":
        "private void BuildBrandedHome()" in b
        and "private static Canvas DairyArtwork()" in b
        and "_homeButtons" not in b,
    "Browser launcher has bare vector globe and text label":
        'var globe = new Canvas { Width = 38, Height = 38 };' in b
        and 'Label("Browser", 11, "#ffffff", true)' in b
        and 'Background = Brushes.Transparent' in b
        and "bubble.Template = template;" in b,
    "No hard-coded live accounting totals":
        "62,871.99" not in b and "659" not in b,
    "No accounting network code changes":
        "Supabase" not in b and "Postgrest" not in b,
}
for k,v in checks.items():
    assert v,"FAILED "+k
    print("PASS "+k)
print("PASS global browser home, tabs and close lifecycle contract")

# Accounting Telegram share uses the existing Browser, not default WebView popups.
route=m.split("private void OnAccountingNewWindowRequested(",1)[1].split("private void QueueTelegramShareDispatch()",1)[0]
assert "web.NewWindowRequested += OnAccountingNewWindowRequested;" in m
assert "TryTelegramShareUri(args.Uri, out var target)" in route and "args.Handled = true;" in route
assert route.index("TryTelegramShareUri") < route.index("args.Handled = true;")
assert "_globalBrowser.OpenTelegramShareAsync(target)" in m
allow=b.split("internal static bool TryTelegramShareUri(",1)[1].split("private static Uri? TabAddress",1)[0]
assert all(x in allow for x in ('Uri.UriSchemeHttps','uri.IsDefaultPort','uri.UserInfo.Length != 0','"web.telegram.org"','"t.me"','"/share/url"'))
share=b.split("public async Task OpenTelegramShareAsync(Uri target)",1)[1].split("private void Open()",1)[0]
assert "OpenFromDesktopMenu();" in share and "Activate(matching);" in share
assert "IsTelegramLanding(safe)" in share and "current.AbsoluteUri, safe.AbsoluteUri" in share
assert "each.IsHome) ?? CreateTab()" in share and "await NavigateAsync(safe.AbsoluteUri)" in share
assert "CloseAll()" not in share and "DisposeTabs()" not in share and "new Window" not in share
assert "No existing tab was overwritten." in share
print("PASS accounting Telegram links share existing Browser session, preserve URL and unrelated popups")

# Native A/R album pilot is limited to installed A/R frames and explicit Telegram photo picker.
album=(root/"src/GlobalBrowserWorkspace.Album.cs").read_text(encoding="utf-8")
transfer=(root/"src/NativeAlbumTransfer.cs").read_text(encoding="utf-8")
patch=(root/"tools/native_ar_album_patch.js").read_text(encoding="utf-8")
bundle=(root/"tools/bundle_web.py").read_text(encoding="utf-8")
assert "_nativeAlbum.Bind(web);" in m and "_nativeAlbum.Dispose();" in m
assert "frame.WebMessageReceived" in transfer and "IsAllowedSource(args.Source)" in transfer
assert 'u.AbsolutePath is "/big-brother-ar/"' in transfer and 'or "/big-brother-ar/your-receivable.html"' in transfer
assert "files.GetArrayLength() is < 1 or > 10" in transfer and "index != t.Index" in transfer and "offset != t.Offset" in transfer
assert "MaxBytes = 65L" in transfer and "FileShare.None" in transfer and "Guid.NewGuid()" in transfer
assert "DOM.setFileInputFiles" in album and "files = arm.Files" in album and "Page.fileChooserOpened" in album
assert "selectMultiple" in album and "Page.getFrameTree" in album and "arm.Web.Source != arm.Address" in album
assert "Page.setInterceptFileChooserDialog" in album and "TimeSpan.FromSeconds(30)" in album
assert "TrySetPreparedAlbum(t.Paths)" in transfer and "OpenTelegramShareAsync(new Uri(TelegramUrl))" in album
assert "for(let i=0;i<files.length;i++)" in patch and "summary:index===0&&i===0" in patch
assert "Folder fallback" in bundle and "patch_desktop_native_ar_album(name, rel.as_posix(), data)" in bundle
print("PASS scoped ordered native A/R photo staging and user-controlled Telegram picker guards")
