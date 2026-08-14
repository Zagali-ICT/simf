# E2E test catalogue — `My devices` (`myDevices`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Closes security finding S10
> from `docs/security/SIMF-Security-Review-DeviceKeys-2026-08-13.md`: the account
> could hold biometric device keys with no surface showing them, which is also
> why the label defect behind D-884 went unnoticed for the whole life of the
> feature. Runner-agnostic Gherkin.
>
> **No Figma node.** None exists for this screen. `simf_app/CLAUDE.md` §13.5
> requires asking rather than inventing one; the owner was asked on 2026-08-14
> and authorised the established house style, so the screen is composed from the
> shared `Simf*` catalogue (`SimfPageShell`, `SimfCard`, `SimfPullToRefresh`,
> `SimfEmptyState`, `SimfErrorState`, `SimfConfirmDialog`). There is therefore
> **no golden** pinning it to a design; the render is pinned by the widget tests
> and by this catalogue.

| | |
|--|--|
| **Route** | aux `/account/my-devices` (`RouteNames.myDevices`) — pushed from the Face-ID row in the profile / side menu |
| **APIs** | `GET /app/auth/device-keys` (list) · `DELETE /app/auth/device-keys/{id}` (revoke) |
| **Surface** | Mobile (Flutter) — signed-in, Approved account |
| **Permissions** | `RequireApprovedAccount` on both endpoints; not a CP/admin action |
| **Source** | `lib/features/account/my_devices_screen.dart` |
| **Decisions** | D-884 (the screen + the label), D-883 (the five-key cap it makes observable), D-882 (revocation on password change) |

## Coverage matrix

| Id | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MYD-001 | The list renders every device key on the account, newest active first | happy | P0 | authored (screen wiring; backend list covered by `DeviceKeySignInTests`) |
| E2E-MYD-002 | The device in the user's hand carries a "This device" chip | happy | P0 | authored |
| E2E-MYD-003 | A row shows last-used when the key has been used, and added-on when it has not | happy | P1 | authored |
| E2E-MYD-004 | Revoking a row asks for confirmation first, and cancelling changes nothing | edge | P0 | authored |
| E2E-MYD-005 | Confirming a revoke removes the key; a later challenge for it returns 401 | happy | P0 | authored ✓ (backend test) |
| E2E-MYD-006 | Revoking THIS device's key also clears the local private half, so the Face-ID button disappears | edge | P0 | authored |
| E2E-MYD-007 | An account with no enrolled keys shows the empty state, not an error | edge | P1 | authored |
| E2E-MYD-008 | A failed load shows the error state with a working retry; pull-to-refresh re-fetches | resilience | P1 | authored |
| E2E-MYD-009 | RTL render (Arabic) — rows, chip and the confirm dialog mirror correctly | i18n | P1 | authored |

## Scenarios

### E2E-MYD-001 — The list renders

```gherkin
Scenario: The account's device keys are listed
  Given a signed-in approved visitor with 2 enrolled device keys
  When the user opens My devices
  Then GET /app/auth/device-keys is called once
  And both devices are listed, active first, newest first within that
  And each row shows the label the device was enrolled under
```

### E2E-MYD-002 — This device is marked

```gherkin
Scenario: The phone in your hand is identifiable
  Given the account holds a key enrolled on THIS device and one from another
  When the user opens My devices
  Then only the row whose id matches the locally stored device-key id
    carries the "This device" chip
```

### E2E-MYD-003 — The timestamps read in Saudi local time

```gherkin
Scenario: A used device shows when it was last used
  Given a device key with a lastUsedAt
  Then its row reads "Last used <date> <12-hour time>" in Saudi local time
  And a key that has never been used reads "Added <date> <time>" instead
```

Never UTC on a user-facing surface (D-770).

### E2E-MYD-004 — Revoke confirms first

```gherkin
Scenario: A destructive action asks before acting
  Given the My devices list
  When the user taps the delete icon on a row
  Then a destructive confirm dialog appears
  And cancelling it calls no endpoint and leaves the list unchanged
```

The wording differs when the row is this device, because the consequence does:
the user will need their password next time.

### E2E-MYD-005 — Revoke removes the key

```gherkin
Scenario: A confirmed revoke kills the credential
  Given the My devices list
  When the user confirms the revoke on a row
  Then DELETE /app/auth/device-keys/{id} is called
  And a success toast shows and the list re-fetches
  And a later challenge request for that id returns 401
```

**Evidence:** `DeviceKeySignInTests.Revoked_key_cannot_be_used_for_challenge`.

### E2E-MYD-006 — Revoking this device clears the local key

```gherkin
Scenario: The app cannot keep offering a dead credential
  Given the account's key for THIS device
  When the user revokes it from My devices
  Then the locally stored device-key id and private half are both cleared
  And the sign-in screen no longer offers the Face-ID button
```

This is the contract on the screen: without it the app would advertise biometric
sign-in backed by a credential the server has already revoked.

### E2E-MYD-007 — Empty state

```gherkin
Scenario: No devices is not an error
  Given a signed-in approved visitor with no enrolled device keys
  When the user opens My devices
  Then the empty state shows with the fingerprint mark and its bilingual message
  And pull-to-refresh still fires (the empty state is hosted in SimfPullableHost)
```

### E2E-MYD-008 — Error and retry

```gherkin
Scenario: A failed load is recoverable
  Given GET /app/auth/device-keys fails
  When the user opens My devices
  Then the shared error state shows with a Retry action
  And tapping Retry re-issues the request
```

### E2E-MYD-009 — RTL

```gherkin
Scenario: The screen mirrors in Arabic
  Given the app language is Arabic
  Then the row icon, label, chip and delete control mirror
  And the confirm dialog reads right-to-left
```
