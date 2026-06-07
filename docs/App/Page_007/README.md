# Page 007 — إنشاء حساب · زائر · Sign up — profile data (مُعاد هيكلته · reworked D-332)

Per-page documentation folder. Everything about this app page lives here.

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
| Route | `RouteNames.signUpVisitor` → `/sign-up/visitor` |
| Titles | AR **إنشاء حساب** · EN **Sign up — profile** |
| Section | 1 — Onboarding / account |
| Nature | **Profile data capture** — collect the registration fields, then advance to interests |
| App privilege | **AUTH-only** — any signed-in account; **no role / no permission code** (D7) |
| Status | **🟠 Docs corrected (D-332) — Flutter rebuild pending.** The reworked screen drops the interests sub-step (now [Page 007‑01](../Page_007-01/README.md)) and adds the **نوع التسجيل (Visitor / Other)** field that filters the ProfileType picker. |

## What changed (D-332)
- **Step 3 of the corrected sign-up flow** (= mockup screen 05): Register (`Page_005`)
  → OTP (`Page_006`) → **this screen (data)** → **[Page 007‑01](../Page_007-01/README.md)
  (interests)** → single save → Confirmation (`Page_010`).
- **Added: نوع التسجيل (Visitor / Other).** The first field — 2 chips, زائر / أخرى —
  the `ProfileType.IsForVisitor` split. It is **not a stored field**; it only filters
  the ProfileType picker (`GET /app/account/profile-types?isVisitor=true|false`).
- **Removed: the interests sub-step.** Interests are now their own screen
  ([Page 007‑01](../Page_007-01/README.md)) — this **reverses D12**.
- **The Save moved to Page 007‑01.** Because the API requires `interestIds` (1–10) on
  the single `POST /app/account/user-profile`, this screen collects the data and
  carries it to Page 007‑01, which adds the interests and fires the one save. This
  screen ends with **Next**, not Save.

## Fields (= mockup 05, + the API-required additions)
نوع التسجيل (Visitor/Other) · التصنيف / ProfileType · full name AR · full name EN ·
gender · الجهة / Organisation (typeahead) · job title · document (is-Saudi → National
ID / Iqama / Passport) · mobile · nationality · ID attachment. **Additive to the
mockup, required by the API:** date of birth (**≥ 18**, D-197) + place of birth (D-163).

## Sources of truth
`Mockup.html` (visual, screen 05) · `SIMF_Screen_Guide_and_User_Journey` (Screen 05) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
DECISIONS_LOG D-046/D-049/D-050/D-163/D-186/D-190/D-197/D-220/D-221/**D-332**.

> Per-page documentation structure (`docs/App/Page_NNN/`). The interests screen split
> out under [Page 007‑01](../Page_007-01/README.md) (D-332, reversing D12).
