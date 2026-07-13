# Delegations (الوفود) — `/delegations` (app screen #21)

- **Route:** `/delegations` (route name `delegations`) — mockup screen **#21** (restored; removed in D-277).
- **Surface / audience:** Mobile App (Flutter) · Guest+ (public).
- **Auth:** **None** — `GET /app/delegations` is `AllowAnonymous` (same as the public booths / sponsors / speakers reads); reached from a home tile + the direct `/delegations` route.
- **Figma:** **1426:10771** (الوفود). **Clean-code freeze:** D-624 (2026-07-04). Originating build: D-499 (Figma `1426:10771`, screen #21 restored from D-277).

## Purpose

The public directory of the **invited countries'** delegations attending the
forum. A visitor — signed-in or guest — sees which countries are participating,
the total participant count, and, per country, who leads the delegation, when
they arrive and leave, and how many members the delegation has. The interactions
are **search**, **scroll**, and a **flag filter** — tapping an invited country's
flag in the stats strip narrows the list to that one country (a removable
active-filter chip clears it). The data is curated in the Control Panel on the
`Country` record; the screen renders the current state of those records.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `delegations_screen.dart` | State — the search `TextEditingController` + `_query`, the selected-flag `_selectedFlagCode` (with the `_onFlagTap` toggle + clear), the `delegationsProvider` watch, the `onRefresh` invalidate, and the loading / error / data branch dispatch inside `SimfPageShell`. |
| `widgets/delegations_body.dart` (`DelegationsBody` + `_ActiveFilterChip`) | The loaded list — the stats strip, the shared `SimfFilterSearchField`, the active-filter chip (when a flag is selected), then the per-country cards filtered by **both** the flag selection and the search (or the empty / no-results `SimfEmptyState`). |
| `widgets/delegations_stats_strip.dart` (`DelegationsStatsStrip` + `_FlagSpot` + `_GridPainter` + `_Stat`) | The navy header strip (Figma 1426:10781): the faint gold grid, the scattered invited-country flags (each a `_FlagSpot` tap target that filters the list; the selected flag is ringed), and the two big-gold stats (participating countries left / total participants right). |
| `widgets/delegation_card.dart` (`DelegationCard` + `_FlagBox`/`_HeadAvatar`/`_HeadChip`/`_MemberChip`/`_DateGroup`) | One country card (Figma 1426:10838): the flag box + bilingual country name, the head-of-delegation box (when set), and the bottom row (member chip + date range). |

The single-use leaf widgets (`_GridPainter`/`_Stat`, and the card's
`_FlagBox`/`_HeadAvatar`/`_HeadChip`/`_MemberChip`/`_DateGroup`) are **colocated
with their parent** per the booths / venue_map precedent (D-615/D-618) — cleaner
than one micro-file per tiny leaf; each file stays ≤400 lines (largest 316).

## Tokenisation (this freeze)

The four module-level raw `Color(0x..)` consts were removed:

- `_chipFill = Color(0x1AC2B8A2)` → the existing **`SimfTokens.beigeFill10`** (byte-identical).
- `_gridLine = Color(0x12C9A84C)` → new **`SimfTokens.goldFill7`** (gold 7% — the stats-strip grid).
- `_headBoxFill = Color(0x0FC9A84C)` → new **`SimfTokens.goldFill6`** (gold 6% — the head-box fill).
- `_headBoxBorder = Color(0x26C9A84C)` → new **`SimfTokens.goldBorder15`** (gold 15% — the head-box border).

Every new token carries the **exact same ARGB value**, so the swap is
render-preserving. (The screen's local filter-search field was already extracted
to the shared `SimfFilterSearchField` in D-613.)

## UI affordances

- **Header:** back chevron + centred title **الوفود**.
- **Stats strip:** `countryCount` (دولة مشاركة / Participating countries) + `totalParticipants` (إجمالي المشاركين / Total participants), over a faint gold grid with the invited-country flags scattered. Each scattered flag (first 8) is a **tap target** — tapping filters the list to that country and rings the flag in gold; tapping again clears.
- **Search box:** one filter input (hint "ابحث عن دولة أو وفد..."), filters cards client-side by country name (ar/en) and head name (ar/en). **Composes** with the flag filter.
- **Active-filter chip:** shown below the search box while a flag filter is active — the selected country's name + a close glyph (assistive label "عرض كل الدول" / "Show all countries"); tapping it clears the flag filter.
- **Country card (per `items[]`):** flag box + bilingual country name; the head-of-delegation box (gold avatar + `headName`/`headNameArabic` + `headTitle`, رئيس الوفد chip — shown **only** when a head is set); the bottom row — member chip (groups glyph leading, `memberCount`) + date range (clock glyph leading, `arrivalDate`→`departureDate`, when both set).

## CP-side management (where the data comes from)

Each card is a projection of a `Country` record curated on the Country Add/Edit
form (`/admin/countries`). An admin (1) marks the country invited
(`Country.IsInvited`), (2) sets `DelegationArrivalDate` / `DelegationDepartureDate`
(additive nullable columns, migration D499), and (3) picks the head of delegation
(`Country.HeadOfDelegationUserProfileId`, additive nullable `Guid` logical FK →
`UserProfile`, `SetNull`) from the country's active delegates (fed by
`GET /admin/countries/{id}/delegates`, gated `Countries.View`). Saved through the
existing `PUT /admin/countries/{id}` (`Countries.Edit`). CP reference doc:
`docs/pages/cp/admin-countries.md`.

