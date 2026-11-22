# Approved Desktop Development Update

APPROVED_VERSION=0.2.6

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
