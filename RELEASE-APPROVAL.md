# Approved Desktop Development Update

APPROVED_VERSION=0.3.29

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

## Approved v0.3.6 — Browser identity, branded Home and collapsible shortcuts

- Approved BIG BROTHER Desktop Browser appearance: replace square blue target badge with an unboxed professional white vector globe in the sidebar brand header and a "Browser" caption below, using a fully transparent WPF click target that preserves the small non-topmost HWND window and system-wide mouse isolation.
- Replace duplicate Home shortcut tiles with a locally rendered BIG BROTHER dairy-distribution Home: branded welcome hero, native vector milk-bottle/warehouse/delivery illustration and brief business/browser information. Retain the exact existing saved website/chat shortcuts only in the top strip, without hard-coded live accounting numbers or new API calls.
- Add an explicit "Shortcuts ▴ / Shortcuts ▾" control immediately before the horizontal saved shortcuts strip. Collapse/expand changes only visibility and row height, never deletes bookmarks, names, or WebView2 profile. Persist BrowserShortcutsExpanded=true by default in backwards-compatible workstation settings.
- Keep existing URL collapse toggle, tabs, browser Close-vs-Minimize semantics, full explicit Telegram saved URLs, multi-tab WebView2 profile reuse and global Dashboard availability.
- No Cloud/PWA, Local Brain, Supabase, invoice, bank, stock, A/R, backend, or accounting-rule changes. No full-desktop, composition, or topmost overlay was introduced.
- Full Windows development CI run **45012927350** passed: browser Home and launcher regression, shortcuts collapse and persistence checks, native ghost-screen focus safety checks, Sales Support navigation regression, warning-as-error .NET 10 build, bundled accounting frontend snapshots, and Windows Velopack development installer artifact upload.
- Approved for **internal development prerelease only**. Real Windows UI acceptance and ghost-screen sanity testing still required.

## Approved v0.3.7 — Windows display name and Browser shortcut UX

- User requested clean Windows app name: BIG BROTHER Desktop, without “(Development)” on Windows Start-menu/installed shortcuts and the displayed installer title. Update both development and release packaging workflows to use --packTitle "BIG BROTHER Desktop" and the GitHub release name without the suffix.
- Crucial: Keep Velopack package ID BigBrother.Desktop, channel dev, versioned dev prerelease and consent-based updater unchanged, so this is an in-place update, not a separate app or staff production release. Main native window title remains BIG BROTHER Desktop • v0.3.7.
- Fix Browser Shortcuts collapse: move Shortcuts ▴/▾ to the always-visible tab bar next to New Tab. When collapsed, shrink the shortcut row to exactly zero height and hide the scroller, returning the freed space to the browser page; do not delete saved URLs, names, or login data.
- Remove the visible horizontal shortcut scrollbar but retain scrolling through PreviewMouseWheel over the shortcut row, mapping the normal mouse wheel to horizontal offset.
- Support a standard left-button hold and drag/drop gesture to rearrange saved website and Telegram shortcuts, including the optional default Telegram Web button. Persist ordered shortcut keys in local workstation settings without changing the saved bookmark URL list, names, or full chat-specific addresses. Keep single-click open, triple-click rename, and right-click rename/remove. Drag payload carries no private URL and avoids manual global pointer capture.
- Defer WPF button redraw until after the native drag has ended, protecting the previously fixed Windows ghost-click issue.
- Move the unboxed white globe Browser launcher down into the reserved right edge of the dark sidebar below BIG BROTHER branding, preventing visual/click overlap while retaining its full 78x77 native click target. Preserve non-topmost bounded HWND-hosted WebView2 and off-app focus behavior.
- No Cloud/PWA, Local Brain, Supabase, bank, inventory, invoice, A/R, financial logic or migration change.
- Full Windows CI **45014130995** passed: cleaned desktop-name/pack identity assertions, global Browser and shortcut UX regressions, standard WebView2 ghost-screen and mouse-safety tests, Sales Support navigation, warnings-as-errors Windows compile, 32 pinned accounting frontends and Windows Velopack installer artifact upload.
- This version remains an internal consent-controlled dev-channel prerelease only; real-Windows acceptance for launcher placement, wheel scroll and drag/drop remains necessary.

