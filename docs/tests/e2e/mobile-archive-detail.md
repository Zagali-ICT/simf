# E2E test catalogue — `Past-edition detail` (`archive-detail`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> per-edition public detail read added in **D-273** (`GET /app/archive/{id}` +
> the `Location*` / `DateLabel*` columns). API implementation lives in
> `tests/SIMF.Api.Tests/ArchiveTests.cs` (public detail) and
> `tests/SIMF.Api.Tests/AdminArchiveTests.cs` (admin authoring round-trip).
>
> **Figma parity (D-432, frame `925:3079` / detail node `24-01`):** the screen
> now matches its Figma frame — the notice banner, "اختار ملتقى" edition pills,
> bulleted gold title, summary, المكان / الزمن row, and the three stat tiles,
> plus the three **new** rich lists that render only when the lazily-loaded
> detail (`GET /app/archive/{id}`) carries them: the **الصور والفيديو** gallery
> strip (image vs. video tile with a play glyph), the **عناوين الجلسات**
> session-title bullets, and the **المتحدثون السابقون** past-speaker avatars
> with a `+N آخرون` overflow chip. Each section is omitted (not faked) when its
> list is empty.
>
> **P6 (D-440):** the gallery + past-speaker images are now **real**. An image
> gallery item whose `url` is an absolute http(s) link renders the real photo
> (`Image.network`, cover-filled); a **video** item or a **legacy relative path**
> keeps the glyph placeholder. A past-speaker whose `photoRelativePath` is an
> absolute URL renders the real avatar, else the initials. The owner chose
> **URL-per-row** (admin pastes a reachable image URL — the CP authoring is
> replace-all, which would orphan per-row uploaded assets); the `_isHttpUrl` guard
> ensures only absolute URLs are loaded, so a relative path never errors.

