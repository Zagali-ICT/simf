# E2E test catalogue — `Organization Profile` (CP `/admin/organization-profile`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). D-495 — the CP page that edits
> the edition-generic forum config (name / title / slogan / bio, version + dates,
> event start/end, current year, status, location + GPS, contact, live-stream link,
> 7 social links, and the about-items + details lists). Backed by `GET`/`PUT
> /admin/organization-profile`; the app + website consume it via the public
> `GET /app/organization-profile` (anonymous, `Last-Modified`/304). The social +
> welcome values were migrated here from the old SiteSettings keys (one source of truth).

| | |
|--|--|
| **Page** | CP `/admin/organization-profile` (`OrganizationProfilePage.razor`) |
| **Route** | `/admin/organization-profile` (nav item `Module.OrganizationProfile`) |
| **APIs** | `GET /api/v1/admin/organization-profile` (prefill, `OrganizationProfile.View`); `PUT /api/v1/admin/organization-profile` — `AdminUpdateOrganizationProfileRequest` (full-document upsert, `OrganizationProfile.Manage`). Public read: `GET /api/v1/app/organization-profile` (anonymous, `Last-Modified`/`If-Modified-Since`→304). **Hero video (D-768):** `POST`/`DELETE /api/v1/admin/organization-profile/hero-video` (multipart `file`, `OrganizationProfile.Manage`); anonymous Range serve `GET /api/v1/app/organization/hero-video.mp4` (HTTP 206). |
| **Surface** | Control Panel (Blazor) — Administrator |
| **Auth setup** | A signed-in admin with `OrganizationProfile.Manage`. Use `Get-Totp` for 2FA — never a literal secret. |
| **Last reviewed** | 2026-06-24 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ORGP-001 | Page loads, prefilled with the current effective profile (seeded 2026 edition incl. about-items + details) | happy | P0 | authored ✓ (`OrganizationProfileTests` admin GET; `GET_public_is_anonymous_and_returns_the_seeded_edition`) |
| E2E-ORGP-002 | Edit scalars + status/year + a social URL + the live link + add/edit about-items & details → Save → success toast; values persist and `GET /app/organization-profile` returns them | happy | P0 | authored ✓ (`OrganizationProfileTests` PUT save+reflect) |
| E2E-ORGP-003 | Conditional GET — a second public read with `If-Modified-Since` = the prior `Last-Modified` returns `304 Not Modified` (no body); after an edit it returns `200` + the new body | caching | P1 | authored ✓ (`OrganizationProfileTests` 304) |
| E2E-ORGP-004 | Auth gate — a non-admin (or an admin without `OrganizationProfile.View`/`Manage`) is forbidden / the nav item is hidden | auth | P0 | authored ✓ (`OrganizationProfileTests` admin 403-gate + `CpNavigationPermissionTests`) |
| E2E-ORGP-005 | Validation — a non-http(s) URL (social / website / live link) → `400`; a name/title cleared → `400` | validation | P1 | authored ✓ (`OrganizationProfileTests` URL-reject) |
| E2E-ORGP-006 | Edition switch — change current year + status (Open→Archived) + the about/detail content → the app About screen + status badge re-skin to that edition from the cached profile | happy | P1 | spec |
| E2E-ORGP-007 | A save wire failure (5xx / network) → error toast, the form keeps the entered values | resilience | P1 | spec |
| E2E-ORGP-008 | RTL render (Arabic) — sections/labels/button mirror; URL + GPS fields stay LTR; the bilingual about-items/details edit cleanly | i18n | P1 | spec |
| E2E-ORGP-009 | Hero background video (D-756) — set the "Hero background video" field to a YouTube link → Save → success; `GET /app/organization-profile` returns `backgroundVideoUrl`; clearing it → the field returns null; a non-http(s) value → `400` | happy | P1 | authored ✓ (`OrganizationProfileTests` PUT save+reflect incl. `BackgroundVideoUrl`; URL-reject) |
| E2E-ORGP-010 | Bilingual detail value (D-762) — a detail row's `Value (AR)` round-trips; a blank `Value (AR)` surfaces as null so the app shows `Value (EN)`; the app About screen shows the Arabic value in Arabic and the English value in English | i18n | P1 | authored ✓ (`OrganizationProfileTests` PUT round-trips `ValueArabic` + null-fallback; app `about_screen_test` per-language render) |
| E2E-ORGP-011 | Repeating-list UX (D-762) — the about-items and details lists render as numbered cards; Up/Down reorder a row (persisted as `DisplayOrder`); Remove drops it; an empty list shows the placeholder | happy | P2 | spec |
| E2E-ORGP-012 | Hero video upload (D-768) — pick an mp4 → Upload → success toast; `backgroundVideoUrl` becomes the served `…/app/organization/hero-video.mp4` URL; the anonymous `GET …/hero-video.mp4` streams the bytes and honours a `Range` request (`206`); the app + website hero play it on Android | happy | P1 | authored ✓ (`OrganizationHeroVideoTests` upload→served-URL + anonymous stream + Range 206; app `hero_background_video_test` served-URL case) |
| E2E-ORGP-013 | Hero video remove (D-768) — "Remove uploaded video" clears `backgroundVideoUrl` and the stream `GET …/hero-video.mp4` then returns `404`; a separately-pasted external/YouTube link is left intact | happy | P1 | authored ✓ (`OrganizationHeroVideoTests` remove→404 + URL cleared) |
| E2E-ORGP-014 | Hero video validation + auth (D-768) — a non-video upload (e.g. `.html`) → `400 ORGANIZATION_PROFILE_INVALID`; a non-admin (no `OrganizationProfile.Manage`) upload → `403` | validation/auth | P1 | authored ✓ (`OrganizationHeroVideoTests` non-video 400 + non-admin 403) |
| E2E-ORGP-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-ORGP-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-ORGP-002 — Edit + save flows through to the public endpoint

