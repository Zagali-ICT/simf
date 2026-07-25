# Page 016 — Logic (الأجندة · Sessions agenda)

Business rules behind the sessions list. Verified against the domain model + the
public programme reads (D-199), the D-252 enrichment, the D-271 speaker
country+photo, and the as-built KSA screen (`sessions_screen.dart` +
`session_models.dart`, D-378).

Last updated: 2026-06-13 — KSA Wave-2 redesign (D-378).

## L-1 Fetch-once + cache; filter in the UI
The owner rule: **the API returns the full programme and the app caches it; the
calendar/filters run in the UI.**
- The app calls `GET /app/programme/sessions` **with no `day` filter** (once,
  in `initState`) → the **whole active programme**, time-ordered. It caches
  the result in screen state.
- The **search**, the **Upcoming / Event-agenda pills** and the **day strip**
  all filter the **cached list client-side** (`filterSessions` in
  `session_models.dart`; Screen Guide: "Day selector / Search → filters the
  list inline"). No per-filter server round-trip.
- The server's optional `?day=yyyy-MM-dd` filter still exists (and is usable
  by any thin client) but the **app does not use it** — it caches the whole
  programme and slices locally.

### L-1a The day strip is data-driven (computed once per load)
The strip's days are the **distinct device-local calendar days** present in
the cached sessions (`sessionDays`: keyed on local year-month-day, ascending,
each entry a midnight-local `DateTime`). They are derived **once per
load/retry**, not per rebuild. No days (empty programme) → the strip is
hidden.

## L-2 What "full programme" means; the two views
`GET /app/programme/sessions` returns **every active session** (`Session.IsActive`),
regardless of broadcast `Status`. "Full programme" = all active sessions; a
soft-deleted session never appears. The two pills are pure client filters:
- **الأجندة القادمة / Upcoming** (the **default** view) keeps sessions with
  `start >= now` (UTC compare — the code drops `start.isBefore(nowUtc)`).
- **أجندة الفعالية / Event agenda** ("forum") shows the whole cached
  programme, past sessions included.

## L-2a Day + search filters (exact semantics)
- **Day filter:** selecting a strip day keeps sessions whose **device-local
  start day** equals it; **re-tapping the selected day clears the filter**
  (selection becomes null — there is no "all days" pill in the frame).
- **Search:** the query is trimmed + lowercased and matched as a substring
  over **`title`, `titleArabic`, `description`, `descriptionArabic` and
  `code`** (joined haystack — both languages always searched). Empty query =
  no filtering.
- The three filters AND together; the input (server time-)order is preserved.

## L-3 Per-item fields (the cached payload)
Each `PublicSessionListItem` carries — mapping to the owner's list:

| Owner field | Contract field(s) | Source |
|-------------|-------------------|--------|
| Date | `Start`, `End` (UTC; app renders device-local) | `Session.Start/End` |
| Code | `Code` | `Session.Code` |
| Title | `Title`, `TitleArabic` | `Session.Title/TitleArabic` |
| Body | `Description`, `DescriptionArabic` *(added D-252)* | `Session.Description/DescriptionArabic` |
| Hall | `HallId`, `HallName`, `HallNameArabic` | `Session.Hall` |
| **is-main-session / type** | `CategoryId`, `CategoryName`, `CategoryNameArabic` | `Session.Category` → `SessionCategory` (**see L-4**) |
| Speakers | `Speakers[]` (`PublicSessionSpeaker`: id, name AR/EN, title/rank, order, role, **country + photo** — L-6) *(added D-252; country+photo D-271)* | `Session.Speakers` → `Speaker` |
| (extra) | `PrimaryTheme*`, `Status` | theme chip + lifecycle status |

The Flutter model (`SessionListItem`) decodes all of the above except the
theme **names** — of the `PrimaryTheme*` trio it decodes only
`primaryThemeColor` — and the KSA list row renders **none** of category /
theme / status / speakers (they ride the cache for the Page_017 preview).

## L-4 "is main session or not / type" = SessionCategory (D-226)
The owner's "is main session or not / type" is the **session category**, confirmed
by both controlled sources:
- The **mockup** screen 17 shows a tag literally reading **`جلسة رئيسية`
  ("Main session")** next to the hall tag.
- The **Screen Guide** SCREEN17 lists *"Tags: hall location + **session
  category**"*.

