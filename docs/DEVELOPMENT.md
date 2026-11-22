# BIG BROTHER Desktop — Development and release plan

## Architecture

A native WPF/WebView2 workstation loads **locally installed** frontend snapshots from 32 existing accounting repositories. Dashboard and its iframe modules share a synthetic HTTPS origin: https://app.bigbrother.test. This host is reserved for the installed app; WebView2 maps it to the web/ installation folder. No Chrome and no GitHub Pages fetches are needed to obtain frontend modules.

The bundler reads config/modules.lock.json and fetches each exact commit during the Windows BUILD, rewrites known first-party GitHub Pages links to the local origin, then packages frontend files. Production Cloud/PWA repositories are not modified.

## Backend

Existing Supabase Cloud remains the system of record. App UI can start from local assets without GitHub/network, but live Cloud data and most transactions still require the internet. Some third-party libraries and assets may still load from CDNs; the build report lists external hosts, and the team must audit/vendor remaining runtime dependencies.

**Important:** Changing from github.io origin to app.bigbrother.test can affect browser storage, OAuth redirects, CORS, and sessions. Do not assume authentication works until verified. The Desktop app has its own persistent account session profile. No privileged JavaScript bridge is exposed.

## Installer / updates

Velopack builds a true Windows Setup.exe, creates shortcuts and supports an HTTPS update feed. The updater is user-consented; it never automatically restarts when somebody is completing an invoice.

- Package ID: BigBrother.Desktop.
- Development channel only initially, version 0.2.0.
- HTTPS update feed: disabled until a secure release host is selected (config/update-source.json has empty FeedUrl).
- Once approved, host Velopack releases.dev.json and matching packages on HTTPS object storage, set FeedUrl in a Desktop release, and validate upgrades from one installed version to the next.
- The app is self-contained for .NET, with WebView2 Runtime detected/bootstrapped by installer.
- User preferences and separate native Telegram profile live under Windows LocalAppData/BIG BROTHER/Desktop, outside the upgrade folder.

The existing Windows Invoice Workstation remains separate and functional; this new app does not overwrite its files or browser profile.

## Acceptance gates before any staff use

1. Install Setup.exe, check both shortcuts and uninstall behavior, restart PC and verify launch.
2. Disconnect internet and confirm interface loads without any GitHub Pages network requests.
3. Dashboard/sidebar: test every category, nested route, refresh highlight and browser Back.
4. Sign in to existing account and verify permissions, sign-out, token refresh and customer data.
5. Validate Invoice Generator and history, A/R and clearing, stock/COGS, expenses, purchase, staff/admin, bank and daily cash with read-only checks first.
6. Printing, file upload, photo camera access, PDF export, save/share, Telegram browser minimize/restore and login persistence.
7. Identify remaining third-party CDN requests in bundle-report.json, plus any hardcoded Cloud URLs the static rewriter cannot transform.
8. Never create dummy production transactions, alter database schemas or release to staff without explicit approval.
9. Install a second dev release from a configured update feed; test retained browser profiles and rollback.

## Security

This GitHub repository is currently PUBLIC. Do not add passwords, service-role keys, staff/customer/bank data, or production diagnostic records. Release signing and update feed trust must be established before stable distribution.

## CI

.github/workflows/windows-desktop-dev.yml builds and uploads a DEVELOPMENT installer artifact; it does not deploy a website, publish stable releases, run migrations or write accounting data.

## GitHub auto-updates (development channel)

In Desktop 0.2.4+, the app uses `new UpdateManager(new GithubSource("https://github.com/angsokhey11-cloud/big-brother-desktop", null, prerelease: true))`. The app checks once on startup after loading the Dashboard. Update discovery is quiet if none is available or Github is unreachable. It asks before downloading, shows download progress, and **requires a second explicit confirmation before restart**. `SetAutoApplyOnStartup(false)` prevents hidden auto-install of a downloaded package.

Development package publication is controlled by the `desktop-release` branch and the separate `publish-desktop-github-dev.yml` workflow. It builds pinned frontend files and publishes the Velopack `releases.dev.json`, full/delta `.nupkg` and installer as a GitHub **prerelease**. Normal source changes on `main` only compile and upload CI artifacts, without being offered automatically to installed users.

The current updater is **public development releases only**, not a stable staff release and not cryptographically code-signed. Public GitHub anonymously rate-limits update checks; one check per app launch minimizes this. Never store GitHub private access tokens in the Desktop app. Do not promote to stable until code signing, end-to-end permissions/database/CORS tests, release access controls and a rollback exercise are complete.

Versions before 0.2.4 can be upgraded once with `tools/Bootstrap-GitHub-Updates.ps1`: it downloads the versioned package directly from GitHub Releases, checks the asset SHA256 and NUSPEC identity, and calls the already-installed Velopack updater without uninstalling. Subsequent updates are handled by the app.

## Desktop updater smoke-test milestone v0.2.7

When the approved GitHub development prerelease v0.2.7 is published, use a machine currently running v0.2.6 to test the **actual in-app** flow:

1. With the Desktop open and no unsaved accounting work, close and reopen it.
2. Verify the Windows titlebar shows v0.2.6 and a new version v0.2.7 prompt appears.
3. Choose No to confirm postponement does not install silently. Use Alt+Space → Check BIG BROTHER Desktop Updates to invoke it again.
4. Choose Yes to download (delta update when available). Confirm progress and version label.
5. On the install/restart prompt, choose No once, then close/reopen the app. It must detect v0.2.7 already downloaded, not fetch the same package a second time.
6. Confirm install/restart only after finishing all unfinished invoices. Verify the new titlebar is v0.2.7, Dashboard is still local, and the Telegram browser remains signed in if previously authenticated.
7. Confirm GitHub outage leaves the already installed Dashboard usable. This test does **not** create production accounting records.

The application has no extra custom navigation header, and the floating Telegram browser remains visible only within Invoice Generator.
