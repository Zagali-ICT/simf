# My profile (CP) — `/account/profile`

| | |
|--|--|
| **Route** | `/account/profile` |
| **Audience** | Any signed-in CP user |
| **Auth** | `[Authorize]` |
| **Status** | ✅ Real |
| **Backend** | `GET /account/api/profile`, `PUT /account/api/profile`, `POST /account/api/profile/avatar`, `POST /account/api/auth/totp/reset-self`, `POST /account/api/auth/recovery-codes/regenerate` |
| **Source** | [`Profile.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Account/Profile.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Per-user profile self-service. View / edit display name, change avatar
(D-116 / D-120 / D-122 / D-123 Cropper.Blazor flow), reset own TOTP
(re-pair), regenerate recovery codes. Reachable from the header user link.

## 4. UI affordances

- **Identity card** — Email (read-only), Display name (editable).
- **Avatar card** — current image + change/upload → opens
  `SimfImageCropperModal` (D-116) → crop → save.
- **Security card** — Reset my 2FA (re-pair flow → routes to
  `/account/totp-pairing`) + Regenerate recovery codes.
- **Sessions card** — list of active sessions with revoke buttons.

## 7. Edge cases

- **Avatar > 2 MB** → server rejects with bilingual error.
- **Cropper crash on dispose** → fixed in D-123 by load-ordering
  `cropper.min.js` before `cropperJsInterop.min.js` in `App.razor`.
- **TOTP reset** → wipes paired secret, redirects to pairing page; current
  session stays valid.

## 11. E2E

| Scenario | ID |
|----------|----|
| Update display name → toast | E2E-PRF-001 |
| Upload avatar + crop + save | E2E-PRF-002 |
| Self-reset TOTP → /account/totp-pairing | E2E-PRF-003 |
| Regenerate recovery codes → 10 fresh codes shown | E2E-PRF-004 |
| Revoke another session → that session 401s next request | E2E-PRF-005 |

## 12. Related

- Decisions: D-116 (cropper visuals), D-120 (PageColor swatch — unrelated but same Cropper era), D-122 (Cropper DI), D-123 (cropperjs load order).
- Companion: [`account-totp-pairing.md`](account-totp-pairing.md).

## D-799 — removing the avatar confirms first

"Remove" under the profile photo deleted the stored image on the first click.
It now opens a `SimfConfirm` stating the image cannot be restored.

_Last reviewed:_ 2026-07-30 by Claude (D-799 destructive-action safety).
