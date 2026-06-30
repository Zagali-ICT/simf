# Sign up — interests (اهتماماتي) — mobile `/sign-up/interests`

| Field | Value |
|---|---|
| Route | `/sign-up/interests` (`RouteNames.signUpInterests`) · **AUTH-only** (any signed-in account mid sign-up; carries the draft) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/sign_up_interests_screen.dart` (`SignUpInterestsScreen`) |
| Figma node | `505:1083` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`; D-365) |
| Shell | Custom navy `Scaffold` — rotated sweep (505:1086) + back/title header (505:1190) + pinned gold CTA |
| Providers | `profileRepositoryProvider` → `ProfileRepository` (interests lookup + the single save + the two image uploads) · `avatarBustProvider` (bump on face-photo upload) |
| Tests | `test/features/account/sign_up_interests_screen_test.dart` (widget, 8 cases) · golden `test/golden/sign_up_interests_golden_test.dart` (`goldens/interests_505-1083.png`) · E2E [`mobile-sign-up-interests.md`](../../../tests/e2e/mobile-sign-up-interests.md) |
| Status | ✅ Real — D-332 (save moved here) → D-365 (KSA frame 505:1083) → D-373 (reference no. on save) → **clean-code frozen (D-550, 2026-06-30)** |
| Legacy detail | `docs/App/Page_007-01/` — retained as the detailed historical spec |

## 1. Purpose
Step 4 (final) of the corrected sign-up flow: Register → OTP → profile data
(`/sign-up/visitor`) → **this screen (interests)** → single save → Confirmation.
Collects **1–10** interests, then fires the **single** `POST /app/account/user-profile`
(the carried `SignUpProfileDraft` + the picked `interestIds`), after uploading the
mandatory ID document (+ the face photo, mandatory for men) — D-332/D-050.

## 2. Audience & access
Any signed-in account mid sign-up. The draft arrives as the route `extra`; a
draft-less deep link shows the recover state back to the profile-data screen.

## 3. UI & behaviour (top → bottom)
The scroll body + the pinned CTA are each capped by `MaxWidthBody(560)` (fill a
phone, don't stretch on a tablet — §13.7).
1. **Header** (505:1190) — back chevron at the start + centred "اهتماماتي".
2. **Heading** "اختر اهتماماتك" + helper text.
3. **Pill grid** (505:1222) — a two-column lazy `GridView.builder` of `InterestChip`s:
   gold when selected, `navyDeep` with a `chipBorderNavy` border otherwise. Tapping
   an 11th pick is blocked with the `interestsMaxReached` snackbar.
4. **Counter** — `n / 10 مختار`.
5. **Pinned gold CTA "متابعة"** — disabled until ≥1 pick; busy spinner while saving.

## 4. Data / API (wire contract D-219 frozen)
- **Load:** `GET /app/account/interests` via `ProfileRepository.getInterests()`.
- **Save (single):** before the upsert, the two images land first —
  `uploadIdImage` (mandatory for everyone; a failure blocks) then `uploadAvatar`
  (mandatory for men → blocks; optional for women → falls through). Then
  `POST /app/account/user-profile` = `draft.request.copyWith(interestIds: …)`.
  On success the response carries the registration reference number, passed to the
  success screen without a re-fetch (D-373).

## 5. Validation & edge cases
- Selection bounded **1–10** (CTA disabled at 0; the 11th tap is rejected with a
  snackbar). Pre-selected ids not in the active lookup are dropped on load.
- Load failure → inline retry (`_buildLoadError`); draft-less open → recover CTA
  back to the profile-data screen; a male face-upload failure blocks the save.

## 6. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct. All strings via `AppL10n`. Chip label
picks `nameArabic`/`name` by locale. Brand font applied once in the theme.

## 7. Testing
- **Widget** (`sign_up_interests_screen_test.dart`, 8 cases): load → pick → save →
  navigate, the 1–10 bounds, the two-photo split (ID-fail blocks; male face-fail
  blocks; female face-fail falls through), draft-less recover state.
- **Golden** (`sign_up_interests_golden_test.dart`): `goldens/interests_505-1083.png`
  @375×812 RTL with 2 of 10 selected — locks the frozen frame parity.
- **E2E**: [`docs/tests/e2e/mobile-sign-up-interests.md`](../../../tests/e2e/mobile-sign-up-interests.md).

## 8. Clean-code DoD (D-550 freeze — 2026-06-30)
- [x] Screen 449 → 437 lines; pill extracted to `InterestChip`; header to
      `_buildHeader`; grid already `GridView.builder` (lazy)
- [x] Shared, not copied: `MaxWidthBody`, `SimfTokens`, `InterestChip` (feature widget)
- [x] Flexible width via `MaxWidthBody(560)` (scroll body + pinned CTA); 0 raw
      `Color(0x…)` in the widget (`chipBorderNavy` + `surfaceTint` tokens)
- [x] Figma node `505:1083` bound; golden locks parity
- [x] widget + golden tests + E2E catalogue + this doc, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (D-219) unchanged

## 9. Changelog
- **2026-06-30 (Phase 3, D-550):** clean-code freeze — dropped the `_chipBorder` +
  `_sweepTint` consts (added `SimfTokens.chipBorderNavy`; reused `surfaceTint`);
  extracted `InterestChip` + `_buildHeader`; wrapped the scroll body + the pinned
  CTA in `MaxWidthBody(560)`; added the `505:1083` golden + this consolidated doc.
  Behaviour + render unchanged.
- **D-373:** the save response carries the registration reference number.
- **D-365:** rebuilt to the KSA frame 505:1083.
- **D-332/D-050:** the single profile save moved here from the profile-data screen.
