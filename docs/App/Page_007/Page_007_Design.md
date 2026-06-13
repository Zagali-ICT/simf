# Page 007 — Design (إنشاء حساب · Sign up — profile data)

Flutter screen design — layout, components, states, RTL, localization. Behaviour is
in [Page_007_Function.md](Page_007_Function.md); rules in
[Page_007_Logic.md](Page_007_Logic.md); contract in [Page_007_API.md](Page_007_API.md).

> Last updated: 2026-06-13 — as-built conformance pass (D-368 KSA rebuild;
> D-371/D-373/D-375 amendments). Source:
> `src/Mobile/simf_app/lib/features/profile/sign_up_visitor_screen.dart`.

> **Reworked (D-332).** Data form only (mockup 05). The interests grid moved to
> [Page 007‑01](../Page_007-01/README.md); this screen ends with **Next**.

> **As-built (KSA-Project redesign — D-368, Figma 168:2972, + D-371/D-373):** the
> login-style navy header (`SimfLogo` 44 + forum name, back chevron + the
> **wired globe language toggle**, top row forced LTR) over a rotated decorative
> sweep, then the beige `cardBeige` card (max-width 400, radius 4, padding 24)
> holding the whole form — **إنشاء ملف شخصى** card head with the avatar mark;
> the visitor/other and Iqama/passport pickers as the design's **beige segmented
> tabs** (`_BeigeTabs` — **selected = white pill / navy text** per the D-373
> owner fix, unselected = container beige / white text); gender as two white
> **radio pills** (18 px gold ring, gold dot when picked); all inputs in the
> login field language (12-grey label, 48 px `beigeBorder`-bordered transparent
> input, radius 4, gold focus); the bordered **إرفاق ملف** attach box (56 px,
> plus mark; thumbnail + name + إزالة once attached); the underlined
> **الموافقة على الشروط والأحكام؟** link → Page 009; the gold **التالي**.
> **Logic byte-identical to the contract** (lookups, prefill, typeahead, Luhn,
> draft → 007-01 — no API write here).
> **Design deltas vs the frame:** (1) ~~the frame's "رقم اللوحة (اختياري)" has
> **no backend field** and is not rendered~~ — **superseded (D-371 C6, BUILT):**
> the owner mandated the plate field; it has the additive backend column and
> **is rendered** (optional, Saudi-standard validated, last input before the
> attach box per D-373). (2) Date of birth and place of birth are **kept**
> (API-required) in the same styling although the frame omits them. (3) The
> frame's Saudi-national switch is **gone (D-373)** — `isSaudi` derives from the
> **searchable nationality picker** (SA → national-ID field, else the
> Iqama/passport tabs). The old screen is parked in `lib/features/_legacy_mockup/`.
> The attach box is a **camera-only capture** with the C7 face-check rules (D-371).

## Layout (as-built)
```
┌────────────────────────────────────────────┐
│ navy surface + rotated sweep                │
│ [‹ back]                        [🌐 globe]  │  forced-LTR row
│        (logo 44)  SIMF forum name           │
├─[ beige card — radius 4, pad 24 ]──────────┤
│  إنشاء ملف شخصى                     (👤)    │  card head
│  [ زائر ▣ | أخرى ]                          │  beige tabs → filters ProfileType
│  التصنيف ▼ (Other only — required)          │  (D-375 loading/retry states)
│  الاسم الكامل (بالعربية)   [_______]        │
│  الاسم الكامل (بالإنجليزية) [_______]       │
│  الجنس   ( ذكر ◉ )  ( أنثى ○ )              │  white radio pills, default ذكر
│  الجهة / المنظمة  [🔍 typeahead] (required) │
│  المسمى الوظيفي (اختياري)  [_______]        │
│  الجنسية ▼ (searchable sheet, default SA)   │
│  └ SA → رقم الهوية الوطنية [__________]     │  conditional (derived from nationality)
│  └ else → [ الإقامة | جواز السفر ] + رقم    │
│  رقم الجوال (اختياري)  [_______]            │  Saudi or international — one field
│  تاريخ الميلاد  [— ▾📅]  (≥18, required)    │
│  مكان الميلاد (اختياري)  [_______]          │
│  رقم اللوحة (اختياري)  [_______]            │  C6 (D-371) — last input
│  المرفقات … [ إرفاق ملف ⊕ ] (camera-only)   │  C7 — mandatory for men
│        الموافقة على الشروط والأحكام؟         │  underlined link → Page 009
│            [  التالي  ]                      │  gold → Page 007‑01 (interests)
└────────────────────────────────────────────┘
```