## Approved v0.3.8 — Transparent text-only Dashboard branding and Browser launcher restoration

- The user's v0.3.7 screenshot showed the globe Browser launcher below BIG BROTHER, which looked awkward. The Dashboard's built-in .brand-home button had width:100% and a blue-tinted :hover background, making its hit area extend across the logo card instead of the displayed BIG BROTHER name/subtitle.
- Add a narrowly scoped, desktop-only CSS override to the already installed native desktop workspace stylesheet: remove .brand painted backgrounds/hover, constrain .brand-home width to visible content with room reserved for the Browser globe, retain the existing onclick Dashboard navigation when the name/subtitle is clicked. Do not modify the Cloud/PWA branding, sidebar routes or accounting code.
- Restore the unboxed white Browser globe and label to the top-right of the Dashboard logo header (origin x=182, y=25 relative to the main window), with a slightly narrower 66px native transparent clickable window to eliminate text overlap. Keep non-topmost HWND-hosted WebView2, off-app focus hiding and ghost-click prevention.
- All v0.3.7 features remain: clean BIG BROTHER Desktop Windows display title without “(Development)”, persisted shortcut order, wheel scrolling, zero-height Shortcut collapse, full Telegram saved URLs and global multi-tab Browser.
- Verified Windows development CI run **45015339851** passed the new logo hit-area regression, Browser focus/shortcuts/tabs regressions, Sales Support navigation, warnings-as-errors .NET build, pinned frontend bundling, and Velopack installer artifact upload.
- Approve for internal consent-based GitHub development prerelease only. User-PC confirmation remains needed for native click boundaries and visual placement. Production/staff approval not granted; no Supabase, Cloud/PWA or Local Brain changes.

## Approved v0.3.9 — Browser globe follows collapsed Dashboard sidebar

- User-confirmed v0.3.8 bug: when collapsing the installed Desktop Dashboard sidebar (body.bb-sidebar-collapsed / .sidebar display:none), the separate native Browser globe stayed at the previous screen coordinates and floated over Invoice Generator/accounting workspace, potentially intercepting invoice form clicks.
- Desktop native host now observes the trusted top-level installed Dashboard shell's actual sidebar visibility via an injected one-way MutationObserver (body class, initial DOM load, resize). It emits only bbDesktopSidebarCollapsedV1:0/1 through WebView2; C# accepts only a string from exact app.bigbrother.test/big-brother-dashboard/index.html source and exposes no privileged host objects, accounting values or database commands.
- Native GlobalBrowserWorkspace hides the entire owned globe HWND immediately on sidebar collapse and keeps it hidden through Refresh, window focus changes and browser minimize/Close. When expanding the sidebar, the launcher reappears in its original brand-header corner. An already-open Browser window remains open with its active tabs, shortcuts, Telegram login and browser profile unaffected.
- All v0.3.8 Browser features remain: independent multi-tab browser, branded Home, full explicit Telegram chat URL bookmarks, shortcut row mouse wheel/drag reorder, collapse toggle and BIG BROTHER logo's limited text hit area.
- No Cloud/PWA, Local Brain, Supabase, financial records, invoice, inventory, A/R, bank or accounting logic modifications. Preserve the standard non-topmost HWND WebView2 ghost-click architecture.
- Full development Windows CI run **45016566573** passed: trusted sidebar collapse signaling regression, shortcut, branding and ghost-screen tests, Sales Support navigation, warnings-as-errors .NET 10 compile, bundled accounting frontend snapshots, and Velopack Windows installer artifact upload.
- Approved for internal consent-controlled GitHub development prerelease only. User PC acceptance required for collapsed/expanded navigation and Windows click isolation; staff production rollout not authorized.

