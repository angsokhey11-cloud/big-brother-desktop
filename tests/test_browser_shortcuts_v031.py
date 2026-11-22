#!/usr/bin/env python3
"""Static safety regression for BIG BROTHER Desktop v0.3.1 browser workspace.

Checks the browser-specific source contract; Windows UI acceptance is separate.
"""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
shell = (root / "src/MainWindow.cs").read_text(encoding="utf-8")
store = (root / "src/SettingsStore.cs").read_text(encoding="utf-8")
project = (root / "src/BBDesktop.csproj").read_text(encoding="utf-8")
checks = {
    "Real-time navigation URL events": "web.SourceChanged += (_, _) => SyncBrowserAddressFromWeb();" in shell
        and "web.NavigationStarting += (_, e) =>" in shell,
    "Live WebView2 URL displayed without stripping query": "_browserAddress.Text = current.AbsoluteUri;" in shell,
    "Saved launch address sanitizes token-bearing URLs": "SettingsStore.RememberableUrl(current.AbsoluteUri)" in shell
        and "return uri.GetLeftPart(UriPartial.Path);" in store,
    "Save targets actual current website": "var liveUrl = _browserView.CoreWebView2?.Source;" in shell,
    "Save opens name editor and never toggles a saved item off": "PromptBookmarkName(\"Save website shortcut\"" in shell
        and "Removed saved website" not in shell,
    "Rename existing shortcut": "private void RenameBookmark()" in shell
        and "PromptBookmarkName(\"Rename website shortcut\"" in shell,
    "Remove existing shortcut explicitly": "private void RemoveBookmark()" in shell,
    "Bookmark selection refresh cannot trigger unexpected navigation": "if (!_refreshingBookmarks && _savedWebsites.SelectedItem is BookmarkItem item)" in shell,
    "Existing bookmark URLs remain backward-compatible": "public List<string> Bookmarks { get; set; } = new();" in store,
    "New bookmark titles persist in profile": "public Dictionary<string, string> BookmarkTitles { get; set; } = new();" in store,
    "URL bar collapse is user-controllable": "toggleAddress.Click += (_, _) =>" in shell
        and "browserAddressGridRow.Height = new GridLength(visible ? 43 : 0);" in shell,
    "URL bar preference persists with default visible": "public bool BrowserAddressBarVisible { get; set; } = true;" in store,
    "WebView2 profiles unchanged": "SettingsStore.BrowserProfile" in shell
        and "SettingsStore.WorkspaceProfile" in shell,
    "Version is next development build": "<Version>0.3.1</Version>" in project,
}
for label, passed in checks.items():
    assert passed, "FAILED " + label
    print("PASS " + label)
print("PASS desktop browser workspace regression")
