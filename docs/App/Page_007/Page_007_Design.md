# Page 007 — Design (إنشاء حساب · زائر · Sign up — visitor)

Flutter screen design — layout, components, states, RTL, localization. Behaviour is
in [Page_007_Function.md](Page_007_Function.md); rules in
[Page_007_Logic.md](Page_007_Logic.md); contract in [Page_007_API.md](Page_007_API.md).

## Layout
A single scrollable form screen with an inline **interests sub-step**:

```
┌────────────────────────────────────────────┐
│  AppBar — إنشاء حساب · زائر  / Sign up      │
├────────────────────────────────────────────┤
│  Section 1 — Personal                       │
│   • Arabic name        • English name       │
│   • Job title (optional)                    │
│   • Nationality      ▼ (country lookup)     │
│   • Is-Saudi toggle                         │
│     └ national id / iqama / passport (cond.)│
│   • Saudi mobile / international mobile      │
│   • Date of birth · Place of birth          │
│   • Gender           ▼                       │
│                                             │
│  Section 2 — Affiliation                    │
│   • الجهة / Organisation  (typeahead search)│
│   • Profile type        (card row)          │
│                                             │
│  Section 3 — Interests (sub-step, "008")    │
│   ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐       │
│   │ card │ │ card │ │ card │ │ card │  …     │
│   └──────┘ └──────┘ └──────┘ └──────┘       │
│   helper: pick 1–10 · counter "n/10"        │
│                                             │
│            [  Save / حفظ  ]                  │
└────────────────────────────────────────────┘
```

## Components
| Component | Bound to | Notes |
|-----------|----------|-------|
| Text fields | name / job title / mobiles / id numbers | AR field hints + EN labels |
| Country dropdown | E3 `countries[]` | shows `nameAr` (primary) + `nameEn`; value = `code` |
| Organisation typeahead | E6 `OrganisationPickerItem[]` | debounced search → `?search=&top=20`; value = `id`; subtitle = `city` |
| Profile-type cards | E4 `items[]` | each card tinted with `pageColor`; value = `id`; optional |
| Gender picker | `Gender` enum | Unspecified default |
| **Interest cards** | E5 `interests[]` | multi-select, **min 1 / max 10**; selected count shown; ordered by `displayOrder` |
| Save button | E2 upsert | disabled until valid + 1–10 interests |

## RTL
Arabic is the primary locale: the whole screen mirrors (fields right-aligned,
dropdown carets and the interests grid flow right-to-left). Each lookup row carries
its own `nameAr` / `nameEn`, so labels switch with the app locale without a re-fetch.

## States
| State | Trigger | UI |
|-------|---------|----|
| **Loading** | screen open → E1 + four lookups in flight | skeleton form; pickers show shimmer |
| **Ready** | lookups returned, form pre-filled from E1 | editable form; Save disabled until valid |
| **Empty lookup** | a lookup returns `[]` | that picker shows its empty state (e.g. "No organisations") — never a blocking error |
| **Validating** | Save tapped, client checks | inline field errors; focus first invalid field |
| **Submitting** | E2 POST in flight | Save shows spinner; form locked |
| **Error** | E2 returns `Validation.Failed` / 500 | map code → field/toast (AR+EN); form state preserved |
| **Success** | E2 `ApiResult.Ok` | profile marked complete → route to **wait-for-approval** |

## Localization
All static labels come from AR/EN resources; all data labels come from the lookup
rows (`nameAr` / `nameEn`, `name` / `nameArabic`). The interests helper text and the
"n/10" counter are localized strings. Toasts for the error codes in
[Page_007_API.md](Page_007_API.md) are bilingual.