## Approved v0.3.10 — Retire PWA Invoice Browser and restore native Browser access

- User observed the obsolete PWA Invoice Generator Browser icon flashing before desktop injection hid it and then reported the native Browser launcher was missing after v0.3.9 sidebar-collapse handling.
- Remove the obsolete Invoice Generator in-page browser script injection and delete its unused invoice-browser-bubble-v1.js / .css from the Invoice Generator source. Pin the cleaned frontend revision a152e243d05dd6bdcb0b4e34fe2b103d9fdcec6a in the desktop's 32-module build, preserving the rest of Invoice Generator accounting behavior.
- Fix native sidebar signal to use only the explicit bb-sidebar-collapsed body class. Never treat a temporarily missing/hidden sidebar element during navigation as a collapsed menu.
- Keep the native Browser globe hidden when the user intentionally collapses the sidebar, but restore access through the Windows system menu: Alt + Space -> Open BIG BROTHER Browser. This opens the existing multi-tab GlobalBrowserWorkspace, preserving live tabs, saved Telegram chats, private device login/profile, and all browser preferences.
- No topmost/transparent full-screen overlay and no reintroduction of the native WebView2 ghost-click issue. No Supabase, Cloud Dashboard route, Local Brain, stock, bank, A/R, invoice financial rule or schema changes.
- Windows development CI run **45018024705** passed: all 32 pinned frontend snapshots, explicit absent-legacy-PWA-browser bundle assertions, native menu and sidebar checks, Sales Support customer navigation, ghost-click/focus regressions, .NET warnings-as-errors compile, and Velopack installer packaging.
- Approve consent-driven internal development prerelease only. Real PC test must verify Alt+Space browser access when sidebar collapsed and no old globe flash in Invoice Generator.

## Approved v0.3.11 — Batch invoice restore and collapsed-sidebar browser dock

- User requested identical fixes for BIG BROTHER PWA and the installed Windows Desktop App.
- Invoice Generator restores a previously selected valid Batch using the existing change handler so its authoritative Supabase Batch invoice-number RPC runs on reopen; Direct Sale continues with the normal sequence. No invoice-number, stock, payment or selling-date validation is bypassed.
- Collapsing the desktop Dashboard sidebar reserves 430px on the right for the native floating Browser when the Invoice Generator is selected and the viewport is at least 1440px wide; unrelated modules and narrow/mobile layouts remain full width.
- Pin Invoice Generator d4bd888868f5e479072f616efda8ac7778d2cc9d and Dashboard 5237c1bf86811fbba704c7aa2c339392eb796ece; keep the other 30 module SHAs fixed. The bundler recognizes Sales Support return-navigation protection already present in the updated Dashboard without duplicating it.
- Development Windows CI run **45112630579** passed: all 32 module snapshots, navigation/browser/ghost-click regression tests, invoice resume/dock guard, .NET 10 compilation and Velopack installer packaging.
- Publish only to the consent-controlled internal GitHub development prerelease channel. Do not automatically install or restart, migrate Supabase, edit accounting data, or authorize staff production rollout. Real Windows acceptance testing remains necessary.

## Approved v0.3.12 — Clean one-row native browser toolbar and scrolling tabs

- User-approved compact Windows Browser UI: remove visible “BIG BROTHER Browser • drag to move” title; retain an accessible left-hand native drag grip without a topmost or fullscreen overlay.
- Put Shortcuts and + New Tab on the blue title toolbar beside the URL visibility control, minimize and Close buttons. The tab-only second row reclaims its former action-button width.
- Hide the native tab strip's horizontal scrollbar; support regular mouse-wheel horizontal scrolling when tabs overflow. Automatically bring the selected tab into view after switching or opening it. Keep existing shortcut strip's mouse-wheel scrolling, drag reordering and persisted settings.
- No Cloud/PWA, Local Brain, Supabase, accounting data, invoice-number, batch, sales, inventory, A/R, bank, or other business-logic changes. Keep previous 32 pinned accounting frontend module versions exactly as v0.3.11.
- Windows development CI **45114332486** passed bundled modules, legacy browser/mouse-capture safeguards, new compact toolbar regression, .NET 10 warnings-as-errors compilation and Velopack installer packaging.
- Approved for consent-controlled internal Desktop development prerelease only. No automatic installation or restart; production/staff approval requires real Windows acceptance testing.

