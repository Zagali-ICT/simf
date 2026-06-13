# Page 007 — إنشاء حساب · زائر · Sign up — profile data (مُعاد هيكلته · reworked D-332)

Per-page documentation folder. Everything about this app page lives here.

> Last updated: 2026-06-13 — as-built conformance pass (D-368 KSA redesign;
> includes the D-371/D-373/D-374/D-375 amendments).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_007_Function.md](Page_007_Function.md) | What the page does — elements, user steps, navigation, acceptance criteria |
| Logic | [Page_007_Logic.md](Page_007_Logic.md) | Business rules — auth gate, lookup sources, the Visitor/Other filter, validation, edge cases |
| API | [Page_007_API.md](Page_007_API.md) | The backend lookups + the pre-fill read that serve this page (the **save** lives on Page 007‑01) |
| Design | [Page_007_Design.md](Page_007_Design.md) | Flutter screen design — layout, components, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **05** (`Mockup.html`) — the profile **data** form |
| Design frame | **KSA-Project Figma 168:2972** (D-368 rebuild — supersedes the mockup visuals) |
| Route | `RouteNames.signUpVisitor` → `/sign-up/visitor` |
| Titles | AR **إنشاء حساب** · EN **Sign up — profile** · card head **إنشاء ملف شخصى / Create profile** |
| Section | 1 — Onboarding / account |
| Nature | **Profile data capture** — collect the registration fields, then advance to interests |
| App privilege | **AUTH-only** — any signed-in account; **no role / no permission code** (D7) |
| Status | **✅ Shipped as-built (D-368, 2026-06-11)** — rebuilt to the KSA frame 168:2972 in `lib/features/profile/sign_up_visitor_screen.dart`; the old mockup screen is parked in `lib/features/_legacy_mockup/`. The D-371 owner constraints (C4–C7) and the D-373/D-375 amendments are built in. |

## What changed (D-332, as-built D-368/D-371/D-373)
- **Step 3 of the corrected sign-up flow** (= mockup screen 05): Register (`Page_005`)
  → OTP (`Page_006`) → **this screen (data)** → **[Page 007‑01](../Page_007-01/README.md)
  (interests)** → single save → Confirmation (`Page_010`).
- **نوع التسجيل (Visitor / Other).** The first field — the design's beige segmented
  tabs, زائر / أخرى — the `ProfileType.IsForVisitor` split. It is **not a stored
  field**; it filters the ProfileType lookup
  (`GET /app/account/profile-types?isVisitor=true|false`). **C5 (D-371):** under
  **Visitor** the picker is hidden and the type auto-locks to the seeded
  **"Normal" (عادي)** row; under **Other** the picker shows and a pick is **required**.
- **Removed: the interests sub-step.** Interests are now their own screen
  ([Page 007‑01](../Page_007-01/README.md)) — this **reverses D12**.
- **The Save moved to Page 007‑01.** Because the API requires `interestIds` (1–10) on
  the single `POST /app/account/user-profile`, this screen collects the data and
  carries it (as a `SignUpProfileDraft` route extra) to Page 007‑01, which adds the
  interests and fires the one save. This screen ends with **التالي / Next**, not Save.
- **D-373:** the **"سعودي الجنسية" switch is removed** — `isSaudi` derives from the
  nationality pick (SA → national-ID path, else Iqama/Passport); defaults =
  Visitor + **Male** + **Saudi Arabia**; the country picker is **searchable**.

## Fields (as-built, in form order)
نوع التسجيل (Visitor/Other tabs) · التصنيف / ProfileType (Other only — required) ·
full name AR · full name EN · gender (ذكر / أنثى radio pills, default Male) ·
الجهة / Organisation (typeahead, **required** — B3/D-221) · job title (optional) ·
nationality (searchable sheet, default SA — drives the document path, D-373) ·
document (SA → National ID; else Iqama / Passport tabs + number) · mobile (one
conditional field — Saudi or international, optional, C4 shapes) · date of birth
(**≥ 18**, D-197) · place of birth (optional, D-163) · **plate number (optional,
C6/D-371)** · attachment (**camera-only** photo, mandatory for men — C7/D-371) ·
terms link → Page 009 · **التالي**.

## Sources of truth
`Mockup.html` (screen 05) · KSA-Project Figma frame 168:2972 (visual, D-368) ·
`SIMF_Screen_Guide_and_User_Journey` (Screen 05) · SIMF-MOB-API-001 (shared API
conventions + auth) · SIMF-MAA-001 (mobile architecture) · DECISIONS_LOG
D-046/D-049/D-050/D-163/D-186/D-190/D-197/D-220/D-221/**D-332**/**D-368**/
**D-371**/**D-373**/**D-374**/**D-375**.

> Per-page documentation structure (`docs/App/Page_NNN/`). The interests screen split
> out under [Page 007‑01](../Page_007-01/README.md) (D-332, reversing D12).
