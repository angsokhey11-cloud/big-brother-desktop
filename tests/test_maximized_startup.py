from pathlib import Path
root=Path(__file__).resolve().parents[1]
source=(root/"src/MainWindow.cs").read_text(encoding="utf-8")
constructor=source.split("public MainWindow()",1)[1].split("BuildWindow();",1)[0]
assert "WindowState = WindowState.Maximized;" in constructor
assert "WindowStartupLocation = WindowStartupLocation.CenterScreen;" in constructor
assert "WindowStyle = WindowStyle.None" not in constructor
assert "Topmost = true" not in constructor
print("PASS native maximized startup; standard Windows chrome retained")