## Approved v0.3.13 — System Alert title and branded Main Server download progress

- Replace the default Chromium JavaScript dialog heading (“app.bigbrother.test says”) only for the locally installed accounting WebView with an owner-bounded native **System Alert!** dialog. Preserve alert, confirm, prompt, and beforeunload decisions using WebView2 ScriptDialogOpening and a deferral. Use Website Alert for untrusted origins, without exposing privileged browser/host objects.
- Brand the existing BIG BROTHER Desktop in-app updater and the lightweight fresh-PC online setup wizard with **BIG BROTHER Main Server** wording rather than showing raw GitHub progress labels to accounting users.
- No backend hosting migration: the update feed still fetches approved public GitHub Releases and the online installer still verifies the GitHub SHA-256 release asset digest. Do not weaken checksum checks, authentication, release approval or consent requirements. Do not expose secrets or modify accounting business rules.
- Existing 32 pinned accounting frontend modules, stock, invoice, banking, A/R, customer and Local Brain state remain unchanged.
- Windows CI **45133752644** passed all module snapshots, new System Alert/update branding guard, native browser focus safeguards, warnings-as-errors .NET compilation, and Velopack packaging. Online setup CI **45133752816** passed.
- Internal user-consent development prerelease only; real Windows acceptance for alert OK/Cancel/typed prompt, browser focus, update and new-PC installation remains required before staff rollout.

## Approved v0.3.14 — Restore Telegram Manager embedded admin functions in Desktop

- User reported white blank Telegram Manager screen and “This page could not be embedded” when opening Customer Bank Directory in installed Windows app.
- Root cause: the Notification Center app.js constructs approved tool iframe URLs from TOOL_ORIGIN='https://angsokhey11-cloud.github.io' without a trailing slash. The desktop bundler's general first-party rewrite only matches URLs with trailing slashes; the app's actual synthetic origin is https://app.bigbrother.test, with GitHub Pages blocked for locally packaged accounting modules. Cross-origin iframe inspection fails and the manager's existing safety fallback clears the frame.
- Desktop-only build-time exact-marker patch changes this single TOOL_ORIGIN to https://app.bigbrother.test for bundled big-brother-notification-center/app.js. It retains every explicit TOOL_PATHS allow-listed route, origin validation, stylesheet, existing admin permissions, backend and financial business rules; Cloud/PWA repo stays untouched.
- New regression guard validates pinned local Customer Bank Directory, Bank Assistant Setup, and Telegram tools, retained same-origin check and a fail-closed build-time marker guard.
- Keep all 32 accounting module pinned revisions unchanged from v0.3.13. No database, transactions, customer balance, bank register, local brain or profile changes.
- Windows CI build **45153134142** passed 32-module bundling, new Telegram Manager local-origin regression, native UI guards, .NET 10 compilation and Velopack installer packaging.
- Approved for consent-controlled internal Desktop development prerelease only. Real Windows verification of Telegram Manager tool loading, return navigation, login, and permission enforcement is still required before staff rollout.

## Approved v0.3.15 — Reject shared merchant STAND IDs in Bank Review