## Components
| Component | Bound to | Notes |
|-----------|----------|-------|
| **Type tabs** (`_BeigeTabs`) | client-only `isVisitor` (bool) | زائر / أخرى; switching re-queries the ProfileType lookup; not sent to the server |
| Profile-type dropdown | E4 `items[]` (`?isVisitor=false`) | rendered **only under Other** (C5 — Visitor auto-locks to "Normal", no picker); shows `nameArabic`/`name` by locale; required; D-375 loading/retry/empty states |
| Text fields | names / job title / place of birth / id numbers / plate | 12-grey `_FieldLabel` caption + 48 px bordered transparent input, radius 4; English name, mobile and plate are LTR; national-id uses the number keyboard, mobile the phone keyboard |
| Country picker | E3 `countries[]` | tap opens the **searchable bottom sheet** (`_CountrySearchSheet`, type-to-filter over AR/EN names, D-373); shows `nameArabic`/`name` by locale; value = `code`; default SA; the pick derives the document section |
| Document-type tabs (`_BeigeTabs`) | client-only Iqama/passport choice | non-Saudi only; switching clears the number field |
| Organisation typeahead | E6 `OrganisationItem[]` | debounced 350 ms `?search=&top=20`; top 8 results listed; subtitle = `city`; selected → label + مسح (clear); **required** (B3/D-221); D-375 spinner/retry states |
| Gender pills (`_RadioPill`) | `Gender` enum | ذكر / أنثى white pills, 18 px gold-ringed radio; **default Male** on an empty profile (D-373) — no unspecified option rendered |
| Date-of-birth field | `dateOfBirth` | InkWell + InputDecorator showing `—` or `yyyy-MM-dd`; `showDatePicker` range *today − 120y* … *today − 18y* (D-197) |
| Attach box | camera capture (C7) | 56 px bordered row, إرفاق ملف + plus icon; **camera-only** (`ImagePicker` camera source); on-device ML Kit face check (no-face → snackbar); attached → 40 px thumbnail + filename + إزالة; inline `idImageRequiredForMen` error for men after a blocked Next |
| Terms link | navigation | underlined الموافقة على الشروط والأحكام؟ → Page 009 (standalone read) |
| **Next** button | navigation | gold `FilledButton` التالي; on valid data carries the `SignUpProfileDraft` to Page 007‑01 — **no POST** |

## States
| State | Trigger | UI |
|-------|---------|----|
| **Loading** | screen open → pre-fill + three lookups in flight (concurrent) | full-screen centered gold `CircularProgressIndicator` |
| **Load error** | any initial read fails | `profileLoadError` text + retry button (re-runs all four reads) |
| **Ready** | reads returned, form pre-filled | editable form (defaults: Visitor / Male / SA on an empty profile) |
| **Picker fetching (D-375)** | ProfileType tab-switch fetch / organisation search in flight | inline spinner + `loadingLabel` |
| **Picker failed (D-375)** | that fetch fails | inline `lookupLoadError` + **retry** — never a silently missing control |
| **Empty search** | organisation search completes with `[]` | `organisationEmpty` row |
| **Validating** | Next tapped, client checks | inline field errors (incl. DOB / nationality / organisation / male-photo); input kept |
| **Advance** | data fields valid + Next | `pushNamed` **Page 007‑01** with the draft |

## RTL
Arabic is the primary locale: the card and fields mirror; the **top back/globe row
is deliberately forced LTR** (chevron left, globe right — frame parity, D-363
pattern). The English-name, mobile and plate inputs force LTR text direction. Each
lookup row carries its own AR/EN names, so labels switch with the app locale
without a re-fetch.

## Localization
All static labels come from `AppL10n` (AR/EN) — e.g. `createProfileTitle`
(إنشاء ملف شخصى), `signUpTypeVisitor`/`signUpTypeOther` (زائر/أخرى),
`documentTypeLabel`/`documentNumberLabel`, `attachmentsLabel`/`attachFileLabel`,
`termsAgreeQuestion`, `plateNumberLabel`, `nextLabel` (التالي) — and all data
labels come from the lookup rows. No hard-coded strings. The globe button toggles
AR ↔ EN and persists the choice (D-363 pattern).
