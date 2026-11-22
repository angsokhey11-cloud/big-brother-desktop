# BIG BROTHER Desktop 0.2.2 — internal development

- Dedicated desktop app repository.
- Reuse proven native Windows WebView2 floating Telegram browser.
- Bundle approved frontend revisions of 32 accounting modules under installed web assets.
- Open locally installed Dashboard rather than GitHub Pages.
- First Windows Velopack Setup.exe with consent-based updater.
- Source and dependency inventory included with each CI build.

Not a stable production release; full Cloud authentication and accounting workflow validation still required.

- Edge-to-edge accounting Dashboard: no duplicate Windows navigation header.
- Native Telegram browser shown only inside the Invoice Generator iframe; preserved when switching functions.
- Branded multi-resolution BIG BROTHER app icon for Windows taskbar, Setup.exe, and shortcuts.

## Desktop v0.2.6 — approved BB accounting icon

- Replace the old Windows BB emblem with the user-approved navy/blue/teal interlocking BB accounting icon.
- Use high-contrast simplified icon frames at 16–32px and full approved BB artwork at larger sizes.
- Apply the icon to the native EXE, Windows taskbar/title bar and shortcuts, large offline Setup.exe, and lightweight online installer EXE.
- No changes to any invoice, stock, cash, banking, receivable, or database code.

## BIG BROTHER Desktop v0.2.7 — Updater regression test

- **Fix**: Previously downloaded updates are recognized on subsequent launches and can be installed without re-downloading.
- **Clarity**: Update prompt shows installed and new version numbers; native Windows title bar displays current Desktop version and download/check status.
- **Safe operation**: Updates still require explicit download approval and explicit install/restart confirmation.
- **Scope**: Desktop shell/updater only. No change to the 32 pinned accounting modules, Supabase schema, stock, banking, A/R, invoices, or accounting values.
- **Verification**: v0.2.6 → v0.2.7 will be tested on the real installed app. Production staff release is not yet approved.
