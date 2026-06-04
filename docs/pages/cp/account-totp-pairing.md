# TOTP pairing (first-time setup) — `/account/totp-pairing`

| | |
|--|--|
| **Route** | `/account/totp-pairing` |
| **Audience** | First-time or re-pairing user |
| **Auth** | `[Authorize]` + `TotpPairingRequired = true` on the session |
| **Status** | ✅ Real |
| **Backend** | `GET /account/api/auth/totp/setup`, `POST /account/api/auth/totp/pair`, `GET /account/api/auth/recovery-codes` |
| **Source** | [`TotpPairing.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Account/TotpPairing.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

First-time TOTP setup, and the re-pair flow after a self-reset. Shows:

1. A QR code (server-rendered SVG via QRCoder) encoding the
   `otpauth://totp/SIMF:{email}?secret={base32}&issuer=SIMF&algorithm=SHA1&digits=6&period=30` URL.
2. The manual-entry secret as text (in case the user can't scan).
3. A 6-digit Verify field.
4. After successful Verify: the 10 single-use recovery codes (download / print
   prompt) → Continue button → sign-in completes.

## 7. Edge cases

- **User refreshes mid-pair** → server holds the secret in the session,
  page rebuilds correctly.
- **User abandons before saving recovery codes** → codes are still generated;
  user can regenerate via `/account/profile` later, but the original 10 codes
  are lost.
- **Invalid first code** → server rejects, user reads next code.

## 11. E2E

| Scenario | ID |
|----------|----|
| Scan QR + enter code → success + 10 codes shown | E2E-TPP-001 |
| Manual-entry secret works | E2E-TPP-002 |
| Wrong code → retry | E2E-TPP-003 |
| Continue after codes seen → /  | E2E-TPP-004 |

## 12. Related

- Auth flow: [`login.md`](login.md), [`login-totp.md`](login-totp.md), [`login-recovery.md`](login-recovery.md).
- Self-service reset: [`account-profile.md`](account-profile.md).

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
