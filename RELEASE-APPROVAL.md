# Approved Desktop Development Update

APPROVED_VERSION=0.3.0

This approval is **for the internal BIG BROTHER Desktop development channel only**.

- Source: exact committed module revisions in `config/modules.lock.json`.
- Distribution: public GitHub Releases **prerelease** with Velopack channel `dev`.
- Installer and update package: `BigBrother.Desktop` application ID.
- Automatic check: the app checks GitHub after it opens; no automatic download or restart without explicit user consent.
- Validation status: compilation and packaging must pass before the release is published. End-to-end business function validation and production use are **not approved**.
- Security: no secret credentials in binary or release; no automatic SQL, database, or accounting data migrations.
- Release workflow runs only from `desktop-release` branch, not ordinary `main` development commits.
- Never republish or overwrite an existing tagged version.

To approve the next version, bump the app/pack version, validate CI, update `APPROVED_VERSION`, then deliberately advance `desktop-release` to that reviewed commit.

## Approved v0.2.5 changes

- Publish the lightweight branded one-file online setup via GitHub Releases.
- Users may choose both the installed application folder and the separate per-user browser/settings data folder.
- The online setup downloads and checksum-verifies the full approved package before delegating to the Velopack installer; existing users continue updating within the app.
- Main accounting frontend revisions remain pinned; no DB writes, SQL migrations or financial logic changes.
- The small setup EXE is unsigned development software and needs Windows .NET Framework and internet access on first install.
- Pre-release source/installer CI and real custom-folder Windows setup tests have passed.

## Approved v0.2.6 change — User-selected Desktop branding

- Replace the native Windows app icon with the user's selected BB financial-ledger artwork (navy, blue and teal).
- Use a simplified variant for 16, 24, and 32px Windows taskbar sizes; use detailed original art for larger icon sizes.
- Keep native WPF titlebar/taskbar, shortcuts, standard Velopack Setup.exe and lightweight Online Setup.exe consistent.
- Verified Windows online-installer build, icon-asset SHA256 integrity check, multi-resolution ICO generation, complete 32-module Desktop build and package succeeded on main.
- No Supabase schema or accounting-data changes and no changes to Cloud/PWA function repositories.
- Development prerelease only; staff production use remains unapproved until full acceptance testing.

## Approved v0.2.7 — updater regression

- Recognizes an already downloaded-but-postponed Desktop update on next launch without downloading it again.
- Shows installed and available version numbers in the new-update prompt; installed app version is visible in normal Windows title bar, without an extra custom header.
- Upgrade remains consent-driven: user approves download and separately approves install/restart after saving unfinished accounting work.
- Full Windows CI passed: 32 pinned accounting module assets, native app compilation, installer packaging and essential files verified (run 44995896855).
- Only updater/desktop shell code changed; no accounting module, bank, sales, A/R, inventory, COGS or database changes.
- Publish on development GitHub prerelease channel for user-controlled acceptance test; **not** a stable staff release.

## Approved v0.2.8 — Your Customer return-navigation bug fix

- Reproduced root cause: Add Customer and Customer Map replaced the Dashboard iframe URL without updating the `data-module` marker, causing the regular Your Customer loader to skip returning to it.
- Desktop-only bundler now sets `frame.dataset.module=ROUTE` in those two adapters before switching their iframe URL.
- Automated Node regression passed on pinned bundled files for both routes and the normal Dashboard same-module guard.
- Windows CI full build **44996994404** passed, including all 32 pinned frontend modules, regression test, native compilation, and Velopack installer packaging.
- No changes to original Cloud/PWA source repositories, accounting database, customer records, permissions, or financial functions.
- Publish to internal development-update channel for real-PC navigation confirmation, not staff-stable release.

## Approved v0.2.9 — Windows input/focus capture safety

- Scope: Desktop native floating browser mouse capture only. No backend, accounting frontend, customer record, Supabase schema, Cloud/PWA, permission, or financial logic changes.
- Fix: Explicitly release drag/resize pointer capture on Invoice Generator exit, browser minimize, application deactivation, Windows minimization, close, Escape, and release outside the captured element.
- Root cause candidate: earlier code could clear `_dragging` without `ReleaseMouseCapture()`, potentially leaving another app's mouse clicks intercepted. Full intermittent real-PC behavior requires acceptance testing to confirm.
- Windows build 44997774478 passed compilation, both frontend/navigation regression and native pointer-capture guard, and full installer packaging.
- Internal GitHub development prerelease only, not yet staff-approved stable production software.

## Approved v0.3.0 — Desktop WebView2 ghost-click isolation test

- Confirmed v0.2.9 mouse capture cleanup did not resolve the user's symptom: other Windows programs cannot be clicked even when the floating Invoice Generator browser was never opened; closing BIG BROTHER Desktop restores normal clicks.
- Microsoft open WebView2 issues #4829 and #5459 describe similar desktop-click obstruction and ghost-layer behavior in WPF WebView2.
- Changed the desktop's always-on Dashboard from WPF WebView2CompositionControl (screen-capture/composition rendering) to standard HWND-hosted WPF WebView2. The Invoice Generator native browser also uses standard WebView2, in small non-topmost owned Windows rather than a full-frame WPF overlay.
- Native bubble/panel exist only within Invoice Generator. Both automatically hide when the main app minimizes, closes, switches away, or another application becomes active. Browser settings/profiles remain in the existing user directory.
- Passed Windows full CI run 44998929882: 32 pinned module packaging, static shell/capture regression guards, navigation regression, .NET 10 warnings-as-errors compilation, self-contained publish and Velopack packaging.
- This is a **diagnostic development build**, not yet user-confirmed. Validate on the user's PC: clicks on Excel/Chrome/desktop icons before Invoice Generator, after minimize/restore, after opening/minimizing floating Telegram browser; ensure Telegram login persists.
- No Cloud/PWA source, Supabase configuration/schema, accounting workflow, invoice, bank, stock or COGS changes. Production staff rollout remains unapproved.
