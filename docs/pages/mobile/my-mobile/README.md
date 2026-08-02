# My mobile number (add / edit) — `myMobile`

| | |
|--|--|
| **Route** | `/my-area/mobile` (app screen #703, **auth-gated**) |
| **Screen** | `MyMobileScreen` |
| **Surface** | Mobile (Flutter) |
| **Audience** | Any signed-in account (opened from My-Area) |
| **Status** | ✅ Real (owner 2026-07-26) |
| **Figma** | No dedicated node — reuses the shared navy account chrome (`AccountSubHeader` + `SimfAuthSweep` + `AuthSubmitButton`), like the biometric step-up and the My-interests edit surface |
| **Source** | [`my_mobile_screen.dart`](../../../../src/Mobile/simf_app/lib/features/myarea/my_mobile_screen.dart) · entry row in [`my_area_dashboard_body.dart`](../../../../src/Mobile/simf_app/lib/features/myarea/widgets/my_area_dashboard_body.dart) |
| **E2E** | [`mobile-my-mobile.md`](../../../tests/e2e/mobile-my-mobile.md) (E2E-MYMOB-001..008) |
| **Last reviewed** | 2026-07-26 |

## 1. Purpose

Owner request (2026-07-26): *"Add / Edit phone number in my profile — NO VERIFY,
ONLY VALIDATE."* Before this the mobile number could only be entered during
sign-up (Page 007); there was no way to add or correct it afterwards.

Reached from **My-Area → "رقم الجوال / Mobile number"**, next to the other
self-service profile edits.

> **No verification step.** There is deliberately **no OTP / SMS confirmation** —
> the number is validated for shape and saved. Do not add a verification phase
> without a new owner decision.

## 2. No schema change, no new endpoint

`UserProfile.SaudiMobile` / `.InternationalMobile` already exist
(`UserProfileConfiguration` — `HasMaxLength(20)` / `(24)`), the app's
`UpsertUserProfileRequest` already carries both, and
`UpsertUserProfileRequestValidator` already enforces the C4 (D-371) shapes on
them. So this screen is **UI only**: it reuses the existing full-profile upsert.

## 3. Behaviour

- **Load:** `GET /app/account/user-profile`. The profile's `isSaudi` picks which
  number is shown and edited — `saudiMobile` for a Saudi national,
  `internationalMobile` for everyone else. The current value is shown above the
  field (or "Not added yet / لم يُضف بعد" when empty) and pre-fills the input.
- **Save:** re-POSTs the **full** loaded profile via
  `UserProfileResponse.toUpsertRequest(mobile: normalizePhone(input))` to
  `POST /app/account/user-profile` — the same lossless round-trip the
  My-interests edit uses, so a mobile-only change nulls nothing else (notably
  `regionId` / `jobTitleArabic`, which the service writes unconditionally). On
  success a "Your mobile number was updated" toast shows and the screen pops
  back to My-Area; on failure the message stays on the screen and nothing is
  navigated.

## 4. Validation (the only gate)

One rule, one place: `validateMobile()` in
[`mobile_field.dart`](../../../../src/Mobile/simf_app/lib/features/account/widgets/mobile_field.dart),
which delegates the shapes to
[`phone_validation.dart`](../../../../src/Mobile/simf_app/lib/core/validation/phone_validation.dart) —
the same functions the sign-up profile form and the staff walk-in form use.

| Case | Rule | Message |
|------|------|---------|
| Empty | Required (D-723) | `mobileRequired` — "رقم الجوال مطلوب" / "Mobile number is required" |
| Saudi | `05XXXXXXXX`, `+9665XXXXXXXX` or `009665XXXXXXXX` | `saudiMobileInvalid` |
| Non-Saudi | E.164 — `+`, non-zero lead, 8–15 digits (`00…` accepted) | `internationalMobileInvalid` |

Arabic-Indic digits fold to Western, spaces / dashes are stripped, and a leading
`00` is rewritten to `+` (`normalizePhone`) **before** submit, so the stored
value always matches the server's `+`-only shapes. Client `maxLength` 17 and the
server shapes both stay inside the EF column caps (20 / 24) — the triple-lock
holds with no change.

## 5. i18n / RTL

Arabic-first. Title رقم الجوال. The field label + helper mirror in RTL, but the
**number itself always renders LTR** (`textDirection: TextDirection.ltr` on both
the read-out and the input, matching the sign-up form).

## 6. Accessibility

The header back control, the labelled field and the Save button are all named;
the field uses `autovalidateMode: onUserInteraction` so the error is announced as
the user types rather than only on submit.

## 7. Tests

- Widget: `test/features/myarea/my_mobile_screen_test.dart` — pre-fill, invalid
  shape rejected without an API call, empty rejected, valid save carries every
  other field, `00…` → `+…` normalisation, server error stays on screen, load
  failure offers retry.
- API: `tests/SIMF.Api.Tests/UserProfileTests.cs` —
  `POST_edit_that_only_changes_the_mobile_persists_it_and_keeps_every_other_field`
  and `POST_adds_a_mobile_to_a_profile_that_had_none` (plus the pre-existing
  shape Theories).
- Blast-radius: My-Area golden re-locked for the new entry row.

## 8. Changelog

- **2026-07-26 (owner):** created. New route #703 + My-Area entry row + the
  `toUpsertRequest(mobile:)` override + the shared `validateMobile()` rule.
