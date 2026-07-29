# E2E test catalogue — `Speakers list` (`speakers`)

> **D-456 country flag:** the card avatar shows the real SpeakerPhoto (anchor
> fallback) with the **country flag on the top-left corner** (shared
> `CountryFlagBadge`, from `Speaker.CountryId`); the sub-line is the rank only.
> Tapping the card opens the profile (908:2110). Flag absent until a country is set.

> **D-453 frame re-verify (908:1744):** matched the frame's card pitch — the
> inter-card gap is now **16px** (card 60 + 16 = 76 pitch). Card structure
> (anchor/photo tile + name over rank + beige caret) unchanged.

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> public speakers list is an **already-built, anonymous** read (D-199): the list
> endpoint `GET /api/v1/app/speakers` returns the active speakers ordered for the
> mockup grid. The "login only" rule (D-269) applies **only** to the meeting
> request on the profile (20), not to this list. API implementation lives in
> [`tests/SIMF.Api.Tests/PublicSpeakersTests.cs`](../../../tests/SIMF.Api.Tests/PublicSpeakersTests.cs).
> The **Flutter screen is built (D-302)** — widget tests in
> `src/Mobile/simf_app/test/features/speakers/speakers_screen_test.dart` (cards,
> card→profile nav, empty, error→retry, avatar-URL wiring) + model tests in
> `…/speaker_models_test.dart`.
>
> **P4 speaker photo (2026-06-16):** the 44×44 gold-bordered avatar tile now
> renders the speaker's uploaded photo (the D-357 **SpeakerPhoto** asset, served
> anonymously at `GET /app/assets/SpeakerPhoto/{id}/image`), falling back to the
> gold **anchor** glyph while it loads or when no photo is uploaded (the route
> 404s) — so the list matches frame 908:1744 exactly in the no-photo state. No
> new endpoint/field/migration (D-357 reuse; the legacy `photoRelativePath` is
> not the byte source).

