#!/usr/bin/env python3
"""Browser Workspace v0.3.5 shortcuts, URL and local profile regression."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
browser=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
store=(root/"src/SettingsStore.cs").read_text(encoding="utf-8")
proj=(root/"src/BBDesktop.csproj").read_text(encoding="utf-8")
checks={
    "Live URL tracks SourceChanged": "web.SourceChanged += (_, _) =>" in browser
        and "SyncAddress();" in browser,
    "Displayed full URL includes fragment": "_address.Text = current.AbsoluteUri;" in browser,
    "Save URL at end of address bar": "Grid.SetColumn(save, 5);" in browser,
    "Saved website appears in shortcut row and Home": "_shortcutButtons.Children.Add(button);" in browser
        and "_homeButtons.Children.Add(homeButton);" in browser,
    "Triple-click rename never navigates on first two clicks": "if (clicks >= 3)" in browser
        and "TimeSpan.FromMilliseconds(650)" in browser,
    "Right-click rename and remove": "button.ContextMenu = context;" in browser
        and "RemoveShortcut(item);" in browser,
    "Explicit Save stores exact chat ID and URL": "var url = current.AbsoluteUri;" in browser
        and "var liveUrl = _active?.View.CoreWebView2?.Source;" in browser,
    "Automatic restore remains sanitized": "SettingsStore.RememberableUrl(current.AbsoluteUri)" in browser,
    "Legacy shortcut URLs and names preserved": "public List<string> Bookmarks { get; set; } = new();" in store
        and "public Dictionary<string, string> BookmarkTitles { get; set; } = new();" in store,
    "Telegram default shortcut rename/remove": "_settings.TelegramShortcutVisible = false;" in browser
        and "_settings.TelegramShortcutName = chosen;" in browser,
    "Telegram Web A bootstrap reloading": "window.history.replaceState(window.history.state" in browser
        and "window.location.reload();" in browser,
    "URL bar collapse preserved": "SetAddressVisibility();" in browser
        and "BrowserAddressBarVisible" in browser,
    "Existing persistent WebView2 profile reused": "SettingsStore.BrowserProfile" in browser,
    "Correct development version": "<Version>0.3.5</Version>" in proj,
}
for k,v in checks.items():
    assert v,"FAILED "+k
    print("PASS "+k)
print("PASS browser shortcut and URL state contract")