```gherkin
Feature: CP Organization Profile
Scenario: An admin edits the forum config and the app reads it
  Given an admin with OrganizationProfile.Manage opens /admin/organization-profile
  And the form is prefilled from GET /admin/organization-profile (the seeded 2026 edition)
  When they set the current year to 2026 and status to "Open"
  And set the main live-stream link to "https://youtube.com/watch?v=simf2026"
  And set Facebook to "https://facebook.com/simf"
  And edit the "Vision" about-item text and add a "Year : 2026" detail
  And tap Save
  Then PUT /admin/organization-profile upserts the profile and shows the success toast
  And GET /api/v1/app/organization-profile returns the new values + about-items + details
  And the app About screen renders name → title → the about cards → the details list from it
```

### E2E-ORGP-003 — Conditional GET (Last-Modified / 304)

```gherkin
Scenario: The app revalidates its cached profile cheaply
  Given the app fetched GET /app/organization-profile and stored the Last-Modified value
  When it re-requests with If-Modified-Since set to that value
  Then the API returns 304 Not Modified with no body and the app keeps its cache
  When an admin then saves a change in the CP
  And the app re-requests with the stale If-Modified-Since
  Then the API returns 200 with the new profile and the app replaces its cache
```

### E2E-ORGP-004 — Auth gate

```gherkin
Scenario: Only admins with the OrganizationProfile permission reach the page
  Given a signed-in non-admin
  When they request /api/v1/admin/organization-profile
  Then the API returns 403 Forbidden
  And the CP nav hides the Organization Profile item for accounts without OrganizationProfile.View
```

### E2E-ORGP-005 — URL validation

```gherkin
Scenario: A non-http(s) URL is rejected
  Given an admin editing the profile
  When they set the live-stream link to "javascript:alert(1)" and tap Save
  Then PUT /admin/organization-profile returns 400 ORGANIZATION_PROFILE_INVALID
  And nothing is persisted
```

### E2E-ORGP-009 — Hero background video round-trips to both clients (D-756)

