#!/usr/bin/env python3
"""Desktop system-wide mouse safety regression for native WebView2 hosting.

Checks the installed-shell architecture (does not simulate Windows input).
Real-PC testing is still required to confirm the intermittent ghost-click bug.
"""
from pathlib import Path

code = (Path(__file__).resolve().parents[1] / "src" / "MainWindow.cs").read_text(encoding="utf-8")
checks = {
    "Main Dashboard uses normal HWND WebView2": "private readonly WebView2 _workspaceView = new();",
    "Native Telegram browser uses normal HWND WebView2": "private readonly WebView2 _browserView = new();",
    "No composition screen capture WebView2": "WebView2CompositionControl" not in code,
    "No full-window floating native HWND": "_overlay.IsHitTestVisible = false;",
    "Small bubble is owned by application window": 'Owner = this,\n            Content = _bubble,',
    "Native floating browser is separately bounded": 'Owner = this,\n            Content = _browserPanel,',
    "Window is not forced topmost": "Topmost = false",
    "Browser windows hide after focus moves to another application": "private void HideNativeWindowsIfAnotherAppActive()",
    "Native windows hide on main minimize": 'if (WindowState == WindowState.Minimized)\n            {\n                ReleaseBrowserMouseCapture();\n                HideNativeBrowserWindows();',
    "Native browser only opens in invoice generator": 'if (!_invoiceScopeActive || WindowState == WindowState.Minimized',
    "Native browser preserves instance via window Hide()/Show()": "_browserWindow.Hide();",
    "Main standard WebView2 preserves local Cloud-independent bundled dashboard": 'web.SetVirtualHostNameToFolderMapping("app.bigbrother.test"',
    "No always-on-top window": "Topmost = true" not in code,
    "Native owned browser can be dragged only when mouse button held": "if (e.LeftButton != MouseButtonState.Pressed) { EndPanelDrag(surface); return; }",
}
for label, needle in checks.items():
    passed = needle if isinstance(needle, bool) else needle in code
    assert passed, f"FAILED {label}"
    print(f"PASS {label}")

print("PASS standard HWND-hosted Desktop shell and invoice-only native overlay safety invariants")
