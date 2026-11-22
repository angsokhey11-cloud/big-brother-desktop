# Approved Desktop Development Update

APPROVED_VERSION=0.3.5

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

## Approved v0.3.1 — Browser Workspace live URL and named shortcuts

- Internal development release for user acceptance testing only, following the verified v0.3.0 ghost-click fix. The standard HWND WebView2 architecture and native window focus isolation stay intact.
- Keep the native address field synchronized with WebView2 navigation, redirects and SPA route changes using SourceChanged and NavigationStarting. The displayed URL remains complete, while persisted session start URLs and bookmark targets deliberately exclude sensitive query strings and fragments.
- Save the current live website through a naming dialog; rename and explicitly remove saved shortcuts. Existing Bookmarks list and saved WebView2 user profiles remain backward-compatible, while shortcut labels persist separately under the same user data directory.
- Add an on/off address bar control in the native blue browser title strip; retain the preference through application restart and upgrades without hiding the saved shortcuts.
- Protect native shortcut dialogs from unexpected owner-window hiding without restoring an always-on-top or full-desktop browser overlay.
- Full Windows development CI run **45000908131** passed: v0.3.1 browser regression, native window focus/ghost-click guards, Sales Support navigation regression, .NET 10 warnings-as-errors compilation, 32 pinned module bundle, Velopack packaging and Setup.exe artifact upload.
- No Cloud/PWA, Local Brain, Supabase schema, bank, sales, inventory, A/R, invoice or other accounting rules changed. Released only to the approved user-controlled GitHub prerelease channel; not staff production software.

## Approved v0.3.2 — Clickable browser shortcuts with full Telegram chat URLs

- Replace the saved-websites dropdown with a horizontally scrollable strip of directly clickable named buttons beside Telegram Web. The original saved bookmark list and labels are retained, and saved items are loaded from the same user profile. New buttons show immediately after pressing Save.
- Move Save to the far right of the browser URL row beside Go. Keep URL collapse/expand setting and live SourceChanged address display.
- A single click on a shortcut opens it; three quick clicks rename the selected shortcut without accidentally navigating on earlier clicks. Right-click provides Rename and Remove actions. Save no longer toggles or deletes an existing shortcut.
- A shortcut explicitly saved by the user now retains the complete current WebView2 URL, including query and fragment, to allow Telegram Web deep links to a particular chat. Sensitive URL parameters are only persisted by the explicit Save action; automatic BrowserUrl session restore remains sanitized to path-only. As full URLs may contain private chat identifiers, profile settings remain local to the device and should only be used on trusted computers.
- Existing pre-v0.3.2 shortcuts remain compatible but cannot recover a chat fragment that previous versions already discarded; re-save specific chat after update.
- Preserve v0.3.0 standard WebView2 HWND ghost-click isolation, Telegram persistent device profile, and all accounting module/backend boundaries. No Cloud/PWA, Local Brain, Supabase, invoice, bank, stock, A/R or accounting schema changes.
- Development Windows CI **45002083544** passed: all static browser and focus safety regressions, Sales Support navigation regression, warnings-as-errors Windows build, 32 pinned frontend bundles, Velopack installer packaging and uploaded dev installer artifact.
- Release only to the consent-driven internal development prerelease channel, not to production staff.

## Approved v0.3.3 — Repeatable Telegram chat shortcuts and removable default

- User reported two v0.3.2 defects after real-PC testing: saved Telegram chat URL buttons worked only once and subsequently reopened Telegram's home page instead of the specific conversation; the permanent Telegram Web button could not be removed.
- For an already-open web document and a saved URL that differs only by hash, use the existing WebView2 document's JavaScript location.hash update, avoiding a full top-level WebView2.Navigate reload of Telegram's SPA and preserving the signed-in session. Cross-site or non-hash navigations continue to use CoreWebView2.Navigate.
- Render Telegram Web as the first optional button under the same direct-open, triple-click rename and right-click Rename/Remove controls as saved shortcuts. Removing that button changes only its persisted visibility, not Telegram Web access, saved chat buttons, or the browser profile; the user may still type or Save Telegram Web again.
- Persist the optional default shortcut's visibility and name in settings while retaining backward compatibility with existing Bookmarks and BookmarkTitles. URL bar Save position, full explicitly saved chat URLs, chat session profile, auto-sanitized BrowserUrl, and URL bar collapse preference remain unchanged.
- Retain native HWND WebView2 and ghost-click safety guards, dialog focus isolation and all accounting UI/financial rules. No Cloud/PWA, Local Brain, Supabase, bank, invoice, A/R or inventory changes.
- Full development Windows CI **45003200797** passed: browser shortcut/SPA static regression, desktop focus/ghost-click safety, Sales Support navigation regression, Windows warnings-as-errors compilation, 32 pinned accounting frontend bundles, and Velopack packaging and installer artifact upload. Real Telegram chat navigation on user PC remains an acceptance test.
- Approved for consent-driven BIG BROTHER Desktop GitHub development prerelease only, not staff production rollout.

