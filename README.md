# BIG BROTHER Desktop

Native Windows accounting workstation for BIG BROTHER's **existing online Supabase system**.

## Project status

**Active development — not approved for live accounting use.** The target is a complete installed Windows application, not a browser shortcut or an Invoice Generator-only test. The existing Cloud/PWA, Android APK, Local Brain V5/V6 and financial database stay unchanged.

## Architecture

- Windows WPF / Microsoft WebView2 desktop shell, including persistent floating Telegram browser.
- Pinned frontend snapshots from the existing BIG BROTHER function repositories, packaged under `web/<repository-name>/`.
- Local WebView2 virtual HTTPS hostname `app.bigbrother.test`, mapped to installed `web/` files — no GitHub Pages request to load the accounting interface.
- **Online Supabase** remains the source of customer, invoice, bank, stock and financial data.
- Velopack Windows installer and update system. Updating application files must never trigger database migrations automatically.

## Ground rules

1. **Never change live accounting repositories, database records, or financial business rules as part of Desktop packaging.**
2. No development/test dummy transactions in the online database.
3. Pin all upstream module revisions before releasing. Audit remote assets and sign-in behavior at the new origin before approving the build.
4. No secret keys, service-role credentials or staff data in this repository, artifacts or logs.
5. Unavailable update services must not stop the installed app from launching; never restart and discard unsaved work.
6. Browser profile and preferences live under the user's local application data and persist across upgrades.

## Build

The Windows GitHub Actions workflow fetches the pinned source revisions in `config/modules.lock.json`, bundles public frontend assets, compiles the WPF workstation, and creates a Velopack **Setup.exe** development installer. No installer is a production release until end-to-end sign-in, route, data, print and payment verification is complete.

See `docs/DEVELOPMENT.md` for architecture, packaging, release safety and testing.

## Development updates via GitHub Releases

From Desktop v0.2.4, the installed app checks the public repository's **approved development prereleases** after opening the Dashboard. It asks before downloading a Velopack update package and asks again before installing/restarting. It does **not** redownload a GitHub Actions ZIP or require Chrome. When suitable delta patches exist, Velopack can download a smaller patch; otherwise it falls back to the full update package.

Release packages are published only from the protected-by-process `desktop-release` branch using `.github/workflows/publish-desktop-github-dev.yml`. Normal `main` development commits are not published as updates. This is a development channel, not staff-approved production software. GitHub outages do not stop locally installed accounting interfaces from opening; online Supabase data still needs connectivity.

Already-installed versions **before v0.2.4** do not yet know how to check GitHub Releases. The one-time `tools/Bootstrap-GitHub-Updates.ps1` can upgrade them from an approved GitHub prerelease directly, preserving user settings (no Setup.exe reinstallation). The bootstrap verifies GitHub release SHA256 and the application package identity before invoking the installed Velopack updater. Only run the script after reviewing it and closing the app.

## Small online Windows installer (fresh installations)

For a new Windows PC, share the single `BIG-BROTHER-Desktop-Online-Setup.exe` from an approved Desktop **GitHub Release**. This is a small standalone Windows Forms launcher (about 67 KB, requiring Windows' built-in .NET Framework 4.5+), **not a ZIP** and **not the 90 MB offline installer**.

Its normal guided flow:
1. Welcome, then choose the **application installation directory** (default `%LOCALAPPDATA%\BigBrother.Desktop`; another local drive such as `D:\Apps\BIG BROTHER Desktop` is allowed).
2. Choose the **local data directory** (default `%LOCALAPPDATA%\BIG BROTHER\Desktop`; another local drive is allowed). Only Desktop preferences, local cached app/browser state, and Telegram/WebView2 login profiles live here. **No cloud accounting data is moved**; Supabase remains the accounting database.
3. Download the approved public development Setup.exe from GitHub Releases, verify its SHA-256 against GitHub's release asset digest, then delegate the installation to Velopack using `--silent --installto "<chosen folder>"`.
4. Store the chosen directories under the **current user's registry** at `HKCU\Software\BIG BROTHER\Desktop`. The installed app reads `DataDirectory` on launch. These preferences persist outside the replaceable Velopack application folder and survive app updates.
5. Finish and optionally launch BIG BROTHER Desktop. It already knows how to check for later approved GitHub development updates.

**Important:** A tiny online installer still downloads the full app on the first installation, so it needs internet access. It avoids sharing or manually extracting a large ZIP. Use the full Velopack Setup.exe from the same release only when an offline installer is required. The lightweight installer is for **fresh installs**; existing users should update using the installed app. No automatic production financial transactions or database migrations are involved.

The application and its installer are currently **unsigned development software**, so a Windows SmartScreen warning may occur. Signing and staff rollout remain separate release gates.
