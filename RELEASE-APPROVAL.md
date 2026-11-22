# Approved Desktop Development Update

APPROVED_VERSION=0.2.4

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

Deployment approval: publish only v0.2.4 as a GitHub development prerelease after successful CI packaging. Keep future versions gated on deliberate release-branch updates.