## Approved v0.3.4 — Telegram Web A chat bookmark bootstrap routing

- Real-PC report from v0.3.3: selecting a saved chat made its ID appear in the URL for roughly one second, then Telegram restored the old screen. Source inspection of the public Telegram Web A project (Ajaxy/telegram-tt) confirms its routing.ts captures the initial chat fragment at module startup and Main.tsx uses parseLocationHash in an effect keyed on currentUserId, not as a general hashchange handler.
- Fix exact saved Telegram /a/, /k/, or /z/ chat links that share the current document base: use a single script evaluation to history.replaceState the full target URL, followed immediately by window.location.reload() so the target fragment is consumed by Telegram at initialization instead of briefly changing the address bar.
- Other websites and cross-site route changes continue to use WebView2 navigation, and saved chats retain their full URLs only after the user's explicit Save action. Existing shortcut buttons, labels, the optional removable Telegram Web shortcut, popup-free browser profile, and URL-toolbar collapse are preserved.
- Preserve the confirmed standard WebView2 HWND ghost-screen workaround, no topmost overlay or backend/accounting logic changes.
- Full development Windows CI run **45004238898** succeeded: browser regression, ghost-click safety, Sales Support navigation, warnings-as-errors .NET 10 compilation, 32 frontend snapshots, installer and Velopack package artifact.
- This is an internal consent-driven development prerelease. Telegram's real app behavior still requires acceptance testing on the user's signed-in PC; CI cannot exercise private Telegram chat navigation.

## Approved v0.3.5 — Global Browser Workspace V2 with Home and multi-tabs

- Promote the native WebView2 browser from an Invoice Generator-only scope to a small dashboard-wide circle, accessible in Dashboard, Sales, Stock, Sales Support, Receivables, and other main modules. The former Invoice-scoped browser is no longer constructed or activated, and the old dashboard polling hook is no longer registered.
- Use a separate GlobalBrowserWorkspace with bounded, non-topmost, main-window-owned WPF windows and ordinary HWND-hosted WebView2 controls. The native circle is anchored near the upper-right edge of the Dashboard sidebar; the browser can be moved/resized. Keep owner deactivation/minimize logic to prevent ghost-screen or input interception in other Windows programs.
- Start each new browser session on a clean native Home page with existing saved website and Telegram chat shortcuts. Do not automatically navigate to the last opened page; minimizing and reopening the circle preserves the current live tabs.
- Allow up to eight independent tabs with shared existing BrowserProfile cookies/local session. Each tab has its own history and WebView2, plus New Tab, select, and Close Tab controls. Target-blank HTTP(S) links open another tab.
- Add explicit Close Browser action distinct from Minimize: dispose every live WebView2 tab, hide the panel, keep the circle, and open to a fresh Home on next launch. Never wipe browser user profile, saved shortcuts, login cookies or preference data.
- Preserve full explicitly saved chat URLs, rename/remove buttons and URL-bar toggle; prior local preferences remain compatible. Migrate former Invoice-relative browser panel position once to dashboard-relative coordinates.
- Preserve v0.3.0 standard WebView2 ghost-click isolation and the Telegram chat-link reload behavior from v0.3.4. User's existing accounting UI/frontend, bank/stock/receivables rules, Supabase, Cloud/PWA, and Local Brain are not modified.
- Windows CI **45011342805** passed static global browser lifecycle/ghost-click checks, shortcut/Home/tab tests, Sales Support navigation regression, warnings-as-errors .NET 10 compilation, bundling of all 32 pinned accounting frontends, and Velopack Windows installer packaging.
- Consent-controlled **internal development prerelease only**. Real Windows acceptance is required for tab switching, browser full Close, 3rd-party links, login persistence and system-wide mouse safety.