```gherkin
Scenario: An admin sets the hero background video and both clients read it
  Given an admin with OrganizationProfile.Manage opens /admin/organization-profile
  When they set "Hero background video (YouTube or MP4/HLS link)" to "https://youtu.be/rmW5sJTp-Zo" and tap Save
  Then PUT /admin/organization-profile returns 200 and a success toast shows
  And GET /app/organization-profile returns backgroundVideoUrl = "https://youtu.be/rmW5sJTp-Zo"
  And the app home hero and the website landing hero play that video muted + looping

Scenario: Clearing the field falls the hero back to the bundled media
  Given the hero background video is set
  When the admin clears the "Hero background video" field and taps Save
  Then GET /app/organization-profile returns backgroundVideoUrl = null
  And the app hero shows the banner image / discover photo and the website hero shows assets/hero-video.mp4

Scenario: A non-http(s) background video URL is rejected
  Given an admin editing the profile
  When they set the hero background video to "javascript:alert(1)" and tap Save
  Then PUT /admin/organization-profile returns 400 ORGANIZATION_PROFILE_INVALID
  And nothing is persisted
```

### E2E-ORGP-010 — Bilingual detail value round-trips + falls back (D-762)

```gherkin
Feature: CP Organization Profile — bilingual detail value
Scenario: An admin gives a detail row an Arabic value and the app shows it per language
  Given an admin with OrganizationProfile.Manage opens /admin/organization-profile
  When they add a detail "Organiser" / "الجهة المنظمة" with Value (EN) "Royal Saudi Naval Forces"
  And set its Value (AR) to "القوات البحرية الملكية السعودية"
  And add a detail "Year" / "السنة" with Value (EN) "2026" and a blank Value (AR)
  And tap Save
  Then PUT /admin/organization-profile persists both rows and shows the success toast
  And GET /app/organization-profile returns the Organiser row with valueArabic set
  And returns the Year row with valueArabic = null (blank clears to null)
  When an Arabic reader opens the app About screen
  Then the Organiser detail shows "القوات البحرية الملكية السعودية"
  And the Year detail falls back to "2026" (no Arabic value)
  When an English reader opens the app About screen
  Then the Organiser detail shows "Royal Saudi Naval Forces"
```

### E2E-ORGP-012/013/014 — Self-hosted hero background video (D-768)

```gherkin
Feature: CP Organization Profile — self-hosted hero background video
Scenario: An admin uploads a hero video and both clients play it
  Given an admin with OrganizationProfile.Manage opens /admin/organization-profile
  When they pick an .mp4 file in "Or upload a video file" and tap "Upload video"
  Then POST /admin/organization-profile/hero-video streams it to the store and shows the success toast
  And GET /app/organization-profile returns backgroundVideoUrl ending "/app/organization/hero-video.mp4"
  And an anonymous GET of that URL returns 200 with the video bytes and X-Content-Type-Options: nosniff
  And the same GET with "Range: bytes=0-3" returns 206 Partial Content
  And the app home hero (video_player) and the website landing <video> play it muted + looping on Android

Scenario: Removing the uploaded video reverts the hero and 404s the stream
  Given a hero video is uploaded
  When the admin taps "Remove uploaded video"
  Then DELETE /admin/organization-profile/hero-video clears backgroundVideoUrl and shows the toast
  And an anonymous GET of /app/organization/hero-video.mp4 returns 404
  And the app hero shows the banner image and the website hero shows the bundled asset

Scenario: A non-video upload or a non-admin caller is rejected
  Given an admin editing the profile
  When they upload a text/html file as the hero video
  Then POST /admin/organization-profile/hero-video returns 400 ORGANIZATION_PROFILE_INVALID
  Given a signed-in non-admin without OrganizationProfile.Manage
  When they POST to /admin/organization-profile/hero-video
  Then the API returns 403 Forbidden
```

---

_Last reviewed:_ `2026-07-25` by `SIMF Team` — D-768 (self-hosted hero background video upload);
D-762 (bilingual detail value + repeating-list UX); D-495 (new page).
