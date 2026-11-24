#!/usr/bin/env python3
"""Desktop-only logo hit-area and floating Browser isolation."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
shell=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
browser=(root/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
project=(root/"src/BBDesktop.csproj").read_text(encoding="utf-8")
checks={
    "Logo CSS applies only to installed desktop workspace":
        "private const string HideWebInvoiceBubbleScript" in shell
        and "AddScriptToExecuteOnDocumentCreatedAsync(HideWebInvoiceBubbleScript)" in shell,
    "BIG BROTHER brand has no painted card background":
        "'.brand{background:transparent!important;box-shadow:none!important}'" in shell,
    "Logo button hit area limited to text, not entire brand":
        "'width:max-content!important;max-width:calc(100% - 46px)!important;'" in shell,
    "No blue logo background when hovering or focusing":
        "'.brand>.brand-home:hover,.brand>.brand-home:focus{'" in shell
        and "'background:transparent!important;box-shadow:none!important}'" in shell,
    "Browser globe back at upper-right of sidebar":
        "_bubbleWindow.Left = origin.X + Math.Min(182" in browser
        and "_bubbleWindow.Top = origin.Y + 25;" in browser,
    "Native globe no longer covers brand text column":
        "Width = 66, Height = 77" in browser
        and "Content = launcherContent, Width = 64, Height = 73" in browser,
    "Floating Browser uses transparent, non-topmost HWND":
        "Background = Brushes.Transparent, AllowsTransparency = true" in browser
        and "ShowActivated = false, Topmost = false" in browser,
    "Globe and Browser label remain fully clickable":
        "launcherContent.Children.Add(globe);" in browser
        and "launcherContent.Children.Add(browserLabel);" in browser
        and "bubble.Click += (_, _) => Open();" in browser,
    "Correct in-place update version": "<Version>0.3.20</Version>" in project,
}
for title, ok in checks.items():
    assert ok, "FAILED " + title
    print("PASS " + title)
print("PASS desktop logo-button and globe non-overlap contract")