The **member count** is **not** stored — it is **derived on read** from the active
delegate `UserProfile`s (`IsDelegate && IsActive`) whose `NationalityId` is the
country (per the D-157 "no duplicated data — resolve on read" rule).

## Data flow

```
Guest opens /delegations → GET /api/v1/app/delegations (anonymous)
  → select Country where IsInvited && IsActive
     → resolve head from Country.HeadOfDelegationUserProfileId (UserProfile)
     → count active delegates (IsDelegate && IsActive) by NationalityId
  → ApiResult<AppDelegations> → stats + cards render
```

`AppDelegations { countryCount, totalParticipants, items[] }`, each
`AppDelegationItem { countryId, countryCode, countryName, countryNameArabic,
headName?, headNameArabic?, headTitle?, arrivalDate?, departureDate?, memberCount }`.

## States

- **Loading:** a spinner while the GET is in flight.
- **Error:** `SimfErrorState` — "تعذر تحميل الوفود." / "Could not load delegations." on a network / 5xx failure; Retry re-runs the call. Wrapped in `SimfPullToRefresh`.
- **Empty:** `SimfEmptyState` (flag icon) — "لا توجد وفود بعد." / "No delegations yet." when no invited countries are returned; **no-results** — "لا توجد نتائج" when the search matches nothing.
- **Head omitted:** when a country has no `HeadOfDelegationUserProfileId`, the card renders without the head box.

## L4 Figma parity (frame 1426:10771)

The `delegations_1426-10771` golden **held without `--update`** after the
decomposition + tokenisation — the render is byte-identical to the frame-verified
D-499 build (the golden is that build's pixel-comparison artifact, annotated
throughout the widgets with node ids 1426:10781/10838/10840/10855/10862/10856), so
the 1426:10771 parity is preserved. The **flag-filter** addition keeps the golden
byte-identical in its default (unfiltered) state: each flag glyph stays at its
exact original pixel (the tap target's transparent padding is offset-compensated),
and the gold selection ring paints **only** on a selected flag — a state the
golden never renders.

## Level-F

Browse screen — every affordance wired: search filters client-side; the stats-strip
flags filter the list to one country (removable via the active-filter chip);
pull-to-refresh + retry re-fetch. Reads `GET /app/delegations` (anonymous). No
missing API. All curation is CP-side (see above).

## i18n + RTL

All strings localized (AR / EN): title, search hint, the two stat labels, the head
label رئيس الوفد, the active-filter clear label ("عرض كل الدول" / "Show all
countries"), the error and empty copy. Country + head names come from the record's
bilingual fields and switch with the locale. Under Arabic the header, stats strip,
search box, active-filter chip and cards mirror right-to-left (the member chip's
groups glyph and the date range's clock glyph each lead at the inline-start; the
active-filter chip aligns to the inline-start via `AlignmentDirectional`).

## Tests

`test/golden/delegations_golden_test.dart` (frame 1426:10771, @375×1075, ar) +
the delegations feature tests — including the **flag-filter** widget test
(`delegations_screen_test.dart`: tap a stats-strip flag → list narrows to that
country; the active-filter chip clears it). E2E:
`docs/tests/e2e/mobile-delegations.md` (`E2E-DEL-001..010`).

## Related decisions

- **D-624** (this clean-code freeze — decomposition + gold-alpha tokens).
- **D-499** (originating build — this screen + the public endpoint + the all-on-`Country` schema choice + additive migration D499). Related: **D-613** (the shared `SimfFilterSearchField` extracted from this screen + the summaries list), **D-157** (member count derived on read), **D-473** (delegate = visitor + `IsDelegate` + invited country), **D-277** (the earlier removal of screen #21).

---

_Last reviewed: 2026-07-13 — added the stats-strip **flag filter** (tap a country
flag to narrow the list; removable active-filter chip; composes with search;
default golden unchanged). Prior: 2026-07-04 (D-624 — clean-code freeze).
Originating doc D-499._
