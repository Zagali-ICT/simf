# Page 007‑01 — Design (اهتماماتي · Sign up — interests)

*Last updated: 2026-06-13 — conformance pass against the as-built KSA-Project screen (D-365).*

Flutter screen design — layout, components, states, RTL, localization. Behaviour is in
[Page_007-01_Function.md](Page_007-01_Function.md); rules in
[Page_007-01_Logic.md](Page_007-01_Logic.md); contract in [Page_007-01_API.md](Page_007-01_API.md).

> **As-built (KSA-Project redesign, 2026-06-11 — D-365, Figma 505:1083):**
> navy `navySurface` (`#102238`) surface + a decorative rotated sweep (28.28°,
> faint white tint, top-right); custom header band (back chevron + centred
> **اهتماماتي** title — no Material app bar); **اختر اهتماماتك** heading + the
> long helper copy in `beigeBorder` (`#C2B8A2`); a **two-column pill grid**
> (43 px rows, gaps 10×12 — gold `accent` (`#C9A84C`) fill when selected,
> `navyDeep` (`#192B41`) fill with a `#2A4066` border otherwise, 14-bold white
> labels, single-line ellipsis); the centred **n / 10** counter; the gold
> **متابعة** button pinned at the bottom. The draft/1–10/single-upsert/ID-image
> contract is unchanged; the old mockup screen is parked in
> `lib/features/_legacy_mockup/`.

## Layout (= Figma 505:1083)
```
┌────────────────────────────────────────────┐
│  ‹   اهتماماتي / My interests               │  custom header band (56 px)
├────────────────────────────────────────────┤
│         اختر اهتماماتك / Choose your interests│  24 semibold, white
│   «اختر ما لا يقل عن واحد وبحد أقصى 10 …»     │  helper, beigeBorder
│                                             │
│   ╭───────────────╮  ╭───────────────╮      │
│   │ pill          │  │ pill ● gold   │      │  two-column pill grid
│   ╰───────────────╯  ╰───────────────╯      │  (43 px rows, scrolls)
│   ╭───────────────╮  ╭───────────────╮      │
│   │ pill          │  │ pill          │      │
│   ╰───────────────╯  ╰───────────────╯      │
│                                             │
│              n / 10 مُختارة                  │  counter, beigeBorder
│  [           متابعة / Continue           ]  │  gold, pinned → POST upsert → Page 010
└────────────────────────────────────────────┘
```

## Components
| Component | Bound to | Notes |
|-----------|----------|-------|
| Back chevron | navigation | pops to Page 007 (fallback `/sign-up/visitor`); disabled while submitting; icon forced LTR |
| Interest pills | `interests[]` (E1) | multi-select `InkWell` pills in a 2-column grid; **selected = gold `accent` fill** (no check icon), unselected = `navyDeep` + `#2A4066` border; ordered by `displayOrder`; tap locked while submitting |
| Counter | derived | live «n / 10 مُختارة» (`interestsCounter`); constant `beigeBorder` styling |
| Inline error | `_submitError` | the upsert's `ApiFailure.message` in `danger` red under the counter |
| **متابعة** button | the upsert | full-width gold `FilledButton`; disabled while 0 selected or submitting; in-button spinner while the POST is in flight |

## States
| State | Trigger | UI |
|-------|---------|----|
| **No draft** | direct deep link without the Page-007 draft | recover state: تعذر تحميل النموذج + a button to the Page-007 form; no API call |
| **Loading** | open (with draft) → `GET /app/account/interests` | centred gold `CircularProgressIndicator` |
| **Load error** | lookup `ApiFailure` | message + **إعادة المحاولة (Retry)** button |
| **Ready** | lookup returned | selectable pills (draft ids pre-selected); متابعة disabled until ≥ 1 |
| **Empty** | lookup returns `[]` | «لا توجد اهتمامات» text — never a blocking error |
| **Max reached** | tapping an 11th pill | snackbar «الحد الأقصى 10 اهتمامات»; selection unchanged |
| **Submitting** | متابعة → POST in flight | in-button spinner; pills, متابعة and Back locked |
| **Error** | POST `ApiFailure` | bilingual message inline under the counter; selection preserved; Back returns to Page 007 with the draft intact |
| **Success** | POST `ApiResult.Ok` | toast «تم حفظ الملف الشخصي» (or the upload-failed warning) → route to **Page 010** with `referenceNumber` as extra |

## RTL
Arabic is the primary locale: the two-column grid, heading, helper and counter mirror
with the locale direction; the متابعة button is full-width. The back-chevron **icon**
is pinned `TextDirection.ltr` so the glyph does not flip. Each interest row carries
`nameArabic` / `name`, so pill labels switch with the locale without a re-fetch.

## Localization
All copy from `AppL10n` (no hard-coded strings): `interestsTitle` (اهتماماتي / My
interests), `interestsChooseTitle` (اختر اهتماماتك / Choose your interests),
`interestsHelper`, `interestsCounter(n)` (n / 10 مُختارة / n / 10 selected),
`interestsEmpty`, `interestsMaxReached`, `continueLabel` (متابعة / Continue — the
design has no "Save" label), `profileSavedToast`, `idImageUploadFailed`,
`profileLoadError`, `retryLabel`, `signUpVisitorTitle`. Pill labels come from the
lookup rows.
