#!/usr/bin/env python3
"""Native Desktop Browser is accessible; retired Invoice PWA browser not bundled."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
invoice=root/"build/web/big-brother-invoice-generator"
index=invoice/"index.html"
modules=json.loads((root/"config/modules.lock.json").read_text(encoding="utf-8"))
main=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
browser=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
revision=next(x["sha"] for x in modules["modules"] if x["name"]=="big-brother-invoice-generator")
checks={
    "Pin includes retired legacy Invoice PWA browser removal":
        revision=="a152e243d05dd6bdcb0b4e34fe2b103d9fdcec6a",
    "Bundled Invoice Generator entry exists": index.is_file(),
    "No obsolete browser injection":
        index.is_file() and "invoice-browser-bubble-v1.js" not in index.read_text(encoding="utf-8"),
    "No obsolete PWA browser script bundle": not (invoice/"invoice-browser-bubble-v1.js").exists(),
    "No obsolete PWA browser CSS bundle": not (invoice/"invoice-browser-bubble-v1.css").exists(),
    "Native Browser reachable without Dashboard sidebar":
        '"Open BIG BROTHER Browser"' in main
        and "_globalBrowser.OpenFromDesktopMenu" in main
        and "public void OpenFromDesktopMenu()" in browser,
    "Globe hidden only by actual collapsed state":
        "document.body.classList.contains('bb-sidebar-collapsed')" in main,
    "No native mouse-blocking fullscreen overlay":
        "Topmost = true" not in main+browser
        and "WebView2CompositionControl" not in main+browser,
}
for name,ok in checks.items():
    assert ok,"FAILED "+name
    print("PASS "+name)
print("PASS PWA browser decommission and native access")
