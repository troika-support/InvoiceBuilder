---
description: "Security, privacy, rate limits, health checks and accessibility pass as a checklist."
---

# 25 · Hardening

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Production readiness. Work through the checklist and report pass/fail for each item.

## Do — checklist
1. HTTPS only, HSTS, security headers (CSP allowing self + `data:` fonts/images; frame-ancestors none, except the preview iframe which uses `srcdoc`), Referrer-Policy, X-Content-Type-Options.
2. Antiforgery on all forms and state-changing controllers (webhooks excluded and signature-verified).
3. Rate limits: `/d/*` 60/min/IP, webhooks 60/min/IP, sign-in 10/min/IP.
4. Data Protection key ring persisted to `[path]`, protected (DPAPI-NG or certificate); app name set.
5. Secrets audit: nothing in appsettings*.json; all options `ValidateOnStart`.
6. Health checks `/health/live`, `/health/ready` (DB, Hangfire, Playwright pool).
7. Logging: correlation ID middleware; PII and secrets redaction enricher; retention 30 days for files.
8. Upload validation (type sniffing, size limits, SVG sanitised or rasterised).
9. Accessibility pass on every screen: axe DevTools (or Playwright + axe) with zero serious issues; manual keyboard and screen-reader pass for editor, designer (Layers path), client links.
10. Privacy: activity and logs avoid full phone numbers; documented retention placeholder `[set by OWNER]`.

## Done when
Every item is marked pass, or has a written follow-up I agreed to.
