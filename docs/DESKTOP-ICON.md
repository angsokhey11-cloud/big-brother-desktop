# BIG BROTHER Desktop icon (approved)

The user selected the navy/blue/teal **BB + ledger** design for the Windows app.

`assets/desktop-icon-art.webp` is an approved, two-panel image asset at 384×192 pixels:

- Left 192×192: the chosen full-resolution BB identity, with background removed for Windows transparency.
- Right 192×192: a simplified version for tiny Windows taskbar sizes.

`tools/create_icon.py` checks the expected asset SHA-256 and generates a multi-frame PNG-compressed Windows ICO containing **16, 24, 32, 48, 64, 128 and 256** pixel representations. Windows taskbar/title-bar sizes 16–32 use the simplified version; larger sizes use the main chosen design. A single build asset supplies the WPF executable, WinForms online setup, native Windows shortcut and Velopack installer.

Changing the icon must **not** modify any accounting function or production database. The design comes from the user's submitted icon and small-format visual refinement. The asset remains a Desktop-only brand change; the existing Cloud/PWA brand assets are untouched.
