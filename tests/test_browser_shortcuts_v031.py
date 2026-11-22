#!/usr/bin/env python3
"""Static safety regression for BIG BROTHER Desktop v0.3.2 browser workspace.

Checks the browser-specific source contract; Windows UI acceptance is separate.
"""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
shell = (root / "src/MainWindow.cs").read_text(encoding="utf-8")
store = (root / "src/SettingsStore.cs").read_text(encoding="utf-8")
project = (root / "src/BBDesktop.csproj").read_text(encoding="utf-8")
checks = {
    "WebView2 updates the address on route changes": "web.SourceChanged += (_, _) => SyncBrowserAddressFromWeb();" in shell,
    "URL text includes chat fragments": "_browserAddress.Text = current.AbsoluteUri;" in shell,
    "Save button moved next to Go": 'Grid.SetColumn(saveUrl, 5);' in shell
        and 'addressRow.Children.Add(saveUrl);' in shell,
    "No Save/Rename controls in shortcut row": 'var star = MakeButton(' not in shell
        and 'var rename = MakeButton(' not in shell
        and 'var remove = MakeButton("×"' not in shell,
    "Existing saved addresses shown as clickable buttons": 'var button = MakeButton(name, "#e3f0fe"' in shell
        and '_shortcutButtons.Children.Add(button);' in shell,
    "No ComboBox for saved websites": 'ComboBox _savedWebsites' not in shell,
    "Shortcut row scrolls horizontally": 'HorizontalScrollBarVisibility = ScrollBarVisibility.Auto' in shell,
    "Triple-click renames without prematurely navigating": 'if (pendingClicks >= 3)' in shell
        and 'await NavigateBrowserAsync(item.Url);' in shell
        and 'TimeSpan.FromMilliseconds(650)' in shell,
    "Shortcut has right-click rename/remove menu": 'button.ContextMenu = menu;' in shell
        and 'RemoveBookmark(item);' in shell,
    "Full Telegram chat URL is saved ONLY on explicit Save": 'var url = current.AbsoluteUri;' in shell
        and 'var liveUrl = _browserView.CoreWebView2?.Source;' in shell,
    "Auto-persisted browser restore does not save tokens": 'SettingsStore.RememberableUrl(current.AbsoluteUri)' in shell,
    "Previous saved URL list remains compatible": 'public List<string> Bookmarks { get; set; } = new();' in store,
    "Shortcut names remain in user profile": 'public Dictionary<string, string> BookmarkTitles { get; set; } = new();' in store,
    "URL bar collapse remains supported": 'ApplyAddressBarVisibility(_preferences.BrowserAddressBarVisible);' in shell,
    "Ghost click isolation architecture unchanged": 'private readonly WebView2 _workspaceView = new();' in shell
        and 'private readonly WebView2 _browserView = new();' in shell
        and 'WebView2CompositionControl' not in shell,
    "Next release version": '<Version>0.3.2</Version>' in project,
}
for label, passed in checks.items():
    assert passed, "FAILED " + label
    print("PASS " + label)
print("PASS desktop browser workspace regression")