| | |
|--|--|
| **Page** | [`Page_024-01`](../../App/Page_024-01/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/archive/{id}` (anonymous) · app screen #24-01 `/archive/:editionId` *(planned — not yet in `route_names.dart`)* (public) |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **None for the read** — the detail is anonymous/public. An **Admin** token (admin TOTP via the `Get-Totp` helper, **no literal secrets**) only to seed an edition and to flip the archive-visibility toggle. |
| **Last reviewed** | 2026-06-16 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB024D-001 | Visible archive → `GET /app/archive/{id}` returns the edition (title, summary, counters) | happy | P0 | authored ✓ (`Public_detail_returns_edition_with_location_and_date_when_visible`) |
| E2E-MOB024D-002 | Place + date label round-trip in the public payload (`location*` / `dateLabel*`) | happy | P0 | authored ✓ (`Public_detail_returns_edition_with_location_and_date_when_visible`) |
| E2E-MOB024D-003 | Anonymous caller (no token) can read the detail (public) | auth | P0 | authored ✓ (`Public_detail_returns_edition_with_location_and_date_when_visible`) |
| E2E-MOB024D-004 | Unknown edition id → 404 `archive_edition_not_found` | edge | P0 | authored ✓ (`Public_detail_returns_404_for_an_unknown_id`) |
| E2E-MOB024D-005 | Archive visibility OFF → detail 404 (single surface, no leak) | edge | P0 | authored ✓ (`Public_detail_is_404_when_archive_visibility_is_off`) |
| E2E-MOB024D-006 | Soft-deleted (`IsActive == false`) edition → 404 | edge | P1 | authored ✓ (`Public_detail_is_404_for_a_soft_deleted_edition`) |
| E2E-MOB024D-007 | Null optional scalars (summary / location / date / cover) → boxes hidden, gradient fallback | edge | P1 | authored (screen) |
| E2E-MOB024D-008 | Absent rich lists (gallery / session titles / past speakers empty) → sections omitted, not faked | edge | P2 | authored (screen) |
| E2E-MOB024D-009 | RTL render; year + counter numbers LTR | i18n | P1 | authored (screen) |
| E2E-MOB024D-010 | الصور والفيديو gallery strip — image tile vs. video tile (play glyph) + caption | happy | P1 | authored (screen) |
| E2E-MOB024D-011 | عناوين الجلسات — session titles render as beige bullets | happy | P1 | authored (screen) |
| E2E-MOB024D-012 | المتحدثون السابقون — first 4 avatars + "+N آخرون" overflow chip | happy | P1 | authored (screen) |
| E2E-MOB024D-013 | P6 — gallery image with an absolute url renders the real photo; a video / relative path do not (D-440) | display | P1 | authored ✓ (screen `P6 — gallery image + past-speaker photo render real URLs`) |
| E2E-MOB024D-014 | P6 — past-speaker with an absolute photo url renders the real avatar; blank/relative → initials (D-440) | display | P1 | authored ✓ (screen `P6 — gallery image + past-speaker photo render real URLs`) |

## Scenarios

### E2E-MOB024D-001 — Detail returned for a visible edition

```gherkin
Feature: Past-edition detail
  As any app user (no login)
  I want to open one past forum edition
  So that I can read its title, summary, place, date and counters

Scenario: A visible archive returns the edition detail
  Given the archive-visibility toggle is on
  And an admin has created the 2024 edition "الملتقى البحري السعودي الدولي 2024"
  When an anonymous client calls GET /api/v1/app/archive/{id}
  Then the response is 200
  And data.year is 2024
  And data.titleEn is "SIMF 2024" (titleAr in Arabic)
  And data.attendees, data.sessions and data.speakers are the seeded counters
```

**Evidence:** `ArchiveTests.Public_detail_returns_edition_with_location_and_date_when_visible` (green).

### E2E-MOB024D-002 — Place + date label round-trip

```gherkin
Scenario: The new place and date-label scalars are returned
  Given the 2024 edition has locationEn "Riyadh · Riyadh Front" and dateLabelEn "November 2024 · 3 days"
  When the detail is fetched
  Then data.locationEn is "Riyadh · Riyadh Front" and data.locationAr is "الرياض · واجهة الرياض"
  And data.dateLabelEn is "November 2024 · 3 days" and data.dateLabelAr is "نوفمبر 2024 · 3 أيام"
  And the screen renders the المكان and الزمن boxes
```

**Evidence:** `ArchiveTests.Public_detail_returns_edition_with_location_and_date_when_visible` +
`AdminArchiveTests.Admin_create_roundtrips_location_and_date_label` (green).

### E2E-MOB024D-003 — Anonymous read

```gherkin
Scenario: The detail is public — no token required
  Given a visible 2024 edition
  When a client with no Authorization header calls GET /api/v1/app/archive/{id}
  Then the response is 200 (not 401)
```

**Evidence:** `ArchiveTests.Public_detail_returns_edition_with_location_and_date_when_visible`
(the detail call uses the no-token client) (green).

### E2E-MOB024D-004 — Unknown id

```gherkin
Scenario: An unknown edition id is not found
  Given the archive-visibility toggle is on
  When a client calls GET /api/v1/app/archive/{a random guid}
  Then the response is 404
  And the error code is "archive_edition_not_found"
```

**Evidence:** `ArchiveTests.Public_detail_returns_404_for_an_unknown_id` (green).

### E2E-MOB024D-005 — Visibility gate

```gherkin
Scenario: A hidden archive does not leak an edition by id
  Given a 2014 edition exists and is active
  And the archive-visibility toggle is turned off
  When a client calls GET /api/v1/app/archive/{the 2014 edition id}
  Then the response is 404 (the same surface as an unknown id)
```

**Evidence:** `ArchiveTests.Public_detail_is_404_when_archive_visibility_is_off` (green).

### E2E-MOB024D-006 — Soft-deleted edition

```gherkin
Scenario: A deactivated edition is not found
  Given an edition has been soft-deleted (IsActive == false)
  When its detail is fetched while the archive is visible
  Then the response is 404 archive_edition_not_found
```

**Evidence:** `ArchiveTests.Public_detail_is_404_for_a_soft_deleted_edition` (create → DELETE → GET → 404).

### E2E-MOB024D-007 — Null optionals

```gherkin
Scenario: Missing optional fields hide their boxes
  Given an edition with no summary, no location, no date label and no cover image
  When the detail renders
  Then the نبذة paragraph, the المكان box and the الزمن box are hidden
  And the cover uses the gradient fallback
  And the three counters still render
```

### E2E-MOB024D-008 — Absent rich lists are omitted

```gherkin
Scenario: When the edition carries no rich lists the sections are not shown
  Given a visible edition whose gallery, sessionTitles and pastSpeakers arrays are all empty
  When the detail renders
  Then the الصور والفيديو, عناوين الجلسات and المتحدثون السابقون sections
       are absent entirely (omitted, not faked, not a broken/empty row)
  And the title, summary, المكان / الزمن row and the three stat tiles still render
```

### E2E-MOB024D-009 — RTL render

```gherkin
Scenario: The detail renders right-to-left in Arabic
  Given the device locale is Arabic
  When the detail renders
  Then the layout and back chevron are right-to-left
  And the year overlay and the counter numbers render left-to-right
```

### E2E-MOB024D-010 — الصور والفيديو gallery strip

```gherkin
Feature: Past-edition rich lists (Figma 24-01)
  As any app user (no login)
  I want to see the selected edition's media, sessions and speakers
  So that the past edition feels rich, not just counters

Scenario: A gallery of one image + one video renders the right tiles
  Given the 2024 edition detail returns gallery:
    | kind | captionAr            | captionEn            |
    | 0    | حفل الافتتاح         | Opening ceremony     |
    | 1    | الكلمة الرئيسية      | Keynote highlights   |
  When the detail renders for the selected edition
  Then a section labelled "الصور والفيديو" ("Photos & videos") appears above the bullets
  And it is a horizontal strip of navy tiles with the beige hairline border
  And the kind=0 tile shows the image glyph (image_outlined) in gold
  And the kind=1 tile shows the play glyph (play_circle_outline) in gold
  And each tile shows its single-line caption ("حفل الافتتاح" / "الكلمة الرئيسية")
  And switching to English shows the English captions ("Opening ceremony" / "Keynote highlights")
```

### E2E-MOB024D-011 — عناوين الجلسات session-title bullets

```gherkin
Scenario: Session titles render as a list of beige bullets
  Given the 2024 edition detail returns sessionTitles:
    | titleAr                              | titleEn                          |
    | مستقبل الملاحة البحرية               | The future of maritime shipping  |
    | الأمن البحري في البحر الأحمر         | Red Sea maritime security        |
  When the detail renders
  Then a section labelled "عناوين الجلسات" ("Session titles") appears
  And each title renders as a beige disc-bulleted line in order
  And "مستقبل الملاحة البحرية" is the first bullet and "الأمن البحري في البحر الأحمر" the second
  And switching to English shows "The future of maritime shipping" then "Red Sea maritime security"
```

### E2E-MOB024D-012 — المتحدثون السابقون avatars + overflow

```gherkin
Scenario: Six past speakers show four avatars and a "+2 آخرون" overflow chip
  Given the 2024 edition detail returns pastSpeakers with six names:
    | nameAr            | nameEn            |
    | أحمد الزهراني     | Ahmed Alzahrani   |
    | سارة القحطاني     | Sara Alqahtani    |
    | محمد العتيبي      | Mohammed Alotaibi |
    | نورة الدوسري      | Noura Aldosari    |
    | خالد الشهري       | Khalid Alshehri   |
    | ريم الغامدي       | Reem Alghamdi     |
  When the detail renders
  Then a section labelled "المتحدثون السابقون" ("Past speakers") appears
  And exactly the first four speakers render as a name + initials avatar chip
  And a gold-bordered overflow chip reads "+2 آخرون" ("+2 more")
  And each avatar shows the speaker's two-letter initials in gold on a navy circle
```

### E2E-MOB024D-013 / 014 — Real gallery + past-speaker photos (D-440)

```gherkin
Feature: Past-edition detail — real images (P6)

Scenario: An image gallery item with an absolute url renders the real photo
  Given a visible edition whose gallery has
    | kind  | url                                      |
    | image | https://cdn.example.sa/2024/opening.jpg  |
    | video | https://youtu.be/abc123                  |
    | image | archive/2024/legacy.jpg                  |
  When the archive screen renders the gallery strip
  Then the first item loads Image.network(https://cdn.example.sa/2024/opening.jpg)
  And the video item shows the play glyph (no network image)
  And the legacy relative path shows the image glyph (no network image)

Scenario: A past speaker with an absolute photo url renders the real avatar
  Given a past speaker whose photoRelativePath is https://cdn.example.sa/2024/s1.jpg
  Then the avatar loads that image; a speaker with a blank/relative photo shows initials
```

**Evidence:** `_GalleryTile` / `_SpeakerChip` render `Image.network` only when
`_isHttpUrl(url)` (absolute http/https), else the glyph / initials fallback.
Screen test `P6 — gallery image + past-speaker photo render real URLs; a video /
relative path do not` asserts the NetworkImage URLs present + the video/relative
absent.

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
