# Delegations (الوفود) — `/delegations` (app screen #21)

- **Route:** `/delegations` (route name `delegations`) — mockup screen **#21** (restored; removed in D-277).
- **Surface / audience:** Mobile App (Flutter) · Guest+ (public).
- **Auth:** **None** — `GET /app/delegations` is `AllowAnonymous` (same as the public booths / sponsors / speakers reads); reached from a home tile + the direct `/delegations` route. The bearer token is **optional but meaningful**: when the app sends one, the response is filtered for that viewer (see "Own delegation excluded" below).
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
| `widgets/delegations_body.dart` (`DelegationsBody`) + `widgets/active_filter_chip.dart` (`ActiveFilterChip`) | The loaded list — the stats strip, the shared `SimfFilterSearchField`, the active-filter chip (when a flag is selected), then the per-country cards filtered by **both** the flag selection and the search (or the empty / no-results `SimfEmptyState`). |
| `widgets/delegations_stats_strip.dart` (`DelegationsStatsStrip` + `_GridPainter`) + `widgets/flag_spot.dart` (`FlagSpot`) + `widgets/delegations_stat.dart` (`DelegationsStat`) | The navy header strip (Figma 1426:10781): the faint gold grid, the scattered invited-country flags (each a `FlagSpot` tap target that filters the list; the selected flag is ringed), and the two big-gold stats (participating countries left / total participants right). |
| `widgets/delegation_card.dart` (`DelegationCard`) + `widgets/flag_box.dart` (`FlagBox`) | One country card (Figma 1426:10838): the flag box + the bilingual country name (the second-language line is suppressed when it would only repeat the title), tappable into the meeting-request sheet for an entitled account. |
| `widgets/delegation_meeting_request_sheet.dart` (`DelegationMeetingRequestSheet`) | The request sheet a tapped card opens. A thin wrapper (146 lines) over the shared `MeetingRequestForm<T>` in `speakers/widgets/` — it supplies the country options, the submit call and its own `_failureText`, which is what keeps A35 true (a server rejection shows the SERVER's bilingual reason, never the speaker copy). Its own fields live in `widgets/delegation_option_tile.dart` + `widgets/delegation_attendee_count_field.dart`. |

Only `_GridPainter` — a `CustomPainter`, not a widget — is still colocated with
its parent per the booths / venue_map precedent (D-615/D-618). The leaves that
WERE private (`_Stat`, `_ActiveFilterChip`, `_FlagSpot`, `_FlagBox`) are public
one-per-file widgets now: a private widget class is itself a convention finding,
and `FlagBox` in particular is reused by more than its original parent. Every
file stays ≤400 lines (largest 146).

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

The **host** country is never on this screen: Saudi Arabia is the OWNER of the
forum, not a visiting delegation, so it is deliberately not flagged
`Country.IsInvited` (D-768).

## Own delegation excluded (G2 — D-811, owner 2026-07-30)

A **signed-in** viewer never sees their **own** delegation. The country whose `Id`
equals the caller's `UserProfile.NationalityId` is dropped **server-side** in
`PublicDelegationService.GetAsync`, before the member-count group-by, so the two
stats (`countryCount`, `totalParticipants`) are computed over exactly the cards on
screen and the strip can never disagree with the list.

- The endpoint stays `AllowAnonymous`. A **guest** (no bearer token) has no `sub`
  claim, so nothing is excluded and the full list is returned.
- A signed-in caller with **no profile row** (an Admin / CP user) has no nationality
  to exclude — also the full list.
- Resolving the nationality is a **same-database** read: `UserProfile` and `Country`
  both live on `SimfAppDbContext`, so D-157 is not engaged (no cross-DB join).
- No output cache is configured on this endpoint, so per-caller filtering poisons
  no shared cache entry.
- The **delegation meeting-request sheet** uses this same feed as its target-country
  picker, so it inherits the exclusion with **no Flutter change** — a viewer can no
  longer pick their own country as the meeting target. The server-side self-target
  guard (`400`, "A delegation cannot request a meeting with itself." / "لا يمكن
  للوفد طلب اجتماع مع نفسه.") is unchanged and remains the authoritative check.

## Data flow

```
Viewer opens /delegations → GET /api/v1/app/delegations (anonymous; bearer sent when signed in)
  → resolve the caller's UserProfile.NationalityId from `sub` (null for a guest)
  → select Country where IsInvited && IsActive
     → and Id != the caller's nationality (skipped for a guest)   ← G2 / D-811
     → resolve head from Country.HeadOfDelegationUserProfileId (UserProfile)
     → count active delegates (IsDelegate && IsActive) by NationalityId
  → ApiResult<AppDelegations> → stats + cards render (stats derived from the filtered set)
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
country; the active-filter chip clears it). Backend: `DelegationsTests.cs` covers
the G2 (D-811) per-viewer exclusion — the signed-in viewer's own country is absent
while the guest read still contains it, and both stats track the filtered list.
E2E: `docs/tests/e2e/mobile-delegations.md` (`E2E-DEL-001..013`).

## Related decisions

- **D-811** (G2 — the list is per-viewer: the caller's own country is excluded server-side, stats recompute, guests see all).
- **D-624** (this clean-code freeze — decomposition + gold-alpha tokens).
- **D-499** (originating build — this screen + the public endpoint + the all-on-`Country` schema choice + additive migration D499). Related: **D-613** (the shared `SimfFilterSearchField` extracted from this screen + the summaries list), **D-157** (member count derived on read), **D-473** (delegate = visitor + `IsDelegate` + invited country), **D-768** (the host country is never `IsInvited`), **D-277** (the earlier removal of screen #21).

---

_Last reviewed: 2026-07-30 — G2 (D-811): a signed-in viewer no longer sees their
**own** delegation; the exclusion is server-side and both stats recompute over the
filtered set; guests still see the full list. Prior: 2026-07-13 — added the
stats-strip **flag filter** (tap a country flag to narrow the list; removable
active-filter chip; composes with search; default golden unchanged); 2026-07-04
(D-624 — clean-code freeze). Originating doc D-499._

## Changelog

- **2026-08-18 (delivery clean-code programme, structure only):** the delegation
  meeting-request sheet and the speaker one were 79% identical, so both now
  compose the one parameterised `MeetingRequestForm<T>` that lives in
  `speakers/widgets/`; each sheet keeps its own wrapper, options, submit and
  `_failureText`, so the A35 server-reason rule (`E2E-DELREQ-011/012`) is
  untouched. The sheet's attendee-count input became
  `widgets/delegation_attendee_count_field.dart`. Behaviour-preserving: the
  `delegations_1426-10771` golden held **without** `--update-goldens` and both
  sheets' widget tests passed unchanged.
  The **Structure** table above was re-read against the tree in the same pass and
  corrected — it still named the private `_ActiveFilterChip` / `_FlagSpot` /
  `_Stat` / `_FlagBox` leaves, which are public one-per-file widgets, and it did
  not list the request sheet at all. **Flagged, not changed:** the same table
  described the country card as carrying a head-of-delegation box and a bottom row
  of member chip + date range; `DelegationCard` renders neither today (flag +
  country name only), which is a content question for the owner and not something
  a documentation pass should decide either way.