- In a Cambodian KHQR notification, STAND is a reused merchant terminal identifier, not an individual transaction reference. The correct transaction reference is the bank's printed Hash (or an explicit valid reference).
- Bank Assistant Supabase Edge Function version 75 was separately deployed and verified against the exact provided Khmer notification. It keeps the Hash distinct from STAND, refuses to reuse the STAND as the transaction ID, and treats ambiguous numeric KHQR references as needing human review.
- Repin only big-brother-notification-center at 3923939f778d26d3af42b3991f52d93ea1765e8b in the Desktop's 32 pinned accounting frontends. The Cloud review UI and installed Desktop review screens now flag long numeric KHQR references and explain that STAND must not be registered.
- All other 31 frontend revision pins, Supabase data/schema, financial logic, bank balances, payments, A/R, warehouse, customer records and Telegram user settings remain unchanged.
- Historic payment PAY-3402 uses the supplied shared STAND 0001037221 as its transaction ID; this update does NOT change historical payments, and the true reference must be independently verified before any correction.
- Windows development CI **45164619712** passed the newly bundled KHQR reference guard, 32-module checks, native browser/ghost-click safeguards, .NET compilation, Velopack Windows installer packaging.
- This remains an internal consent-controlled development prerelease. Real Windows bank-review UI acceptance and finance-team validation are required before staff rollout.

## Approved v0.3.16 — Audited admin A/R bank payment reversal

- New admin-only “Reverse A/R Bank Payment” screen under **All Receivable**. Search by payment ID, transaction ID, customer or invoice. Review affected invoices, original amount/payment date, new outstanding. Requires 12-500 character reason, exact payment ID typed, checkbox acknowledgment, browser confirmation.
- NEW deployed server RPC `public.bb_ar_payment_reversal_v1`: only active admin with A/R Edit may search/preview/reverse. Atomic payment+allocation reversal, invoice A/R restoration, original payment/allocations retained in Sales History audit snapshots and additional bank audit log, concurrency token verification, and bank-reference release. No sale or stock reversal.
- Version 1 handles **eligible direct bank A/R payments** only. Refuses cash payments, registered bank wallets/outlet shares, existing registrations/duplicates, linked deposits/refunds/requests, closed collection, returned invoices, multiple distinct references and inconsistent/duplicate allocations. No silent changes to other payments.
- Tested read-only preview with authorized admin context. Executed a complete rehearsal reversal inside one SQL transaction followed by `ROLLBACK`, then independently verified the real payment and invoice and audit history were completely unchanged.
- Only `big-brother-ar` frontend pin changes to `00df73a52c26c64e3381a88b38db83470d999700`. Other 31 accounting module revisions unchanged; no accounting records changed during installation. The server RPC is deployed separately to BIG BROTHER DEV Supabase, not packaged in the web bundle.
- Windows dev CI **45180380213** passed 32 pinned frontends, Telegram Manager routing, existing browser stability checks, new audited A/R reversal bundle test, warnings-as-errors .NET compilation, Velopack packaging.
- Internal development prerelease only; real Desktop admin review and reversal acceptance required before company rollout.


## Approved v0.3.17 — Native Desktop update truck

- User approved matching the Mobile truck animation for the internal Windows development channel. Native WPF owner-bounded non-topmost animation during checks and downloads, real download percentage, immediate Close and five-second completed-check dismissal. Preserve Velopack package identity, local update support, explicit download and restart decisions, and existing accounting frontend pins.
- Windows development CI **45352193236** passed on exact main commit `af633eb35239a595528611756aadcb935ca931cf` including regression guards, compilation and development installer packaging. Release branch retains existing approval history; only this approval file differs from tested main source. Actual Windows update and UI acceptance remain pending.
- Internal development prerelease only; no staff production approval, accounting data changes, or automatic installation/restart.


## Approved v0.3.18 — Native maximized startup

- User approved native WPF maximized startup on the internal Desktop development channel. Windows determines the monitor work area; standard window chrome, taskbar and manual restore remain available. Existing normal dimensions are retained for restore.
- Windows main CI **45353633858** passed on exact commit `eebb58c6db9e17945957be84bf45f00622df6cb3`. Added startup regression guard and aligned version guards. Verified previous release tree differs from its tested main tree only in RELEASE-APPROVAL.md; no accounting frontend changes.
- Internal development prerelease only. Actual monitor and DPI acceptance testing remains pending. No accounting data changes or automatic installation/restart.


