# E2E test catalogue — Website speakers (`/speakers`)

| | |
|--|--|
| **Page** | [`web/speakers.md`](../../pages/web/speakers.md) |
| **Route** | `/speakers` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — the page is anonymous.** `Speakers.razor` calls `SimfPublicClient`, which carries **no bearer token**; `GET /api/v1/speakers` is `AllowAnonymous()`. A signed-in session is neither required nor read. (Seeding speakers for the golden path uses the Control Panel admin — `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper — to create active speakers over the admin API, then the public page is driven anonymously.) |
| **Figma** | KSA Maritime Forum — Speakers (Desktop AR), node `5840-26779` (card `5840:26994` + content `5840:26996`; event band `5840:26981`) |
| **Last reviewed** | 2026-07-15 |

> **What this page is.** `/speakers` (`Speakers.razor` + `Speakers.razor.cs`) is
> the Website's public, anonymous **speakers listing** (Figma `5840-26779`). It is
> a read-only static-SSR page built on the shared `LandingShell` chrome, with no
> CRUD, no modal, no form and no button. Two sections:
> 1. **Event page-title band** (`ln-pagehero`) — a white band with the SIMF
>    logo lockup + the theme heading (the page's single `<h1>`) + three meta rows
>    (date / time / venue), each with a **navy** line-icon. Static content from
>    `Speakers.Band.*` resx keys.
> 2. **Speaker grid** (`ln-spklist` → `ln-spklist-grid` of `ln-spkcard`) — live.
>    On load `OnInitializedAsync` does `SpeakerList = (await Api.GetSpeakersAsync())?.Items ?? []`
>    (`GET /api/v1/speakers`). A `null` result (failed envelope / unreachable
>    service) maps to an **empty list** — the page has **no separate error state**;
>    zero speakers renders the empty-state paragraph (`Speakers.Empty`).
>
> **Per card** (`ln-spkcard`): a ringed gradient photo box — the real portrait
> (`<img>`) when the speaker has one, else just the gradient backdrop; the name
> (`ln-spkcard__name`, navy ExtraBold 18px, Arabic-preferred in RTL); a **gold**
> role pill (`ln-spkcard__role`) **only when `Rank` is present**; a **gray**
> location row (`ln-spkcard__loc`, pin + country) **only when the country is present**.
>
> **Photo source** (`PhotoUrl`): `HasPhotoAsset` ⇒ the same-origin media route
> `/content/assets/SpeakerPhoto/{id}/image`; else the legacy `PhotoRelativePath`;
> else empty ⇒ the card shows its gradient backdrop only. Mirrors
> `SiteContentEndpoints.MapSpeakers`.
>
> **Bilingual fallback.** `DisplayName` is Arabic-preferred in RTL
> (`NameArabic` when present, else `Name`) and English-preferred in LTR;
> `LocationName` is the country only (`CountryNameAr` in RTL / `CountryNameEn` in
> LTR — the public API exposes country, not city). Chrome + band labels come from
> `IStringLocalizer<Strings>` (`Speakers.*` keys) and follow the `/culture` switch.
>
> **Recolorable icons.** The band + card icons reuse the DGA line-glyphs
> (`secondnav/*.svg`) via a shared `.ln-ico` alpha-`mask` painted with a token
> colour — navy on the band, gray on the card pin — so one asset serves both.
>
> **Auth model (Website, anonymous).** This is **not** a Control-Panel page: there
> is **no** `RequirePermission`, no `/not-permitted` gate, and **no**
> unauthenticated → `/login` redirect. The page is reachable by anyone, signed in
> or not. The "auth" row below asserts the *anonymous-by-design* contract (loads
> with no Authorization header, never 401s), not a redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WSPK-001 | Golden path — published speakers render as cards (ringed photo, name, gold role pill, gray country row) + the event band | happy | P0 | _to author_ |
| E2E-WSPK-002 | Card composition — role pill renders only when `Rank` present; location row only when country present | happy | P1 | _to author_ |
| E2E-WSPK-003 | Photo source — `HasPhotoAsset` ⇒ `/content/assets/SpeakerPhoto/{id}/image`; no asset + no legacy path ⇒ gradient-only card (no `<img>`) | happy | P1 | _to author_ |
| E2E-WSPK-004 | Ordering — cards render in the API's `DisplayOrder` (the server sorts; the page does not re-sort) | happy | P1 | _to author_ |
| E2E-WSPK-005 | Empty state — `GET /speakers` returns 0 rows ⇒ the `Speakers.Empty` paragraph, no cards, band still renders | happy | P1 | _to author_ |
| E2E-WSPK-006 | Failure degrades to empty — a failed / unreachable `GET /speakers` (client maps to null) ⇒ empty state, **no** error alert, no unhandled exception | resilience | P1 | _to author_ |
| E2E-WSPK-007 | Server 500 on `/speakers` ⇒ client maps to null ⇒ empty state, no unhandled exception in the console | resilience | P2 | _to author_ |
| E2E-WSPK-008 | Auth: anonymous-by-design — page loads with no Authorization header, no `/login` redirect, no `/not-permitted`; a signed-in session changes nothing | auth | P0 | _to author_ |
| E2E-WSPK-009 | RTL / Arabic render — band + cards render Arabic (Arabic-preferred name + country), page mirrors right-to-left, band icons navy + visible | i18n | P1 | _to author_ |
| E2E-WSPK-010 | Responsive — grid steps 4→3→2→1 columns (1440/1100/860/560), the band stacks below 860px, no horizontal overflow at 1440/1024/768/390 in both languages | responsive | P1 | _to author_ |
| E2E-WSPK-011 | Icons visible — band meta icons are navy (`#001640`) and the card location pin is gray (`#545555`), both rendered via the `.ln-ico` mask (never invisible white-on-white) | visual | P2 | _to author_ |
| E2E-WSPK-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WSPK-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | 2026-07-29 PASS (LTR+RTL) |

## Scenarios

### E2E-WSPK-001 — Golden path

```gherkin
Feature: Website speakers listing renders the published forum speakers
  As any visitor (anonymous or signed in)
  I want to see the forum's speakers and participants
  So that I can learn who is speaking and plan meetings

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And — to seed speakers — an Administrator has signed into the Control Panel
      (superadmin@simrsnf.com + TOTP via the Get-Totp helper)
  And via the admin API an active Speaker "Dr. Sarah Al-Otaibi" / "د. سارة العتيبي"
      exists with rank "Chief Scientist" / "كبيرة العلماء", country "Saudi Arabia" /
      "المملكة العربية السعودية", display order 0, and an uploaded SpeakerPhoto asset
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The published speakers render as cards under the event band
  When the browser opens /speakers
  Then a GET /api/v1/speakers request fires with NO Authorization header and returns 200
  And the ApiResult envelope is Success = true with Data.Items containing "Dr. Sarah Al-Otaibi"
  And an event band (section.ln-pagehero) renders with the single <h1> theme heading
      and three meta rows (date / time / venue) each with a navy icon
  And a speaker grid (.ln-spklist-grid) renders one .ln-spkcard
  And that card shows a ringed gradient photo box containing an <img> whose src is
      "/content/assets/SpeakerPhoto/{id}/image"
  And the card name reads "Dr. Sarah Al-Otaibi" (English base in the EN culture)
  And a gold role pill (.ln-spkcard__role) reads "Chief Scientist"
  And a gray location row (.ln-spkcard__loc) shows the pin + "Saudi Arabia"
  And the Speakers.Empty paragraph is NOT present
  And the page title is "Speakers — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-speakers-grid-before.png` (full grid + band)
- Screenshot after: `docs/screenshots/web-speakers-card-after.png` (one card in focus — photo ring, name, pill, country pin)
- Console errors: 0 expected
- Network: `GET /api/v1/speakers` returns 200 (ApiResult envelope, `Success = true`) with no Authorization header; the speaker photo `GET /content/assets/SpeakerPhoto/{id}/image` returns 200; no other `/api/v1/...` call fires
- Audit row: none — `/speakers` is a read-only anonymous page and writes no `OperationLog` / `RowAudit` row

### E2E-WSPK-002 — Card composition (conditional pill + location)

```gherkin
Scenario: The role pill and location row render only when their data is present
  Given an active speaker "Dr. Sarah Al-Otaibi" with rank "Chief Scientist" and country "Saudi Arabia"
  And an active speaker "Mike Constable" with NO rank and NO country
  When the browser opens /speakers
  Then two .ln-spkcard cards render
  And exactly one .ln-spkcard__role gold pill renders (on Dr. Sarah Al-Otaibi's card)
  And exactly one .ln-spkcard__loc location row renders (on Dr. Sarah Al-Otaibi's card)
  And the "Mike Constable" card still renders its name and its gradient photo box
  And no empty pill and no empty location row appear on the "Mike Constable" card
```

### E2E-WSPK-003 — Photo source

```gherkin
Scenario: HasPhotoAsset drives the same-origin media route; no asset falls back to the gradient
  Given an active speaker "Jane Roe" has an uploaded SpeakerPhoto asset (HasPhotoAsset = true)
  And an active speaker "Alex Stone" has NO photo asset and a blank PhotoRelativePath
  When the browser opens /speakers
  Then "Jane Roe"'s card renders an <img> with src "/content/assets/SpeakerPhoto/{Jane's id}/image"
  And that image request returns 200 (a real portrait, not a broken image)
  And "Alex Stone"'s card renders NO <img> inside .ln-spkcard__photo (the gradient backdrop only)
  And no card shows a broken-image icon

Scenario: A legacy PhotoRelativePath is used when there is no media asset
  Given an active speaker has HasPhotoAsset = false but a non-blank PhotoRelativePath
  When the browser opens /speakers
  Then that speaker's card <img> src equals the PhotoRelativePath value
```

### E2E-WSPK-004 — Ordering

```gherkin
Scenario: Cards render in the server's DisplayOrder
  Given three active speakers exist with display orders:
    | name              | displayOrder |
    | Bravo Speaker     | 1            |
    | Alpha Speaker     | 0            |
    | Charlie Speaker   | 2            |
  When the browser opens /speakers
  Then the cards render in order: "Alpha Speaker", "Bravo Speaker", "Charlie Speaker"
  And the page does NOT re-sort — it renders Data.Items in the order the API returns
      (GET /api/v1/speakers is ordered by DisplayOrder server-side)
```

### E2E-WSPK-005 — Empty state

```gherkin
Scenario: Zero active speakers renders the empty-state paragraph
  Given the database has no active Speaker rows
  And GET /api/v1/speakers returns 200 with Data.Items = []
  When the browser opens /speakers
  Then SpeakerList is empty and no .ln-spkcard renders
  And the .ln-spklist__empty paragraph renders the Speakers.Empty text
      ("Speakers will be announced soon." / "سيتم الإعلان عن المتحدثين قريباً.")
  And the event band still renders its theme heading + date/time/venue rows
  And no error alert appears (the page has no error state)
```

### E2E-WSPK-006 — Failure degrades to empty

```gherkin
Scenario: A failed speakers envelope leaves the grid empty, not errored
  Given GET /api/v1/speakers returns a body whose ApiResult envelope has Success = false
      (so SimfPublicClient.GetSpeakersAsync returns null)
  When the browser opens /speakers
  Then OnInitializedAsync maps the null result to an empty list (result?.Items ?? [])
  And the page renders the Speakers.Empty empty state (NOT an error alert — the page has none)
  And no .ln-spkcard renders
  And no unhandled exception reaches the browser console
```

> The page comment is explicit: a null/unreachable result "just leaves the grid
> empty" — there is deliberately **no** separate error alert on this page (unlike
> `/programme`).

### E2E-WSPK-007 — Server 500 on /speakers

```gherkin
Scenario: API 500 on the speakers list degrades to the empty state with no unhandled exception
  Given GET /api/v1/speakers returns HTTP 500 (e.g. the DB is down)
  When the browser opens /speakers
  Then SimfPublicClient reads the body and, on a failed/non-JSON envelope, returns null
      (it never throws for HttpRequestException / JsonException / timeout)
  And the page renders the Speakers.Empty empty state
  And no unhandled exception reaches the browser console
```

### E2E-WSPK-008 — Auth gate (anonymous by design)

```gherkin
Scenario: The page is reachable anonymously and never redirects to a login or not-permitted page
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /speakers directly
  Then the page renders (grid or empty state) WITHOUT redirecting to /login
  And the page does NOT redirect to /not-permitted (a Control-Panel concept, absent here)
  And the GET /api/v1/speakers request carries NO Authorization header
  And the API does not return 401/403 for the public read

Scenario: A signed-in session changes nothing on this page
  Given an Approved Visitor is signed in on the Website
  When they open /speakers
  Then the same anonymous public read fires (SimfPublicClient attaches no bearer token)
  And the rendered grid is identical to the anonymous view
```

> **Note (Website, not CP).** `/speakers` has **no** `RequirePermission` attribute
> and never routes to `/not-permitted`. `SimfPublicClient` attaches no bearer
> token; `ListPublicSpeakersEndpoint` is `AllowAnonymous()`. The "auth gate" for
> this page is the *absence* of any gate.

### E2E-WSPK-009 — RTL / Arabic render

```gherkin
Scenario: The listing mirrors and renders Arabic content under an Arabic UI culture
  Given the seeded speaker has NameArabic "د. سارة العتيبي", CountryNameAr "المملكة العربية السعودية"
  When the browser opens /speakers under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left
  And the band <h1> renders the Arabic theme text and the meta rows render the Arabic date/time/venue
  And the band meta icons render navy and visible (not invisible white-on-white)
  And the card name shows "د. سارة العتيبي" (DisplayName prefers the *Arabic value)
  And the card location shows "المملكة العربية السعودية"
  And no Latin text leaks where an Arabic value is present (the English base is used only when the *Arabic field is blank)

Scenario: Arabic fallback when an *Arabic field is blank
  Given a speaker has Name "Guest Speaker" but NameArabic is blank
  When the page renders under the Arabic culture
  Then the card name falls back to the English "Guest Speaker" (DisplayName returns the base when NameArabic is blank)
```

### E2E-WSPK-010 — Responsive

```gherkin
Scenario: The grid steps 4 → 3 → 2 → 1 columns and the band stacks, with no horizontal overflow
  Given at least 8 active speakers exist
  When the browser opens /speakers and the viewport width is set to each of 1440, 1100, 860, 560
  Then the .ln-spklist-grid renders 4 columns at ≥1101px, 3 at ≤1100px, 2 at ≤860px and 1 at ≤560px
  And below 860px the .ln-pagehero__inner stacks (flex-direction: column-reverse) — logo above the theme block
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

### E2E-WSPK-011 — Icons visible (recolorable mask)

```gherkin
Scenario: Band and card icons are painted the token colour, never invisible
  When the browser opens /speakers
  Then each .ln-pagehero__meta .ln-ico computes background-color rgb(0, 22, 64) (#001640 navy) and a non-none mask-image
  And each .ln-spkcard__loc .ln-ico computes background-color rgb(84, 85, 85) (#545555 gray) and a non-none mask-image
  And no meta/location icon renders as white-on-white (the DGA glyphs ship white for the dark nav — the mask repaints them)
```

**Evidence captured:**
- DOM check: `getComputedStyle('.ln-pagehero__meta .ln-ico').backgroundColor === 'rgb(0, 22, 64)'` and `.ln-spkcard__loc .ln-ico` === `'rgb(84, 85, 85)'`; both `maskImage !== 'none'`
- Regression guard for the fix in commit `9ccb9542` (band + card icons were previously invisible white-on-white)

---

## Implementation notes

- **Read-only, anonymous, no CRUD.** `/speakers` has no button, modal, form,
  filter, toggle or grid action — the matrix above is exhaustive for the page's
  *actual* behaviour (load → render grid, plus the two terminal states: grid /
  empty). There is **no** error alert and **no** "load more" / pagination. Do not
  invent Add/Edit/Delete, search or permission scenarios the page does not have.
- **Anonymous by design.** `SimfPublicClient` carries no bearer token and
  `ListPublicSpeakersEndpoint` (`GET /api/v1/speakers`) is `AllowAnonymous()`.
  The "auth" scenario (E2E-WSPK-008) asserts the anonymous contract, not a
  `/login` / `/not-permitted` redirect (the Control-Panel pattern, absent here).
- **Lower-layer coverage:**
  - Component (bUnit, no browser): `tests/SIMF.Web.Tests/SpeakersPageTests.cs` pins
    the render branches — `Renders_speaker_cards_when_the_public_list_loads`,
    `Renders_the_empty_state_when_there_are_no_speakers`,
    `A_failed_speakers_envelope_degrades_to_the_empty_state`,
    `Uses_the_media_asset_photo_route_when_the_speaker_has_a_photo_asset`,
    `Omits_the_role_pill_and_location_when_the_speaker_has_neither`.
  - API integration: `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` covers the
    backing `GET /api/v1/speakers` (+ `{id}`): anonymous + succeeds, created active
    speaker appears in list + detail, ordered by display order, unknown id 404,
    deactivated speaker absent, detail returns active sessions, social URLs hidden
    unless consented. These prove the wire contract (anonymous, ordering,
    soft-delete drop-off, 404) at a lower layer; the E2E scenarios prove the
    *rendered* grid — card composition, photo source, the empty state, and the
    RTL / responsive presentation.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` under `tests/SIMF.E2E.Tests/` with a step-definition class.
  The steps are already runner-agnostic.

---

_Last reviewed:_ 2026-07-15 by Claude (Speakers page — `ln-` Bootstrap SSR, Figma 5840-26779).