`SessionCategory` (D-226) is a **dynamic lookup** whose own code comment says
*"a dynamic Category, for example a main session"*. So **"Main Session" is one
seeded category value**, not a separate boolean — there is **no `IsMain` field**.
The category ships empty (the team seeds the value list, OI-2); until seeded, the
category fields are null. The category renders on the **detail** (Page_017) —
the KSA list row carries no type chip.

## L-5 Ordering + row numbering
The list is ordered by `Start` then `Title` (server-side); the client
filters preserve that order. Each rendered row is numbered with a
**zero-padded 1-based index over the filtered list** (`01`, `02`, … — a pure
client sequence, not `Code`). **There is no active/next-session marker** —
the KSA frame has none, and the old mockup's brass highlight was dropped with
the D-378 rebuild; the API does not flag "active" either.

## L-6 Speakers (incl. country flag + photo — D-271)
The list speaker cards mirror the detail exactly: only **active** speakers, ordered
by `DisplayOrder` (0 = primary), each with name (AR/EN), rank (`Title`), order and
role (`Speaker`/`Host` — D-225). The **list row renders no speakers** — the cards
ride the cache so the session detail (Page_017) previews without a second fetch.

Each `PublicSessionSpeaker` also carries (append-only, D-219 / D-271):

| Field | Type | Drives |
|-------|------|--------|
| `CountryId` | `int?` | the speaker's **country flag** — the client renders the flag **from `CountryId`** |
| `CountryNameEn` / `CountryNameAr` | `string?` | the country **label / fallback** (tooltip + a no-flag text fallback) |
| `PhotoRelativePath` | `string?` | the speaker **avatar** image (the card's round photo) |

Rules:
- The **flag is rendered from `CountryId`** (the client maps the id → a flag
  asset); `CountryNameEn` / `CountryNameAr` are the **label/fallback** only.
- `PhotoRelativePath` is the **avatar** source; when null the card shows the
  initials / placeholder avatar.
- All four are **nullable** — a speaker with no country shows no flag (name
  fallback only), and a speaker with no photo shows the placeholder. They surface
  on the wire of **both** the list (this page) and the detail (Page_017), from
  the one cached payload — covered by
  `ProgrammeSessionsTests.Session_speaker_carries_country_flag_and_photo`.

## L-7 Edge cases
- Empty programme → empty list + hidden day strip; the UI shows the
  **لا توجد جلسات / No sessions** placeholder.
- A pill/day/search combination with no matches → the **same** empty
  placeholder (the cache is intact; clearing the filters restores the list).
- A session with no speakers → `Speakers` is an empty array (never null on the wire).
- A speaker with no country → `CountryId` null → no flag (the name carries the
  context); a speaker with no photo → `PhotoRelativePath` null → placeholder avatar.
- Body (`Description`) may be null → the row shows the time chip + numbered
  title only (the description line is omitted).
- The fetch failed (`ApiFailure`) → error state; **Retry re-runs the fetch**
  (and re-derives the day strip).

## L-8 Localization
Arabic primary (RTL), English secondary; bilingual data is paired
(`Title`/`TitleArabic`, `HallName`/`HallNameArabic`, `CategoryName`/`CategoryNameArabic`,
speaker `Name`/`NameArabic`, country `CountryNameEn`/`CountryNameAr`), with a
cross-language fallback when one side is blank. Times are UTC on the wire,
rendered in the device tz; the row's **time chip** is forced LTR (`hh:mm` over
`AM`/`PM`) and the day strip's weekday labels are 3-letter English in both
locales (as the KSA frame draws them).

## L-9 Screen naming — الأجندة → الجلسات (D-271) → الأجندة (D-378)
- **D-271/D-276** renamed the screen **الأجندة · Agenda → الجلسات · Sessions**
  and the route `agenda`/`/agenda` → `sessions`/`/sessions` (with the
  `/sessions/:sessionId[/my-seat]` sub-routes). The route + constants keep
  those names.
- **D-378** (the KSA rebuild) re-titles the **visible header and bottom-nav
  label** to **الأجندة / Agenda** (`l10n.navAgenda` — one string drives both)
  and sets the pill copy to the frame's
  **أجندة الفعالية / Event agenda** + **الأجندة القادمة / Upcoming agenda**
  (`sessionsViewForum` / `sessionsViewUpcoming`). Pill behaviour is unchanged —
  they still filter the cached list client-side (L-1/L-2).
- The **API is unchanged** throughout: the read stays
  `GET /app/programme/sessions` — every rename was **UI-only**, no contract
  change.
