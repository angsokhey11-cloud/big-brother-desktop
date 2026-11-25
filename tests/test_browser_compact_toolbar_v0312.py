#!/usr/bin/env python3
"""Native BIG BROTHER Browser compact toolbar regression, v0.3.12."""
from pathlib import Path
r=Path(__file__).resolve().parents[1]
s=(r/"src/GlobalBrowserWorkspace.cs").read_text(encoding="utf-8")
checks={
 "No redundant browser title": 'Label("BIG BROTHER Browser   •   drag to move"' not in s,
 "Small accessible native drag grip": 'Child = Label("⋮⋮", 19' in s
       and '_browserWindow.DragMove();' in s,
 "Shortcuts on main URL toolbar": s.index('var shortcutsToggle = Button("Shortcuts ▴"')
       < s.index('var tabsRow = new DockPanel'),
 "New Tab on main URL toolbar": s.index('var newTab = Button("+ New Tab"')
       < s.index('var tabsRow = new DockPanel'),
 "URL toggle and minimize/close remain on header": 'var toggle = Button("URL ▴"' in s
       and 'close.Click += (_, _) => CloseAll();' in s
       and 'minimize.Click += (_, _) => Minimize();' in s,
 "Tab scrollbar hidden": 'HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden' in s.split('var tabScroll = new ScrollViewer')[1].split('tabsRow.Children.Add(tabScroll);')[0],
 "Wheel scrolls tab headers only when overflowing": 'tabScroll.PreviewMouseWheel += (_, e) =>' in s
       and 'if (tabScroll.ScrollableWidth <= 0) return;' in s
       and 'tabScroll.ScrollToHorizontalOffset(next);' in s,
 "Active tab auto-scrolls into view": 'header.BringIntoView();' in s,
 "Saved shortcut scrollbar and reorder still present": 'shortcutRow.PreviewMouseWheel' in s
       and 'DragDrop.DoDragDrop(button' in s,
 "URL/shortcuts collapse persisted": 'SetAddressVisibility();' in s
       and 'SetShortcutVisibility();' in s
       and 'SettingsStore.Save(_settings);' in s,
 "No ghost input overlay": "Topmost = true" not in s
       and "Topmost = false" in s.split("_browserWindow = new Window",1)[1].split("};",1)[0]
       and "AllowsTransparency = true" not in s.split("_browserWindow = new Window",1)[1].split("};",1)[0]
       and 'WebView2CompositionControl' not in s
}
for name,ok in checks.items():
 assert ok, 'FAILED '+name
 print('PASS',name)
print('PASS Compact Browser toolbar v0.3.12')
