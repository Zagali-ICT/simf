# Page 007 — Design (إنشاء حساب · Sign up — profile data)

Flutter screen design — layout, components, states, RTL, localization. Behaviour is
in [Page_007_Function.md](Page_007_Function.md); rules in
[Page_007_Logic.md](Page_007_Logic.md); contract in [Page_007_API.md](Page_007_API.md).

> **Reworked (D-332).** Data form only (mockup 05). The interests grid moved to
> [Page 007‑01](../Page_007-01/README.md); this screen ends with **Next**.

> **As-built (KSA-Project redesign, 2026-06-11 — D-368, Figma 168:2972):** the
> login-style navy header (`SimfLogo` 44 + forum name, back chevron + the
> **wired globe language toggle**) over the beige `cardBeige` card holding the
> whole form — **إنشاء ملف شخصى** card head with the avatar mark; the
> visitor/other and document-type pickers as the design's **beige segmented
> tabs** (unselected = white pill, selected = beige with white text); gender as
> two white **radio pills** (18 px gold ring, gold dot when picked); all inputs
> in the login field language (12-grey label, 48 px `beigeBorder`-bordered
> transparent input, radius 4); the bordered **إرفاق ملف** attach box (56 px,
> plus mark; thumbnail + remove once attached); the underlined
> **الموافقة على الشروط والأحكام؟** link → Page 009; the gold **التالي**.
> **Logic byte-identical** (lookups, prefill, typeahead, Luhn, draft → 007-01).
> **Design deltas (D-368):** the frame's "رقم اللوحة (اختياري)" has **no
> backend field** and is not rendered (flagged to the owner); date of birth,
> place of birth and the Saudi-national switch + national-ID path are **kept**
> (API-required) in the same styling although the frame omits them. The old
> screen is parked in `lib/features/_legacy_mockup/`.

## Layout (= mockup screen 05)
```
┌────────────────────────────────────────────┐
│  AppBar — إنشاء حساب / Sign up              │
├────────────────────────────────────────────┤
│  نوع التسجيل / Registration type            │
│   [ ● زائر / Visitor ] [ أخرى / Other ]     │  2 chips → filters ProfileType
│  التصنيف / ProfileType        ▼ (filtered)  │
│                                             │
│   • Arabic name        • English name       │
│   • Gender           ▼                       │
│   • الجهة / Organisation  (typeahead search)│
│   • Job title (optional)                    │
│   • Document: is-Saudi toggle               │
│     └ national id / iqama / passport (cond.)│
│   • Mobile (Saudi / international)           │
│   • Nationality      ▼ (country lookup)     │
│   • Date of birth (≥18) · Place of birth    │  (D-197 / D-163 — additive to mockup)
│   • المرفقات / ID attachment (optional)      │
│                                             │
│            [  Next / التالي  ]               │  → Page 007‑01 (interests)
└────────────────────────────────────────────┘
```

## Components
| Component | Bound to | Notes |
|-----------|----------|-------|
| **Type chips** | client-only `isVisitor` (bool) | زائر / أخرى; selecting one re-filters the ProfileType picker; not sent to the server |
| Profile-type cards | E4 `items[]` (`?isVisitor=`) | each card tinted with `pageColor`; value = `id`; optional |
| Text fields | name / job title / mobiles / id numbers | AR field hints + EN labels |
| Country dropdown | E3 `countries[]` | shows `nameArabic` (primary) + `name`; value = `code` |
| Organisation typeahead | E6 `OrganisationPickerItem[]` | debounced `?search=&top=20`; value = `id`; subtitle = `city` |
| Gender picker | `Gender` enum | Unspecified default |
| Date-of-birth picker | `dateOfBirth` | selectable range ends at *today − 18y* (D-197) |
| **Next** button | navigation | enabled when the required data fields are valid; carries form state to Page 007‑01 — **no POST** |

## States
| State | Trigger | UI |
|-------|---------|----|
| **Loading** | screen open → E1 + three lookups in flight | skeleton form; pickers show shimmer |
| **Ready** | lookups returned, form pre-filled from E1 | editable form; Next disabled until valid |
| **Empty lookup** | a lookup returns `[]` | that picker shows its empty state — never a blocking error |
| **Validating** | Next tapped, client checks | inline field errors; focus first invalid field |
| **Advance** | required fields valid + Next | navigate to **Page 007‑01** with the form state |

## RTL
Arabic is the primary locale: the whole screen mirrors (type chips, fields, dropdown
carets flow right-to-left). Each lookup row carries its own `nameAr` / `nameEn`, so
labels switch with the app locale without a re-fetch.

## Localization
All static labels (type chips, field labels, Next) come from AR/EN resources; all data
labels come from the lookup rows. No hard-coded strings.