## Approved v0.3.19 — Stock allocation prefill date and form responsiveness

- Shared Stock Management source commit 3762557b41f7b6067d05967845551c6c94dca986 resets transaction date to explicitly planned date or current Cambodia date and initializes selling date range to that date. Skips redundant movement/source initialization, retains editable fields, purchased/zero-cost separation, normal stock save and one-time consume; blocks Save during pending consume. No stock or financial records changed.
- Shared PWA GitHub Pages deployment 45369542606 succeeded. Desktop pins only the corrected Stock Management module; Windows development CI 45369615647 passed on exact tested main commit 046222fa91737811348b612b48759e6f06f072bc, including new date and prefill regression checks and packaging. Real-device responsiveness remains to be tested.
- Internal development prerelease only. No automatic installation or restart, production/staff rollout, database migration or accounting data changes.


## Approved v0.3.20 — Exact planned batch transaction date

- Stock Allocation source 75d05c92415b44fdd239286964c4465343241529 stores the explicitly planned issue date in the saved allocation; Stock Management source 01f80d059213639f37ea3c8941a5e14846136b84 loads that date, resets selling date range, and labels legacy creation date as a suggestion for verification. Both PWA deployments succeeded. User confirmed the matching Android implementation works on device on 24-Nov-2026 and requested Windows delivery.
- Windows development CI 45372318625 passed on exact tested main commit cfacfe9133872787464e5975b0102d3525f0902d. Preserve only historical approval file from release branch. Internal development prerelease only; no data migration, no accounting record changes, no forced installation or restart. Windows on-device behavior still requires user confirmation.


## Approved v0.3.21 — Bank Review navigation and Desktop access

- User requested a Bank Review fix and Desktop delivery. Dashboard pin df86824fcc1c17d2be995b58a0a862abc618358b adds the dedicated permission-gated route and removes Bank History fallback navigation. Supabase Edge Function version 13 separately allows the exact installed Desktop origin while preserving the existing admin authorization and Telegram validation. Pending review data was not modified. User confirmed PWA Bank Review loads the existing batch.
- Windows development build #122 succeeded on exact main commit c596d5613b16b1c08803a4acf0a42aa2c0906db4 (user-provided GitHub Actions screenshot). Existing regression guards include the dedicated local route and admin access. Installed Windows end-to-end acceptance remains pending.
- Internal consent-controlled development prerelease only; no forced installation, staff production rollout, database migrations or accounting transaction changes.


## Approved v0.3.22 — Repeat bank directory save and customer search focus

- User confirmed the shared Bank Review fix works in Telegram Mini App and requested Windows delivery. Notification Center pin c636c02e6d5a14fc7a063d239e93815d2693fad2 replaces native directory confirmation popups with an in-page dialog and preserves unrelated transaction cards and selections during saves. Existing registration and directory RPCs, KHQR Hash-versus-STAND safeguards and admin checks remain unchanged.
- Windows development CI run 45551899833 passed on exact main commit defbde09f989c69d7f8b58d16c15228cd34f855e. The previous build failed on an outdated exact-revision assertion; this was corrected without removing safety checks. Installed Windows focus behavior requires user acceptance testing.
- Internal consent-controlled development prerelease only; no automatic installation or restart, staff production rollout, database migrations or accounting transaction changes.


## Approved v0.3.23 — Universal workspace focus recovery

- User requested universal Windows focus recovery for function transitions and native dialog closure, and authorized publishing the update. Shared workspace signal covers all visible same-origin accounting modules and Dashboard Home; temporary blank and external frames are ignored. Existing active input selection, foreground-window safeguards, and accounting logic are preserved.
- Windows development CI run 45555346368 passed on exact main commit efc0a118a7845c87ea5b1a74622e37682bd6eeab. Eight representative module paths and focus guard scenarios also passed simulated testing. Windows real-device acceptance remains pending.
- Internal consent-controlled development prerelease only; no forced installation or restart, staff production rollout, database migration, or accounting transaction changes.


