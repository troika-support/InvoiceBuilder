---
description: "Publish profile, server setup script, runbook and smoke test."
---

# 27 · Deploy to IIS

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Repeatable deployment to Windows Server / IIS (spec §7).

## Do
1. Publish profile (framework-dependent, win-x64), `web.config` with in-process hosting and stdout logs off.
2. `deploy/Setup-Server.ps1`: install hosting bundle check, create the app pool (no managed code, identity), site with HTTPS binding, folders for files, logs and the Data Protection keys with ACLs, `PLAYWRIGHT_BROWSERS_PATH` and Chromium install, app-pool environment variables for every secret setting name (values prompted, never stored in the script).
3. `deploy/Publish.ps1`: build, test, apply the idempotent SQL script, copy with `app_offline.htm`, warm-up request.
4. `deploy/Smoke-Test.ps1`: `/health/ready`, sign-in page 200, render a sample PDF via an Owner-only endpoint, Paynow webhook rejects a bad hash.
5. `docs/runbook.md`: first run (seed owner, enrol 2FA, configure business, Paynow test mode, email/WhatsApp keys), go-live checklist (Paynow live keys, ResultUrl reachable, WhatsApp templates approved, backups scheduled, `// VERIFY:` items closed), backup and restore, rotating secrets, troubleshooting (template-rendering §9, paynow §9).

## Done when
A clean Windows Server VM goes from nothing to a passing smoke test using only the scripts and runbook.
