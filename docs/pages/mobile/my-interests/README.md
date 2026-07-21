# My interests (edit) — `myInterests`

| | |
|--|--|
| **Route** | `/my-area/interests` (app screen #702, **auth-gated**) |
| **Screen** | `SignUpInterestsScreen(editMode: true)` — the SAME interests page as sign-up, in edit mode |
| **Surface** | Mobile (Flutter) |
| **Audience** | Any signed-in account (opened from My-Area) |
| **Status** | ✅ Real (#14, 2026-07-21) |
| **Figma** | No dedicated node — mirrors the sign-up interests screen (505:1083) per owner direction ("same interest page for create + edit") |
| **Source** | [`sign_up_interests_screen.dart`](../../../../src/Mobile/simf_app/lib/features/account/sign_up_interests_screen.dart) (`editMode`) · entry row in [`my_area_dashboard_body.dart`](../../../../src/Mobile/simf_app/lib/features/myarea/widgets/my_area_dashboard_body.dart) |
| **E2E** | [`mobile-my-interests.md`](../../../tests/e2e/mobile-my-interests.md) (E2E-MYINT-001..007) |
| **Last reviewed** | 2026-07-21 |

## 1. Purpose

Lets a signed-in user **edit their interests after sign-up** (#14) — previously
interests could only be chosen during registration. Reached from **My-Area →
"اهتماماتي / My interests"**. It is the sign-up interests screen reused in edit
mode, so the visual language, the `InterestChip` grid, and the **1-10** rule are
identical to create; only the load + save wiring differs.

## 2. Behaviour

- **Load:** self-loads the current profile (`GET /app/account/user-profile`) and
  the interests lookup (`GET /app/account/interests`), then pre-selects the
  interests already saved on the profile. (Create mode instead receives an
  in-memory `SignUpProfileDraft`; a draft-less create open shows a recover state
  — edit mode never does.)
- **Save:** re-POSTs the **full** profile via
  `UserProfileResponse.toUpsertRequest().copyWith(interestIds: …)` to
  `POST /app/account/user-profile`, then shows the "Your interests were updated"
  toast and pops back to My-Area.

## 3. The round-trip fidelity fix (#14)

The only write is the **full-profile** upsert, and the server sets `RegionId` +
`JobTitleArabic` unconditionally from the request. The Flutter DTO previously
**omitted** both, so an interests-only save would null the user's region and
Arabic job title. #14 adds `regionId` + `jobTitleArabic` to both
`UpsertUserProfileRequest` (write) and `UserProfileResponse` (read) — additive
keys on the D-219 wire contract that the backend already accepts and returns
(`UserProfile.cs`) — and a `toUpsertRequest()` helper that mirrors **every**
loaded field. A unit test (`profile_models_test`) and an edit-mode widget test
(`sign_up_interests_screen_test`) pin that no field is wiped.

> **Note:** the same latent wipe affects the shipped **Website** `/account/profile`
> interests editor (its `UpsertUserProfileRequest` model also never sets
> `RegionId`/`JobTitleArabic`). That is a separate defect, tracked apart from #14.

## 4. Validation & edge cases

- **1-10 interests** (client rule, shared with create); the server accepts 0-10
  (D-684), so the client rule is stricter by design.
- **Load failure** → error state + Retry; no write is attempted.
- **Auth gate** — route #702 is in `_authenticatedRoutes`; an anonymous open
  redirects to sign-in.

## 5. i18n / RTL

Arabic-first. Title اهتماماتي; the chip grid + `n / 10` counter mirror in RTL;
each chip shows the interest's `nameArabic` under Arabic.

## 6. Tests

- Unit: `profile_models_test` — `toUpsertRequest` round-trip fidelity + `copyWith`.
- Widget: `sign_up_interests_screen_test` — edit-mode pre-select, lossless save
  (no wipe), load failure. Create-mode tests + the 505:1083 golden stay green
  (create render unchanged). My-Area golden re-locked for the new entry row.
- Blast-radius: `router_role_matrix_test` (route #702 gate).

## 7. Changelog

- **2026-07-21 (#14):** created. Generalised the sign-up interests screen to
  serve create + edit; added the `myInterests` route + the My-Area entry row +
  the `regionId`/`jobTitleArabic` round-trip fix.
