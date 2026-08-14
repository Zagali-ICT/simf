# My devices — أجهزتي

| | |
|--|--|
| **Route** | `/account/my-devices` (`RouteNames.myDevices`), pushed |
| **Source** | `src/Mobile/simf_app/lib/features/account/my_devices_screen.dart` |
| **Surface** | Mobile (Flutter) |
| **Audience** | Signed-in, Approved account (`RequireApprovedAccount`) |
| **APIs** | `GET /app/auth/device-keys` · `DELETE /app/auth/device-keys/{id}` |
| **E2E** | [`e2e/mobile-my-devices.md`](../../../tests/e2e/mobile-my-devices.md), `E2E-MYD-001..009` |
| **Tests** | `test/features/account/device_label_test.dart`; the revoke path in `tests/SIMF.Api.Tests/DeviceKeySignInTests.cs` |
| **Decisions** | D-884 (this screen + the device label), D-883 (the five-key cap), D-882 (revocation on password change) |

## Purpose

Show every biometric device key enrolled on the account, and let the owner revoke
any of them.

It exists because of security finding S10: the account could hold credentials
with no surface showing them. That is not only an inconvenience. A device key
**outlives a session revoke**, so before this screen the only person who could
see what an account held was someone reading the database, and the only way to
remove one was an administrator calling the admin endpoint with an id nobody
could look up.

It is also what makes the sibling fixes observable: the five-key cap (D-883)
bounds a set nobody could previously see, and the enrolment notification points
at a list that now exists.

## Design source

**There is no Figma node for this screen, and one was asked for.**
`simf_app/CLAUDE.md` §13.5 requires asking rather than inventing a design; the
owner was asked on 2026-08-14 and authorised the established house style. The
screen is therefore composed entirely from the shared catalogue:
`SimfPageShell`, `SimfCard`, `SimfPullToRefresh` / `SimfPullableHost`,
`SimfEmptyState`, `SimfErrorState`, `SimfConfirmDialog`, and `SimfTokens` for
every colour, space and text style.

Consequence worth knowing: **no golden pins this render.** Parity is held by the
E2E catalogue and the widget tests, not by a pixel comparison, because there is
nothing to compare against.

## Layout

One row per device key, active first and newest first within that. Each row
carries:

- a phone glyph, gold while the key is active and muted once revoked;
- the label the device was enrolled under (D-884), or "Unnamed device" if empty;
- a **This device** chip when the row's id matches the locally stored key;
- a subtitle: last-used when the key has been used, added-on when it has not,
  and the revocation stamp for a revoked row;
- a delete control, shown only for active rows, replaced by a spinner in flight.

Timestamps render in Saudi local time, 12-hour, never UTC (D-770).

Revoked rows are kept in the list rather than filtered out: "this device was
removed on the 3rd" is the half of an audit trail a user can actually read.

## Behaviour

- **Load** on open, plus pull-to-refresh (owner rule: every data screen).
- **Revoke** asks first, through the shared destructive confirm. The wording
  differs when the row is this device, because the consequence differs: the user
  will need their password next time.
- **Revoking this device's own key also clears the local private half.** This is
  the screen's contract. Without it the app would keep offering a Face-ID button
  backed by a credential the server has already revoked. It lives in
  `AuthController.revokeDeviceKey`, which shares `_clearLocalDeviceKey` with
  `disableDeviceKey`.
- **Empty** and **error** states use the shared surfaces, both hosted in
  `SimfPullableHost` so the refresh gesture still fires on short content.

## Edge cases

| Case | Behaviour |
|------|-----------|
| No keys enrolled | Empty state with the fingerprint mark, not an error |
| Load fails | Shared error state with a working Retry |
| Revoke fails | Toast with the localized failure; the list is untouched |
| Revoking the last key | Allowed; the user falls back to password sign-in |
| A key revoked on another device | Appears revoked after the next load or refresh |

## Accessibility and i18n

Every string goes through `AppL10n` (ar + en). The screen mirrors in RTL through
the shared widgets; the delete control carries a tooltip. Arabic is the primary
language.

## Changelog

| Date | Change |
|------|--------|
| 2026-08-14 | First issue (D-884). Closes security finding S10 |
