# E2E test catalogue — `Booths` (`booths`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> booth reads are already built + anonymous (D-199 / D-230); API tests in
> `tests/SIMF.Api.Tests/PublicBoothsTests.cs`. The **Flutter screen is built
> (D-304)** and widget-tested in
> `src/Mobile/simf_app/test/features/booths/booths_screen_test.dart` (list,
> tap→detail sheet, empty, error→retry). It reuses the venue-map booth models +
> `VenueMapRepository` (same wire contract, no duplicate model).
>
> **Figma re-skin (D-432):** the page now matches the KSA-Project frame
> **922:2458 "Halls"** — navy shell + centred `الأجنحة` header, a bordered
> `الأجنحة` search field, and one exhibitor card per booth (company header +
> gold code pill + deep-navy hall box + gold guide-me CTA). Newly wired sections,
> all carried on `GET /app/booths` (server resolves the officer **Contact-first**):
> the **booth-officer row** (gold name over the fixed `المسؤول في الجناح` role
> beside a gold RS-style initials tile), the **email + phone contact boxes**, and
> the **real hall display name** in the hall box (falls back sector → generic
> `قاعة المعرض`). A **client-side search filter** (name/exhibitor/sector/code)
> with a distinct no-match state was added. Officer/contact/hall rows render only
> when the wire actually carries that data — never invented.
>
> **P6 (D-440):** the company logo tile now renders the exhibitor's **real
> `CompanyLogo`** (D-357) at `{base}/app/assets/CompanyLogo/{exhibitorContactId}/image`.
> The booth wire gained an append-only `exhibitorContactId` (the exhibitor's
> Contact id = the logo owner, resolved server-side via
> `Booth.ExhibitorId → Exhibitor.ContactId`); the tile falls back to the booth
> initials when the exhibitor has no linked Contact (or the load fails).

| | |
|--|--|
| **Page** | [`Page_022`](../../App/Page_022/README.md) |
| **Route** | `GET /api/v1/app/booths` · `/app/booths/{id}` · app screen #22 `/booths` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **None** — both reads are `AllowAnonymous` (a guest sees the booths). |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB022-001 | Guest loads the booths list (name · exhibitor · sector · code) | happy | P0 | authored ✓ (screen `lists the booths`) |
| E2E-MOB022-002 | Tapping a booth opens the sheet + lazy description (`/booths/{id}`) | happy | P0 | authored ✓ (screen `tapping a booth opens the detail sheet`) |
| E2E-MOB022-003 | Empty list → empty state | edge | P1 | authored ✓ (screen `empty list shows the empty state`) |
| E2E-MOB022-004 | A read failure → error + Retry that re-fetches | resilience | P0 | authored ✓ (screen `error shows retry, which re-fetches`) |
| E2E-MOB022-005 | Booth detail 404 → keep the summary, drop the description | edge | P2 | covered (sheet `localizedDescription` null → omitted; mirrors venue-map L-8) |
| E2E-MOB022-006 | Booth-officer row (gold name + `المسؤول في الجناح` + RS tile) | happy | P1 | _to author (D-432)_ |
| E2E-MOB022-007 | Email + phone contact boxes (both / one-only / none) | happy | P1 | _to author (D-432)_ |
| E2E-MOB022-008 | Hall box shows real hall name → sector → generic fallback | happy | P1 | _to author (D-432)_ |
| E2E-MOB022-009 | Search filter narrows the list to the typed term | happy | P0 | _to author (D-432)_ |
| E2E-MOB022-010 | Search with no match → `لا توجد أجنحة مطابقة` state | edge | P1 | _to author (D-432)_ |
| E2E-MOB022-011 | P6 — booth logo wired to the D-357 CompanyLogo route via `exhibitorContactId`; initials when unlinked (D-440) | display | P1 | authored ✓ (screen `P6 — a booth with a linked exhibitor wires the CompanyLogo route` + `…no linked exhibitor shows no logo image`); API `PublicBoothsTests.Public_booth_carries_the_exhibitor_contact_id_for_the_logo` |

## Scenarios

### E2E-MOB022-001 — Guest loads the booths

```gherkin
Feature: Booths (exhibition)
  As a guest (signed out)
  I want the list of exhibitor booths
  So that I can find an exhibitor

Scenario: The booths render without a token
  When the app calls GET /api/v1/app/booths
  Then it returns 200 with the active booths
  And each card shows the name, exhibitor, sector and the booth code
```

**Evidence:** screen test `lists the booths`; API `PublicBoothsTests`.

### E2E-MOB022-002 — Booth detail sheet

```gherkin
Scenario: Tapping a booth loads its description
  When the visitor taps a booth card
  Then a bottom sheet shows the name + exhibitor/sector
  And GET /api/v1/app/booths/{id} fills the description
```

**Evidence:** screen test `tapping a booth opens the detail sheet`.

### E2E-MOB022-003 — Empty / E2E-MOB022-004 — Error+retry / E2E-MOB022-005 — Detail 404

```gherkin
Scenario: No booths shows the empty state
  Given GET /api/v1/app/booths returns an empty list
  Then the screen shows the "No booths" placeholder

Scenario: A failed read offers a retry
  Given the booths read fails
  Then an error + Retry are shown, and Retry re-runs the read

Scenario: A booth detail 404 keeps the summary
  Given the detail call 404s
  Then the sheet keeps name/exhibitor/sector and shows no description
```

**Evidence:** screen tests `empty list shows the empty state`,
`error shows retry, which re-fetches`; the 404-keeps-summary path mirrors the
venue-map booth sheet (D-298).