## Approved v0.3.24 — Shared Telegram Browser routing

- User requested that accounting Share to Telegram links use the existing native Browser and explicitly authorized publication. Accounting WebView routes HTTPS Telegram Web and t.me/share/url popup requests to the existing Browser profile and tabs; generic landing requests reuse a live Telegram tab. Existing tabs, print popups, document generation and direct bot sends are preserved. Explicit share links retain their URL. Runtime verification on Windows remains pending.
- Windows development CI run 45578216148 passed on exact main commit 9f876c17eb9210ac0f05ad2c9b6718d52636e5aa.
- Internal consent-controlled development prerelease only; no forced installation or restart, staff production rollout, database migration, or accounting transaction changes.


## Approved v0.3.25 — Native A/R album sharing pilot

- User requested and explicitly approved publication of Windows native A/R album sharing using the existing Browser. The Desktop-only bundle patch preserves the selected summary-first photo order, groups of up to ten, and folder fallback. A restricted local transfer stages photos for an explicitly armed Telegram Web photo picker. The user chooses the recipient, verifies preview order, and sends manually. Non-photo documents retain their existing folder workflow. Android and PWA remain unchanged.
- Windows development CI run 45607242189 passed on exact main commit 318383e50350aed1054fb9037ff8fca40dec1bd3. Simulated ordered-transfer checks passed; actual Telegram Web attachment compatibility and photo order remain unverified on the user's Windows device. Temporary attached files are retained until session cleanup, with stale unlocked session cleanup on subsequent use.
- Internal consent-controlled development prerelease only; no forced installation or restart, staff production rollout, database migration, or accounting transaction changes.


## Approved v0.3.27 — Native PDF sharing and visible album toolbar

- User confirmed v0.3.25 native seven-photo Telegram attachment and correct arrangement on Windows. User requested native A/R PDF sharing and explicitly authorized v0.3.27 publication. This release includes the v0.3.26 toolbar relocation above Telegram and abandoned-album recovery, and extends the restricted native transfer to one authorized PDF (up to 65 MB) with safe filename handling and document picker validation. Existing PDF generation, authorization, save fallback, photo order, and user-controlled Send are retained. No PWA or APK changes.
- Windows development CI run 45610285857 passed on exact main commit a5dac920445583acfdb599aafeea9acbced443eb. Simulated PDF transfer and cancellation checks passed. Actual Telegram PDF attachment and relocated toolbar remain to be tested on the user's Windows device.
- Internal consent-controlled development prerelease only; no forced installation or restart, staff production rollout, database migration, or accounting transaction changes.


## Approved v0.3.29 — Built-in PDF viewer and independent Browser

- User confirmed v0.3.27 native PDF attachment success, requested internal PDF viewing and explicitly requested that the open Browser remain visible while working in other Windows applications. User explicitly authorized v0.3.29 publication.
- Includes v0.3.28 PDF viewer (Telegram preview popups, completed downloaded PDF and native local file picker) and v0.3.29 independent bounded opaque Browser, with only the launcher hidden on app switching. Manual minimize, full close, pointer-capture cleanup, existing photo/PDF sharing, and app shutdown cleanup retained.
- First CI rejected outdated blanket topmost/ownership assertions. Updated legacy regression guards to require exactly one bounded opaque topmost Browser, non-topmost launcher, no composition overlay and no automatic focus stealing. Windows development CI run 45613977604 passed on exact main commit 791bc89779e2daede345276dd4bc145ff5868313. Actual PDF preview, independent visibility and other-app mouse interaction require Windows user testing.
- Internal consent-controlled development prerelease only; no forced install or restart, staff production rollout, database migration, PWA or APK changes.
