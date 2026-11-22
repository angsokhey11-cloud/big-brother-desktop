#!/usr/bin/env python3
"""Prevent regression of Windows pointer capture escaping BIG BROTHER Desktop.

Static guard for native shell lifecycles, not a substitute for a real device UI
test. No Supabase, database, browser login, or personal data is accessed.
"""
from pathlib import Path

source = (Path(__file__).resolve().parents[1] / "src" / "MainWindow.cs").read_text(encoding="utf-8")

def section(start: str, end: str) -> str:
    assert start in source, f"Missing lifecycle method: {start}"
    return source.split(start, 1)[1].split(end, 1)[0]

for situation, start, end, marker in [
    (
        "invoice iframe closes or changes",
        "private void ApplyInvoiceScope(",
        "private void OnWorkspaceMessage(",
        "if (!valid)\n        {\n            // The Invoice iframe can disappear while the user is dragging or\n            // resizing its browser. Merely clearing _dragging leaves WPF\n            // holding mouse capture and can steal clicks from other apps.\n            ReleaseBrowserMouseCapture();",
    ),
    (
        "browser minimizes",
        "private void MinimizeBrowser()",
        "\n}",
        "ReleaseBrowserMouseCapture();",
    ),
    (
        "drag is released, including outside captured element",
        "private void WirePanelDrag(",
        "private static double Bound(",
        "Mouse.PreviewMouseUpOutsideCapturedElementEvent",
    ),
    (
        "mouse capture always releases even if drag flag cleared",
        "private void EndPanelDrag(",
        "private static double Bound(",
        "if (surface.IsMouseCaptured) surface.ReleaseMouseCapture();",
    ),
]:
    body = section(start, end)
    assert marker in body, f"Missing release guard when {situation}"
    print("PASS", situation)

for marker in (
    "Deactivated += (_, _) => ReleaseBrowserMouseCapture();",
    "if (WindowState == WindowState.Minimized)",
    "Closing += (_, _) =>\n        {\n            ReleaseBrowserMouseCapture();",
    "if (_dragSurface?.IsMouseCaptured == true)",
    "if (_resizeGrip?.IsMouseCaptured == true)",
    "_dragging = surface.CaptureMouse();",
    "grip.DragCompleted += (_, _) =>",
):
    assert marker in source, f"Missing capture-release invariant: {marker}"

assert "Topmost = true" not in source
print("PASS window deactivate, minimize, close and resize release browser capture")
print("PASS no always-on-top Desktop window")
