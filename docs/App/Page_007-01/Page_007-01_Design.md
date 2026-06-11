# Page 007‑01 — Design (اهتماماتي · Sign up — interests)

Flutter screen design — layout, components, states, RTL, localization. Behaviour is in
[Page_007-01_Function.md](Page_007-01_Function.md); rules in
[Page_007-01_Logic.md](Page_007-01_Logic.md); contract in [Page_007-01_API.md](Page_007-01_API.md).

> **New (D-332).** Mirrors mockup screen 5‑01.

## Layout (= mockup screen 5‑01)
```
┌────────────────────────────────────────────┐
│  AppBar — اهتماماتي / My interests          │
├────────────────────────────────────────────┤
│   اختر اهتماماتك / Choose your interests     │
│   pick 1–10 — used to suggest people/sessions│
│                                             │
│   ╭────────╮ ╭────────╮ ╭────────╮          │
│   │ chip   │ │ chip ● │ │ chip   │  …        │  selectable pills (wrap)
│   ╰────────╯ ╰────────╯ ╰────────╯          │
│                                             │
│                      n / 10 selected        │
│            [  Save / حفظ  ]                  │  → POST upsert → Confirmation
└────────────────────────────────────────────┘
```

## Components
| Component | Bound to | Notes |
|-----------|----------|-------|
| Interest pills | `interests[]` (E1) | multi-select chips; selected = accent fill + check; ordered by `displayOrder` |
| Counter | derived | live "n / 10"; turns muted at 10 |
| **Save** button | the upsert | disabled < 1 selected; spinner while the POST is in flight; form locked |

## States
| State | Trigger | UI |
|-------|---------|----|
| **Loading** | open → `GET /app/account/interests` | shimmer chips |
| **Ready** | lookup returned | selectable chips; Save disabled until ≥ 1 |
| **Empty** | lookup returns `[]` | empty state ("No interests yet") — never a blocking error |
| **Submitting** | Save → POST in flight | spinner; chips + Save locked |
| **Error** | POST `Validation.Failed` / 500 | bilingual field/toast; selection preserved; Back returns to Page 007 with state intact |
| **Success** | POST `ApiResult.Ok` | "please wait" → route to **Page 010** (Confirmation) |

## RTL
Arabic is the primary locale: the chip grid wraps right-to-left, the counter mirrors,
the Save button is full-width. Each interest row carries `nameAr` / `nameEn`, so labels
switch with the locale without a re-fetch.

## Localization
Title, helper, counter and Save come from AR/EN resources; chip labels come from the
lookup rows. No hard-coded strings.
