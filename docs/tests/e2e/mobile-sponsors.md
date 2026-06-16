# E2E test catalogue — `Sponsors` (`sponsors`)

> **Authority:** SIMF E2E template (D-133). The sponsors read is built + anonymous
> (D-199; API `tests/SIMF.Api.Tests/SponsorsTests.cs`). **Flutter screen built
> (D-305)** — widget tests in `src/Mobile/simf_app/test/features/sponsors/sponsors_screen_test.dart`.
>
> **Figma parity (D-432):** Page 023 was re-skinned to the KSA-Project frame
> **922:2824 "Shepherds"** on the shared navy shell — forced-LTR header, centred
> "الرعاة", right-aligned per-tier section label, sponsor cards 72-high with a
> square initials badge + forward chevron. The **first (strategic) tier** renders
> the **gold hero card** (gold fill, navy text, gold initials badge, navy chevron);
> **every later tier** renders the **navy premium card** (navyDeep fill, beige
> hairline, white text, navy badge with a gold edge, gold chevron). The card's
> secondary line now **prefers the authored bilingual tagline**
> (`tagline`/`taglineArabic`, e.g. "الراعي الاستراتيجي · …") and falls back to the
> website `url` only when no tagline is set.
>
> **P6 (D-440):** the layout is now **position-based, three bands** — group 0 → the
> gold **hero** card, the **last** group (when >1) → the compact **3-column logo
> grid** ("رعاة ذهبيون"), any group in between → the navy **premium** card. Each
> sponsor logo is the **real `SponsorLogo` asset** (D-357) at
> `{base}/app/assets/SponsorLogo/{id}/image`, with the acronym initials as the
> fallback (network-image loads fail in tests → initials).

| | |
|--|--|
| **Page** | [`Page_023`](../../App/Page_023/README.md) |
| **Route** | `GET /api/v1/app/sponsors` · app screen #23 `/sponsors` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB023-001 | Guest loads sponsors grouped by tier (section header per tier) | happy | P0 | authored ✓ (screen `renders tier headers + sponsor cards`) |
| E2E-MOB023-002 | Empty / all-empty groups → empty state | edge | P1 | authored ✓ (screen `empty groups show the empty state`) |
| E2E-MOB023-003 | Read failure → error state | resilience | P0 | authored ✓ (screen `error shows the error state`) |
| E2E-MOB023-004 | Sponsor name binds the real wire names (`nameEn`/`nameAr`, not name/nameArabic) | contract | P0 | covered (model `Sponsor.fromJson` + screen render) |
| E2E-MOB023-005 | Secondary line prefers the authored tagline over the website link | display | P0 | _to author (Figma 922:2824)_ |
| E2E-MOB023-006 | Secondary line falls back to the website `url` when no tagline is set | display | P1 | _to author (Figma 922:2824)_ |
| E2E-MOB023-007 | First (strategic) tier renders the gold hero card; later tiers render navy premium cards | i18n/visual | P0 | _to author (Figma 922:2824)_ |
| E2E-MOB023-008 | Card secondary line is omitted when both tagline and url are empty | edge | P2 | _to author (Figma 922:2824)_ |
| E2E-MOB023-009 | P6 — lowest tier renders as the 3-col logo grid; group 0 stays the hero card (D-440) | layout | P1 | authored ✓ (screen `P6 — the lowest tier renders as a logo grid`) |
| E2E-MOB023-010 | P6 — each sponsor logo is wired to the D-357 SponsorLogo route (hero, card + grid) (D-440) | display | P1 | authored ✓ (screen `P6 — each sponsor logo is wired to the D-357 SponsorLogo route`) |

## Scenarios

```gherkin
Scenario: Sponsors render grouped by tier without a token
  When the app calls GET /api/v1/app/sponsors
  Then it returns 200 with groups[] (tier, tierName, sponsors[])
  And the screen shows a section header per tier with the sponsor cards

Scenario: An empty programme shows the empty state
  Given no sponsors are configured
  Then the screen shows the "No sponsors" placeholder

Scenario: A failed read shows the error state
  Given the sponsors read fails
  Then the screen shows the error message
```

### E2E-MOB023-005 — Secondary line prefers the authored tagline over the website link

```gherkin
Scenario: An authored tagline wins over the website url under the sponsor name
  Given the app is in Arabic
  And the first tier "الرعاية الاستراتيجية" has a sponsor whose
    nameAr = "الشركة السعودية للصناعات العسكرية"
    taglineArabic = "الراعي الاستراتيجي · شريك التصنيع الدفاعي"
    url = "https://sami.com.sa"
  When the sponsors screen renders that card
  Then the name line reads "الشركة السعودية للصناعات العسكرية"
  And the secondary line under it reads "الراعي الاستراتيجي · شريك التصنيع الدفاعي"
  And the website url "https://sami.com.sa" is NOT shown as the secondary line
```

### E2E-MOB023-006 — Secondary line falls back to the website url when no tagline is set

```gherkin
Scenario: With no tagline the card shows the website link as the secondary line
  Given the app is in English
  And a sponsor has
    nameEn = "General Authority of Military Industries"
    tagline = null
    taglineArabic = null
    url = "https://gami.gov.sa"
  When the sponsors screen renders that card
  Then the name line reads "General Authority of Military Industries"
  And the secondary line under it reads "https://gami.gov.sa"
```

### E2E-MOB023-007 — Strategic tier = gold hero card; later tiers = navy premium cards

```gherkin
Scenario: The first tier is the gold hero card and later tiers are navy premium cards
  Given two non-empty tiers are returned in order
    | tier | tierName               |
    | 0    | الرعاية الاستراتيجية   |
    | 1    | رعاة بريميوم           |
  When the sponsors screen renders
  Then the first section label "الرعاية الاستراتيجية" is right-aligned above its cards
  And each card in the first tier is the gold hero card (gold fill, navy name text,
    gold initials badge with a navy edge, navy chevron)
  And the second section label "رعاة بريميوم" is right-aligned above its cards
  And each card in the second tier is the navy premium card (navyDeep fill, beige
    hairline, white name text, navy initials badge with a gold edge, gold chevron)
  And the initials badge shows the first two letters of the sponsor name (e.g. "SA")
```

### E2E-MOB023-008 — No secondary line when both tagline and url are empty

```gherkin
Scenario: A sponsor with neither tagline nor url shows only its name
  Given a sponsor has tagline = null, taglineArabic = null and url = null
  When the sponsors screen renders that card
  Then the card shows the sponsor name and the initials badge
  And no secondary line is rendered under the name
```

### E2E-MOB023-009 / 010 — P6 tiered layout + real logos (D-440)

```gherkin
Scenario: Three tiers render hero / cards / grid (lowest = grid)
  Given three non-empty tiers are returned (Strategic, Premium, Gold)
  When the sponsors screen renders
  Then the first tier renders the gold hero card
  And the middle tier renders navy premium cards
  And the lowest (last) tier renders a 3-column logo grid (one GridView)
  And every tier's sponsor name is shown

Scenario: Each sponsor logo loads the real SponsorLogo asset
  Given a sponsor with id "s1"
  When its card / grid tile renders
  Then it builds an Image.network for {base}/app/assets/SponsorLogo/s1/image
  And on a failed/absent load it falls back to the acronym initials
```

**Evidence:** `sponsors_screen_test.dart` (6) + `SponsorsTests` (API).
**Figma parity:** Page 023 matches KSA-Project frame **922:2824 "Shepherds"** (D-432);
real logos + the lowest-tier grid added P6 (D-440).

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
