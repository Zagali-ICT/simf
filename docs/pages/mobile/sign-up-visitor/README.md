# Sign-up — profile data (إنشاء ملف شخصي) — mobile `/sign-up/visitor`

| Field | Value |
|---|---|
| Route | `/sign-up/visitor` (`RouteNames.signUpVisitor`) · **AUTH-only** (any signed-in account; no role / no permission, D7) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/account/sign_up_visitor_screen.dart` (`SignUpVisitorScreen`) |
| Figma node | `168:2972` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`; D-368) |
| Shell | `SimfFormScaffold` (`pinnedHeader: true`) — the shared account/entry scaffold (back + globe toggle, logo + forum name) |
| Providers | `profileRepositoryProvider` → `ProfileRepository` (pre-fill + 3 lookups) |
| Tests | `test/features/account/sign_up_visitor_screen_test.dart` (widget, 23 cases) · `plate_validation_test.dart` · `phone_validation_test.dart` · `profile_models_test.dart` · golden `test/golden/sign_up_visitor_golden_test.dart` (`goldens/sign_up_visitor_168-2972.png`) · E2E [`mobile-sign-up-visitor.md`](../../../tests/e2e/mobile-sign-up-visitor.md) (E2E-MOB007-001..022) |
| Status | ✅ Real — D-332 (rework: save moved to interests) → D-368 (Figma 168:2972) → D-371/D-373/D-374/D-375 amendments → **clean-code frozen (D-546, 2026-06-30)** |
| Legacy detail | `docs/App/Page_007/` (Function / Logic / API / Design) — retained as the detailed historical spec |

## 1. Purpose
Step 3 of the corrected sign-up flow — Register → OTP → **this screen (profile
data)** → interests (`/sign-up/interests`) → single save → Confirmation. Collects
the registration fields and carries them forward as a `SignUpProfileDraft` (route
extra) — **no API write happens here**; the one upsert fires on the interests
screen (D-332). Also the post-sign-in landing for any account whose server-computed
`profileComplete` is `false` (D-374).

## 2. Audience & access
Any signed-in account (the `auth` rate-limit bucket; no `Policies(...)`, not
`AllowAnonymous`). Actor is resolved from the `sub` claim — the body never carries a
user id.

## 3. UI & behaviour (form order)
The beige form card (capped by `MaxWidthBody(560)` so it fills a phone but doesn't
stretch edge-to-edge on a tablet — §13.7) holds, in order:
1. **نوع التسجيل** — Visitor/Other beige tabs (`BeigeTabs`). **Not stored**; it
   filters the ProfileType lookup. **C5 (D-371):** Visitor hides the picker and
   auto-locks the seeded **"عادي / Normal"** type; Other shows the picker and a pick
   is **required**.
2. **التصنيف / ProfileType** (`DropdownButtonFormField`, D-722 — a **simple
   dropdown/select**, not the full-screen searchable sheet, since the type list is
   short) — Other only.
3. Full name **AR** / **EN** (`SimfLabeledTextField`; per-script keystroke filters).
4. **الجنس** — `GenderPillsField` (ذكر / أنثى; default Male).
5. **الجهة / Organisation** (`SimfPickerField` typeahead → `LookupSearchSheet`) —
   **required** (B3 / D-221).
6. **المسمى الوظيفي** — job title (optional).
7. **الجنسية** — searchable country sheet (default SA). The pick **drives the
   document path** (D-373): SA → national-ID; else Iqama / Passport tabs + number.
8. **document fields** (`_buildDocumentFields`).
9. **رقم الجوال** — one conditional `MobileField` (Saudi or international, C4 shapes).
10. **تاريخ الميلاد** — `DateOfBirthField` (**≥ 18**, D-197).
11. **مكان الميلاد** — place of birth (optional, D-163).
12. **رقم اللوحة** — Saudi plate (optional, C6/D-371; assemble/parse in
    `plate_validation.dart`).
13. **المرفقات** — `AttachmentField` ID document (mandatory) + face photo
    (**camera-only**, mandatory for men — C7/D-371; server face-gate).
14. **الموافقة على الشروط والأحكام؟** terms link → Page 009.
15. **التالي / Next** — validates and advances (carries the draft).

## 4. Data / API (the wire contract D-219 is frozen)
Read on load (concurrent), per `ProfileRepository`:
- **E1** `GET /app/account/user-profile` → `UserProfileResponse` (pre-fill; empty on
  first profile).
- **E3** `GET /app/account/user-profile/countries` → `{ countries: [{ code, name,
  nameArabic }] }`.