| | |
|--|--|
| **Page** | [`Page_019`](../../App/Page_019/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/speakers` (list, **anonymous**) · app screen #19 `RouteNames.speakers` → `/speakers` (guest+) |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **None** — the list is `AllowAnonymous` (guest+). An **Admin** token is used **only** to seed the speakers (and to soft-delete one). **No literal secrets** (admin TOTP via the `Get-Totp` helper). |
| **Last reviewed** | 2026-07-22 |

> **Figma parity (D-432):** the screen is re-skinned to the KSA-Project frame
> **908:1744 "Speakers"** — the navy shell with a centred header `المتحدثون`
> flanked by a circled back chevron (profile header pattern 908:2110), then a
> vertical list of navy `#192B41` cards on the beige `0.2px` hairline ([`KsaCard`]).
> Each card (908:1999) shows, in RTL: a small beige caret at the inline start,
> the white name (16/SemiBold) over the beige `rank · affiliation` line
> (12/Regular), and a **44×44 gold-bordered anchor tile** ([`_RoleTile`], 908:2004).
> **The anchor tile renders for EVERY speaker** — the host/speaker distinction is
> **per-session** (it lives on the session↔speaker join), so the host **star** is
> shown only on the **session detail**, never on this global list.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB019-001 | Anonymous caller gets the active speakers as `ApiResult<PublicSpeakers>` (`items`) | happy | P0 | authored ✓ (`PublicSpeakersTests`) |
| E2E-MOB019-002 | Items are ordered by `displayOrder` then `name` | happy | P0 | authored ✓ (`PublicSpeakersTests`) |
| E2E-MOB019-003 | Each card carries avatar (`photoRelativePath`), rank (`rank`) and name (`name` / `nameArabic`) | happy | P1 | authored ✓ (screen) |
| E2E-MOB019-004 | Tapping a card → Speaker profile (20) with that `id` | happy | P0 | authored ✓ (screen) |
| E2E-MOB019-005 | A soft-deleted speaker drops out of the list | edge | P1 | authored ✓ (`PublicSpeakersTests`) |
| E2E-MOB019-006 | No speakers → empty `items` → list placeholder | edge | P2 | authored ✓ (screen) |
| E2E-MOB019-007 | RTL render; rank/name right-to-left, avatar leading | i18n | P1 | authored ✓ (screen) |
| E2E-MOB019-008 | Re-skin: navy hairline `KsaCard`, beige caret, name over `rank · affiliation`, gold anchor tile (frame 908:1999) | happy | P1 | _to author_ |
| E2E-MOB019-009 | Anchor tile shows for EVERY speaker; no host star on this global list (D-432) | happy | P0 | _to author_ |
| E2E-MOB019-010 | Centred header `المتحدثون` flanked by circled back chevron + balancing spacer (908:2110) | happy | P2 | _to author_ |
| E2E-MOB019-011 | Card with no rank/affiliation drops the beige sub-line, keeps the name | edge | P2 | _to author_ |
| E2E-MOB019-012 | Arabic app renders the speaker's `rankArabic` when populated (CP-entered **or** Excel-imported) | i18n | P1 | _to author_ |
| E2E-MOB019-013 | Arabic app falls back to the English `rank` when `rankArabic` is blank — intended, not a bug | i18n | P1 | _to author_ |
| E2E-MOB019-014 | **The search field has an accessible name (BUG-012)** — the shared `SimfSearchField` exposes its placeholder as the field's own semantics label (and keeps it once the user types, when the placeholder is gone). One fix covers every search surface: speakers, exhibition/booths, delegations, agenda, session summaries, notifications, and the `SimfFilterSearchField` variant | a11y | P1 | authored ✓ (app `test/app/widgets/simf_search_field_semantics_test.dart`) |
| E2E-MOB019-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB019-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOB019-001 — Anonymous list of active speakers

```gherkin
Feature: Speakers list (المتحدثون)
  As any visitor (guest or signed-in)
  I want to see every active speaker
  So that I can open a speaker's profile

Scenario: The list returns the active speakers without a token
  Given two active speakers have been seeded by an admin
  When an anonymous client calls GET /api/v1/app/speakers with no token
  Then the response is 200
  And the body is ApiResult<PublicSpeakers> with items holding both speakers
  And each item carries id, name, nameArabic, rank, countryNameEn, countryNameAr, photoRelativePath and displayOrder
```

**Evidence:** `PublicSpeakersTests` (the anonymous list returns the seeded active speakers) — `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` (green).

### E2E-MOB019-002 — Ordering

```gherkin
Scenario: The speakers come back in display order
  Given speaker "Beta" has displayOrder 2 and speaker "Alpha" has displayOrder 1
  And two speakers share displayOrder 3 with names "Mike" and "Adam"
  When the list is fetched
  Then items are ordered by displayOrder ascending
  And ties are broken by name ascending (so "Adam" precedes "Mike")
```

**Evidence:** `PublicSpeakersTests` (items ordered by displayOrder then name) — `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` (green).

### E2E-MOB019-003 — Card content

```gherkin
Scenario: Each speaker card shows the avatar, rank and name
  Given the list returned a speaker with rank "القبطان البحري", a name and a photoRelativePath
  When the grid renders the sp-card
  Then the card shows the avatar from photoRelativePath (the ⚓/★ avatar placeholder when absent)
  And the rank line shows "القبطان البحري"
  And the name shows below the rank with a "المزيد"/More affordance
```

### E2E-MOB019-004 — Open a profile

```gherkin
Scenario: Tapping a card opens that speaker's profile
  Given the list shows a speaker whose id is the seeded speakerId
  When the user taps the card (or "المزيد"/More)
  Then the Speaker profile (20) opens at /speakers/:speakerId with that speakerId
```

### E2E-MOB019-005 — Soft-deleted speaker drops out

```gherkin
Scenario: A soft-deleted speaker is not listed
  Given two active speakers are seeded
  And an admin soft-deletes one of them
  When the anonymous list is fetched again
  Then items holds only the remaining active speaker
  And the soft-deleted speaker does not appear
```

**Evidence:** `PublicSpeakersTests` (a soft-deleted speaker is excluded from the list) — `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` (green).

### E2E-MOB019-006 — Empty list

```gherkin
Scenario: No speakers shows the empty-list placeholder
  Given no active speakers exist
  When the anonymous list is fetched
  Then items is empty
  And the screen shows the "no speakers yet" placeholder rather than an error
```

### E2E-MOB019-007 — RTL render

```gherkin
Scenario: The speakers grid renders right-to-left in Arabic
  Given the device locale is Arabic
  When the speakers list renders
  Then the sp-list and each sp-card are right-to-left
  And the avatar leads and the rank/name lines read right-to-left
  And the "المزيد"/More affordance sits at the natural RTL end of the card
```

### E2E-MOB019-008 — Re-skinned navy hairline card (frame 908:1999)

```gherkin
Scenario: A speaker card renders the Figma navy hairline chrome
  Given the list returned a speaker named "محمد العتيبي" with rank "القبطان البحري" and country "السعودية"
  When the المتحدثون list draws that speaker's card
  Then the card is a navy "#192B41" KsaCard on the beige 0.2px hairline
  And a small beige caret (ic_caret_left) sits at the inline-start edge of the card
  And the white name "محمد العتيبي" (16/SemiBold) shows above the beige line "القبطان البحري · السعودية" (12/Regular)
  And a 44×44 gold-bordered tile sits at the inline-end edge of the card
```

### E2E-MOB019-009 — Anchor tile for every speaker, no host star on the list (D-432)

```gherkin
Scenario: The global list shows the anchor tile for every speaker and never the host star
  Given three active speakers are seeded, one of whom hosts a session
  When the المتحدثون list renders all three cards
  Then every card's role tile is the gold-bordered anchor tile (Icons.anchor, accent @ 15% fill)
  And no card shows a host star glyph
  And the host/المضيف star appears only on the session detail, not on this global list
```

### E2E-MOB019-010 — Centred header with circled back chevron (frame 908:2110)

```gherkin
Scenario: The header keeps the title optically centred under the navy shell
  Given the speakers list is open on the navy KsaPage
  When the header renders
  Then the centred title reads "المتحدثون" (English: "Speakers")
  And a circled back chevron (KsaBackButton) sits at the leading edge
  And a 42×42 spacer balances it at the trailing edge so the title stays centred
  When the back chevron is tapped
  Then ksaBackOrHome navigates back (or home when there is no back stack)
```

### E2E-MOB019-011 — Card with no rank or affiliation

```gherkin
Scenario: A speaker with no rank and no country drops the sub-line
  Given the list returned a speaker whose rank is null/blank and whose country is null
  When the card renders
  Then the white name shows on its own
  And no beige rank·affiliation sub-line is drawn
  And the gold anchor tile and the beige caret still render
```

### E2E-MOB019-012 — Arabic rank renders under the Arabic app

```gherkin
Scenario: The card shows the Arabic rank when rankArabic is populated
  Given an active speaker whose rank is "Captain" and whose rankArabic is "القبطان البحري"
  And the speaker was created either via the CP add/edit form OR the Speakers Excel import (the "RankArabic" workbook column)
  And the device locale is Arabic
  When the المتحدثون list renders that speaker's card
  Then the beige rank sub-line shows the Arabic rank "القبطان البحري"
  And the English "Captain" is not shown under the Arabic locale
  # localizedRank(isArabic:true) = _pickOpt(rankArabic, rank) → rankArabic when non-empty
```

### E2E-MOB019-013 — English fallback when rankArabic is blank (intended, not a bug)

```gherkin
Scenario: The card falls back to the English rank when rankArabic is blank
  Given an active speaker whose rank is "Commander" and whose rankArabic is null/blank
  And the device locale is Arabic
  When the المتحدثون list renders that speaker's card
  Then the beige rank sub-line shows the English rank "Commander"
  And this English fallback is INTENDED behaviour — the Arabic app shows the English rank only because no Arabic rank was provided (_pickOpt returns rank when rankArabic is empty)
  # History: before the importer fix, Excel-created speakers ALWAYS hit this fallback
  # because the Speakers import bound only the English "Rank" column and dropped the
  # Arabic rank (RankArabic=null); the CP form always persisted rankArabic. The importer
  # now maps the "RankArabic" column too, so a populated Arabic rank survives the Excel
  # path (E2E-MOB019-012) and this fallback fires only when the Arabic rank is genuinely absent.
```

---

_Last reviewed:_ `2026-07-22` by `SIMF Team` — added E2E-MOB019-012/013: the Arabic
app renders `rankArabic` when populated (CP **or** Excel import) and intentionally
falls back to the English `rank` when it is blank. Documents the Speakers Excel
importer fix (the `RankArabic` column now round-trips; previously Excel-created
speakers landed with `rankArabic=null`). No app render change — the render was
already correct.

_Last reviewed:_ `2026-07-26` by `SIMF Team` — BUG-012: the shared search field now
exposes its placeholder as the field's accessible name (E2E-MOB019-014).

_Prior review:_ `2026-06-16` by `SIMF Team`.