### E2E-MOB022-006 — Booth-officer row (Figma 922:2800, D-432)

```gherkin
Feature: Booths — booth-officer row
  As a guest browsing the exhibition
  I want each booth to show its responsible officer
  So that I know who to ask about that exhibitor

Scenario: A booth carrying an officer shows the officer row
  Given GET /api/v1/app/booths returns a booth "بحرية" whose
    officerName is "Rana Saleh" (server resolved it Contact-first)
  When the guest opens /booths
  Then that booth card shows "Rana Saleh" in gold
  And under it the fixed role label "المسؤول في الجناح" (EN: "Booth officer")
  And a gold square initials tile reading "RS" beside the name

Scenario: A booth with no officer omits the row entirely
  Given a booth whose officerName is null/blank
  Then no officer name, no "المسؤول في الجناح" label and no initials tile render
  And the card still shows the company header, hall box and guide-me CTA
```

**Evidence:** card `_OfficerRow` renders only when `booth.officerName` is
non-blank; role label = `l10n.boothsOfficerRole` (`المسؤول في الجناح` /
`Booth officer`); initials via `_initials(name)`.

### E2E-MOB022-007 — Email + phone contact boxes (Figma 922:2810, D-432)

```gherkin
Feature: Booths — officer contact boxes

Scenario: Both contacts present shows two side-by-side boxes
  Given a booth whose officerEmail is "rana@bahria.sa"
    and officerPhone is "+966500000000"
  When the guest opens /booths
  Then the card shows a navy email box reading "rana@bahria.sa" (mail glyph)
  And beside it a navy phone box reading "+966500000000" (call glyph)
  And both texts render left-to-right even in Arabic

Scenario: Only one contact present shows only that box
  Given a booth with officerPhone but no officerEmail
  Then only the phone box renders (no empty email box)

Scenario: No contacts present omits the contact row
  Given a booth with neither officerEmail nor officerPhone
  Then no contact boxes render on that card
```

**Evidence:** `_ContactBoxes`/`_ContactBox` — gated on
`officerEmail`/`officerPhone` non-blank; each box is `textDirection: ltr`.

### E2E-MOB022-008 — Hall box display name + fallback chain (Figma 922:2798, D-432)

```gherkin
Feature: Booths — hall box label

Scenario: Real hall name is shown when on the wire
  Given a booth whose resolved hall display name is "HALL A"
  When the guest opens /booths
  Then the deep-navy hall box shows "HALL A" in gold (centred, single line)

Scenario: Falls back to the sector when no hall name
  Given a booth with no hall name but sector "الدفاع البحري"
  Then the hall box shows "الدفاع البحري"

Scenario: Falls back to the generic label when neither is present
  Given a booth with no hall name and no sector
  Then the hall box shows "قاعة المعرض" (EN: "Exhibition hall")
  And never an invented hall name (D11 / Page_015 L-6)
```

**Evidence:** `_HallRow` label =
`booth.localizedHallName(...) ?? booth.localizedSector(...) ?? l10n.boothsHallFallback`
(`قاعة المعرض` / `Exhibition hall`).

### E2E-MOB022-009 — Search filter narrows the list (Figma 922:2549, D-432)

```gherkin
Feature: Booths — client-side search

Scenario: Typing a term filters the visible cards
  Given the list shows booths "بحرية", "الدفاع الجوي" and "موانئ"
  And the search field hint reads "ابحث عن جناح أو شركة"
    (EN: "Search for a booth or company")
  When the guest types "بحرية" into the search field
  Then only the "بحرية" booth card remains
  And the filter also matches on exhibitor, sector and booth code
  And clearing the field restores the full list
```

**Evidence:** `_SearchField.onChanged` → `_query`; `_filtered` matches
name/exhibitor/sector/code, case-insensitive; hint = `l10n.boothsSearchHint`.

### E2E-MOB022-010 — Search with no match (Figma 922:2549, D-432)

```gherkin
Scenario: A query that matches nothing shows the no-match state
  Given the list has booths but none match "zzzz"
  When the guest types "zzzz"
  Then the cards disappear and a no-match placeholder shows
    "لا توجد أجنحة مطابقة" (EN: "No matching booths") with the search-off icon
  And the empty-list placeholder "لا توجد أجنحة" is NOT shown
    (that state is reserved for an actually empty booth list)
```

**Evidence:** `_buildBody` — `filtered.isEmpty` (with `_booths` non-empty) →
`KsaEmptyState(icon: search_off_outlined, message: l10n.boothsNoMatch)`
(`لا توجد أجنحة مطابقة` / `No matching booths`); distinct from `boothsEmpty`.

### E2E-MOB022-011 — Real company logo via D-357 (Figma 922:2793, D-440)

```gherkin
Feature: Booths — real company logo

Scenario: A booth whose exhibitor has a linked Contact shows the real logo
  Given a booth whose exhibitorContactId is "c1"
  When the guest opens /booths
  Then the logo tile builds an Image.network for
    {base}/app/assets/CompanyLogo/c1/image
  And on a failed/absent load it falls back to the booth initials

Scenario: A booth with no linked exhibitor shows the initials (no network image)
  Given a booth whose exhibitorContactId is null
  Then the logo tile renders the booth initials and no network image
```

**Evidence:** `_LogoTile` builds the CompanyLogo URL only when `exhibitorContactId`
is non-blank, else initials; the booth wire carries `exhibitorContactId` (resolved
server-side `Booth.ExhibitorId → Exhibitor.ContactId`). Screen tests + API
`Public_booth_carries_the_exhibitor_contact_id_for_the_logo`.

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
