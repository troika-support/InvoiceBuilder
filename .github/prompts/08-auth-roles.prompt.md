---
description: "ASP.NET Identity, 2FA for Owner, roles, business access policies, sign-in pages."
---

# 08 · Authentication and roles

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Sign-in and authorisation per spec §1.1 and §5.1.

## Do
1. Identity with `AppUser`, roles Owner and Staff, cookie auth, lockout 5 tries / 15 min, password reset by email (use `IEmailSender` stub until prompt 21).
2. Required authenticator-app 2FA for Owner (enrolment page with QR via QRCoder, recovery codes).
3. `UserBusiness` assignments; `ICurrentBusiness` from the user's selected business (stored per user); policy `CanActOnBusiness` used by UI services and controllers (R2). Also register authorization policies named `Owner` (role Owner) and `Staff` (role Owner or Staff); the theme kit's SideNav uses these names.
4. Seeding command `dotnet run -- seed-owner --email … --tenant "…"` that creates the tenant and first Owner (no self-registration).
5. Sign-in, 2FA, forgot-password and reset pages styled with the theme tokens (centred card, max-width 420, product mark, DS §5.1 voice).
6. Implement `ICurrentUser`/`ICurrentTenant`/`ICurrentBusiness` in Web; replace the dev fixed tenant.

## Tests first
Integration: Staff can't open `/settings`; a user assigned to business A gets 403 on B's client via controller; Owner without 2FA is forced to enrol.

## Done when
I can seed an owner, enrol 2FA, sign in and see the empty app.
