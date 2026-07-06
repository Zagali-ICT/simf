# Registration-status gate — حالة التسجيل (Page 011, `#11`)

- **Route:** `/registration-status` (`RouteNames.registrationStatus`). Access: signed-in but not-yet-approved (router gate route 11).
- **Figma:** **1701:3789** (D-591). **Clean-code freeze:** D-623 (2026-07-04); stages card removed + golden re-locked D-665 (2026-07-06).

## Purpose

A gate for a signed-in but **not-yet-approved** account. On open (and on every
Re-check) it calls `refreshCurrentUser` (`GET /app/users/me`) and renders the state:
**Pending** (under-review + Re-check), **Approved** (Continue → app), **Rejected**
(declined copy). A session-expired failure flips auth signed-out and the router gate
redirects to sign-in.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `registration_status_screen.dart` (230) | State — load, pending/approved/rejected switch, sign-out/continue/back, `_buildBody`/`_buildStatusView`/`_buildError` |
| `widgets/registration_status_header.dart` | `RegistrationStatusHeader` — plain-chevron gate header |
| `widgets/registration_status_hero.dart` | `RegistrationStatusHero` — state ring + headline + message |
| `widgets/registration_primary_button.dart` | `RegistrationPrimaryButton` |
| `widgets/registration_sign_out_link.dart` | `RegistrationSignOutLink` |

The error state keeps a **custom** `_buildError` (beige message + grouped sign-out
link) rather than `SimfErrorState` — the shared widget's white message + separate
link would change the render (D-623).

## L4 Figma parity (frame 1701:3789)

The "المراحل" stages card was **removed** (D-665, 2026-07-06) — the frame 1701:3789 has
no such card and its source nodes (`1701:3805–3822`) were deleted from Figma, so the app
now matches the frame: hero → gold "متابعة" button → "تسجيل الخروج" link. The
`registration_status_1701-3789` golden was regenerated to the cardless render.

## Level-F

Wired: Re-check (pending) / Continue (approved) primary button, sign-out, back, retry.
Reads `refreshCurrentUser`. No missing API.

## Tests

`test/golden/registration_status_golden_test.dart` + the registration feature tests.
E2E: `docs/tests/e2e/mobile-registration-status.md`.
