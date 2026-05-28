# E2E test catalogue — My profile (`/account/profile`)

| | |
|--|--|
| **Page** | [`cp/account-profile.md`](../../pages/cp/account-profile.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-PRF-001 | Update display name → toast | P1 |
| E2E-PRF-002 | Upload + crop avatar (D-116/D-122/D-123) → saved | P1 |
| E2E-PRF-003 | Self-reset TOTP → routes to /account/totp-pairing | P0 |
| E2E-PRF-004 | Regenerate recovery codes → 10 fresh codes shown | P1 |
| E2E-PRF-005 | Revoke another session → that session 401s next request | P1 |

## Scenarios

### E2E-PRF-001 — Update display name

```gherkin
Scenario: Update display name
  Given the user is on /account/profile
  When they change Display name to "New Display Name"
  And click Save in the Identity card
  Then PUT /account/api/profile fires
  And the response is ApiResult.Ok with updated profile
  And the toast reads Account.Profile.Saved
  And the top header user link updates to "New Display Name"
```

### E2E-PRF-002 — Avatar upload + crop

```gherkin
Scenario: Upload a new avatar and crop it
  Given the user clicks Change avatar
  And picks a 1 MB PNG
  Then SimfImageCropperModal opens (D-116/D-122/D-123 stack)
  And the cropper canvas + preview both render (D-123 fixed)
  When they crop to 256×256
  And click "Crop and save"
  Then POST /account/api/profile/avatar fires with the cropped image
  And the avatar in the page + the top header refresh to show the new image
  And no console error (cropper.destroy resolves correctly per D-123)
```

### E2E-PRF-003 — Self-reset TOTP

```gherkin
Scenario: Reset my own 2FA → routes to pairing
  Given the user is on /account/profile
  When they click "Reset my 2FA" in the Security card
  And confirm in the modal
  Then POST /account/api/auth/totp/reset-self fires
  And the server wipes the authenticator secret + recovery codes
  And the current session stays valid
  And the browser routes to /account/totp-pairing
  And the user pairs fresh codes (see UC-AUTH-TPP)
```

### E2E-PRF-004 — Regenerate recovery codes

```gherkin
Scenario: Regenerate the 10 single-use recovery codes
  Given the user is on /account/profile
  When they click "Regenerate recovery codes"
  And confirm
  Then POST /account/api/auth/recovery-codes/regenerate fires
  And the 10 fresh codes appear in a download/print modal
  And the previous 10 codes are invalidated
```

### E2E-PRF-005 — Revoke another session

```gherkin
Scenario: Revoke a peer session
  Given the user has 2 active sessions (e.g. desktop + phone)
  When they revoke the phone session from the Sessions card
  Then DELETE /account/api/auth/sessions/{id} fires
  And the next API call from the phone session returns 401
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
