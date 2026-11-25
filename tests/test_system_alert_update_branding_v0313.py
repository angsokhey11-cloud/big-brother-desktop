#!/usr/bin/env python3
"""Desktop v0.3.13 native alerts + branded update progress safety checks."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
main=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
updates=(root/"src/UpdateService.cs").read_text(encoding="utf-8")
setup=(root/"bootstrapper/OnlineSetup.cs").read_text(encoding="utf-8")
checks={
 "Accounting WebView uses custom script dialogs":
  "web.Settings.AreDefaultScriptDialogsEnabled = false;" in main
  and "web.ScriptDialogOpening += OnAccountingScriptDialog;" in main,
 "System Alert header only for trusted accounting pages":
  'var title = local ? "System Alert!" : "Website Alert";' in main
  and 'origin.Host.Equals("app.bigbrother.test"' in main,
 "Alert, Confirm, Prompt, Beforeunload all preserve decisions":
  all(('case CoreWebView2ScriptDialogKind.'+x+':') in main
      for x in ('Alert','Confirm','Prompt','Beforeunload'))
  and "args.ResultText = answer;" in main
  and "deferred.Complete();" in main,
 "No forced acceptance for cancellation": "if (dialog.ShowDialog() != true) return false;" in main,
 "No browser ghost overlay": "WebView2CompositionControl" not in main,
 "Existing in-app update checks are still GitHub verified":
  "new GithubSource(" in updates,
 "New-PC setup still checks approved release and SHA256":
  "https://api.github.com/repos/" in setup
  and "selectedRelease.Sha256" in setup,
 "Progress now calls BIG BROTHER Main Server":
  "Downloading BIG BROTHER update from Main Server" in updates
  and "Connecting to BIG BROTHER Main Server" in setup,
 "Old GitHub progress text removed":
  "from GitHub…" not in updates and "Connecting to GitHub Releases" not in setup
  and " from GitHub..." not in setup
}
for name,ok in checks.items():
 assert ok, "FAILED: "+name
 print("PASS",name)
print("PASS Desktop System Alert and update branding.")

# Workspace focus recovery is shared, local and event-driven.
focus=main.split("private void QueueWorkspaceFocus()",1)[1].split("public MainWindow()",1)[0]
signal=main.split('private const string WorkspaceFocusSignalScript = """',1)[1].split('""";',1)[0]
restore=main.split('private const string RestoreWorkspaceFrameFocusScript = """',1)[1].split('""";',1)[0]
assert "WindowState == WindowState.Minimized" in focus and "!IsActive" in focus and "_accountingDialogDepth != 0" in focus
assert "_workspaceView.Focus();" in focus and "DispatcherPriority.ContextIdle" in focus
assert ".Activate()" not in focus and "DispatcherTimer" not in focus
assert "bbDesktopWorkspaceFocusReadyV2" in signal and "bbDesktopWorkspaceFocusReadyV2" in main
assert "frame.contentWindow.location.href" in signal and "u.origin!==location.origin" in signal
assert "frame.dataset.module" not in signal and "frame.dataset.module" not in restore
assert "workspace.hidden" in signal and "home&&wasHome===false" in signal
assert "active===document.body||active===document.documentElement" in restore
assert main.count("QueueWorkspaceFocus();")==2 and "_accountingDialogDepth--;" in main
print("PASS shared native workspace and dialog focus recovery guards")