- **E4** `GET /app/account/profile-types?isVisitor={bool}` → `{ items: [{ id, name,
  nameArabic, isVisitor }] }` (re-queried when the Visitor/Other tab flips).
- **E6** `GET /app/organisations?search={text}&top=20` → `[{ id, nameAr, nameEn, city }]`.

The **save** (`POST /app/account/user-profile` + the multipart id-image upload + the
server face-gate `VISITOR_ID_IMAGE_NO_FACE`) runs on the **interests** screen
(007-01) once `interestIds` (1–10) are picked. Async load is surfaced via explicit
loading (spinner) / data / inline-retry (`_buildLoadError`) states.

## 5. Validation & edge cases
- Names: 2–4 parts, one script (AR field blocks non-Arabic at the keystroke; EN
  blocks non-Latin). Plate: Saudi standard (3 of the 17 letters + 1–4 digits, either
  order, ≤7 chars; Arabic-script + Arabic-Indic digits normalised) — see
  `plate_validation.dart` (`isStandardPlateNumber` / `assemblePlate` / `parsePlate`,
  mirrors the server `SaudiPlate`; D-468/D-471).
- Nationality gate (D-373): a null nationality with no SA fallback blocks Next with
  the inline picker error (it is **not** a `FormField`); an unknown code with SA
  present falls back to Saudi Arabia.
- A failed profile-types lookup shows an inline retry — never a silently hidden
  picker (D-375). A failed pre-fill load shows the full-screen retry.

## 6. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct. All strings via `AppL10n`. The brand
font (`FSAlbertArabic`) is applied once in the theme — including the gold CTA, after
the D-545 theme fix (see Changelog).

## 7. Testing
- **Widget** (`sign_up_visitor_screen_test.dart`, 23 cases): type filter, the
  Visitor/Other picker lock, the D-373 nationality→document switch + gate +
  fallback, the D-375 lookup retry, load-failure retry, Next draft assembly.
- **Unit**: `plate_validation_test.dart` (assemble/parse round-trips, D-468/D-471),
  `phone_validation_test.dart`, `profile_models_test.dart`.
- **Golden** (`sign_up_visitor_golden_test.dart`): `goldens/sign_up_visitor_168-2972.png`
  @375×2100 RTL (empty/default state) — locks the frozen frame parity.
- **E2E**: [`docs/tests/e2e/mobile-sign-up-visitor.md`](../../../tests/e2e/mobile-sign-up-visitor.md).

## 8. Clean-code DoD (D-546 freeze — 2026-06-30)
- [x] Screen 2245 → ~1530 lines; 9 presentational widgets extracted to
      `features/account/widgets/`; pure plate logic to `plate_validation.dart`
- [x] Shared, not copied: `SimfFormScaffold`, `SimfFieldLabel`, `SimfFieldStyle`,
      `SimfLabeledTextField`, `SimfPickerField`, `GenderPillsField`, `AttachmentField`
- [x] Flexible width via `MaxWidthBody(560)`; 0 raw `Color(0x…)` (sweep → `SimfTokens.surfaceTint`)
- [x] Figma node `168:2972` bound; golden locks parity (documented D-368 deviations:
      plate omitted-in-frame but kept for backend; DOB / place / national-ID kept)
- [x] widget + unit + golden tests + E2E catalogue + this doc, same changeset
- [x] `flutter analyze` 0 errors / 0 warnings; full suite green; wire contract unchanged

## 9. Changelog
- **2026-06-30 (Phase 2, D-548):** moved to `lib/features/account/` (auth+profile
  consolidation); place-of-birth now reads the D-547 region API (`regionsProvider`)
  with the const list as offline fallback; mobile maxLength via the shared
  `MobileField`; validators delegate to the shared `lib/core/validation/` predicates.
  Behaviour + golden unchanged (page re-freezes in Phase 3).
- **2026-06-30 (clean-code D-546 freeze):** decomposed the screen (Slices A/B/C);
  wired `MaxWidthBody(560)` (was `ConstrainedBox(400)`); tokenised the sweep tint;
  added the golden `sign_up_visitor_168-2972.png` + this consolidated doc.
  **App-wide:** the gold CTA `textStyle` now carries the brand font family (theme
  `_accentButton` previously omitted it, so Arabic CTAs fell back off-brand) —
  corrected `session_detail` + `speaker_profile` goldens too. Behaviour unchanged.
- **D-374:** server-computed `profileComplete` routes incomplete accounts here.
- **D-371/D-373/D-375:** plate field; nationality drives the document path; lookup
  inline retry; image-required (camera-only) for men + server face-gate.
- **D-368:** rebuilt to the KSA frame 168:2972. **D-332:** save moved to interests.
