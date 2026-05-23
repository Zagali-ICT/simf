# SIMF-OPS-001 — Account Recovery Procedures

Last updated: 2026-05-23
Status: Draft (operational supplement; not a controlled document)

This document is the **standard operating procedure** for restoring access to
a SIMF Control Panel account whose owner has lost both factors (authenticator
app and recovery codes). It complements decision D-041 in
[`DECISIONS_LOG.md`](decisions/DECISIONS_LOG.md).

> Identity must be verified **out of band** before any of these procedures is
> applied. Acceptable out-of-band channels: a phone call to a known number,
> an in-person identity check, a signed letter from the user's line manager.
> Email alone is not a sufficient identity check for any of these procedures.

---

## Tier 1 — Visitor / attendee accounts

Visitors do not have two-factor authentication enabled by default in SIMF
V1.0.0. There is no recovery procedure because there is no second factor to
recover from. A visitor who has forgotten their **password** uses the
public **Forgot password?** link on the sign-in page — same as before.

If a future increment makes 2FA opt-in for visitors, this section will be
revisited.

---

## Tier 2 — Control Panel user (any non-Administrator role)

**Use the CP page** `/admin/reset-2fa`.

| Step | Action |
|---|---|
| 1 | Verify the user's identity out of band. Record the channel and the verification details — they go into the audit reason. |
| 2 | Sign in as an Administrator. |
| 3 | Open the **System → Reset user 2FA** menu item (or navigate to `/admin/reset-2fa`). |
| 4 | Enter the affected user's **email address**. |
| 5 | Enter a **reason** (10–500 chars) — include how identity was verified, when, and by whom. The reason is permanently audited. Example: *"User reported lost phone via call from known number 555-… on 2026-05-23. Verified date of birth and last sign-in city."* |
| 6 | Submit and confirm the modal. |

The system will: wipe the authenticator key, wipe all recovery codes, flip
`TwoFactorEnabled = false`, roll the security stamp, revoke every refresh
token (signing the user out of every session), and email the user a
notification of the reset with the actor email and reason.

The user signs in next time with **password only** and is prompted to
re-enrol 2FA from `/account/profile`. **Make sure they save a recovery code
this time.**

### What is NOT allowed via this page

- **An Administrator cannot reset their own 2FA.** Use `/account/profile` →
  Disable (which requires a current TOTP code, by design). If you can't
  produce a current TOTP code, use Tier 3.
- **An Administrator cannot reset another Administrator's 2FA.** Demote the
  target's role first, perform the reset, then restore the role; or use
  Tier 3 if no other Administrator can demote.

---

## Tier 3 — Administrator / super-administrator

The Administrator role (including the super-administrator) cannot be reset
through the CP page above (separation of privileges, D-041). The recovery
path is **configuration-side**:

| Step | Action |
|---|---|
| 1 | Verify identity out of band, as Tier 2. |
| 2 | Sign in to the API host with operator privileges. |
| 3 | Generate a fresh TOTP secret (Base32, 32 chars). Example: `openssl rand -hex 20 \| xxd -r -p \| base32`. Save it for the next step. |
| 4 | Update the `SuperAdmin:TotpSecret` value in `appsettings.json` (or the equivalent environment-variable override `SuperAdmin__TotpSecret`) for the SIMF API process. |
| 5 | **Restart the API.** The `IdentitySeeder` re-applies the configured TOTP secret on every boot to the super-administrator account. |
| 6 | Share the new secret with the verified administrator through a secure channel (a sealed envelope, an internal vault, an in-person hand-off). They add it to their authenticator as a setup-key entry (`Account: superadmin@…`, key: as generated). |
| 7 | The administrator signs in with password + the TOTP code from the new entry. |
| 8 | On `/account/profile`, the administrator should **re-generate recovery codes immediately** and save the new set in the same secure channel. |

If the administrator has also forgotten their **password**, an operator can
reset it via SQL (`UPDATE AspNetUsers SET PasswordHash = … WHERE Email = …`,
using `UserManager.PasswordHasher.HashPassword(user, "new-password")` in a
short LINQPad / dotnet-script snippet to compute the hash) and force a
password change on next sign-in via `PasswordChangeRequired = 1`.

---

## Emergency fallback — direct SQL wipe

Only when the above tiered procedures cannot be applied (for example, the CP
is itself unavailable and the user must sign in to fix the CP):

```sql
DECLARE @Email NVARCHAR(256) = 'user@example.com';
DECLARE @UserId UNIQUEIDENTIFIER =
    (SELECT Id FROM AspNetUsers WHERE Email = @Email);

UPDATE AspNetUsers
   SET TwoFactorEnabled = 0,
       SecurityStamp = NEWID()
 WHERE Id = @UserId;

DELETE FROM AspNetUserTokens
 WHERE UserId = @UserId
   AND LoginProvider IN ('[AspNetUserStore]', '[SIMF]');

DELETE FROM TotpRecoveryCodes
 WHERE UserId = @UserId;

DELETE FROM RefreshTokens
 WHERE UserId = @UserId;
```

This bypasses the audit trail of the admin-reset endpoint. Record the action
in the operator change log instead. Identity verification still applies.

---

## Audit

Every Tier-2 reset writes an `Admin.TwoFactorReset` row to the operation log
with:

- `EventType = "Admin.TwoFactorReset"`
- `Outcome = "Success"`
- `SubjectEmail` and `SubjectUserId` — the affected user
- `ActorUserId` — the administrator who performed the reset
- `Detail` — the reason text

Failed attempts (admin-vs-admin, self-reset, missing target) emit
`Admin.TwoFactorResetFailed` with the error code. The SOC should treat any
spike in `Admin.TwoFactorResetFailed` or any `Admin.TwoFactorReset` outside
business hours as a potential indicator of abuse.

---

## When the user has not lost both factors

| Situation | Use |
|---|---|
| Authenticator app gone, recovery codes saved | Sign in to TOTP page → **Use a recovery code instead** → enter saved code (D-040) |
| Password forgotten, 2FA intact | Public **Forgot password?** link on `/login` |
| 2FA wants to be turned off voluntarily | `/account/profile` → Disable (requires a current TOTP code) |

These do not need an operator at all.
