# Page 016 — Logic (الجلسات · Sessions)

Business rules behind the sessions list. Verified against the domain model + the
public programme reads (D-199), the D-252 enrichment, and the D-271 speaker
country+photo + screen rename.

## L-1 Fetch-once + cache; filter in the UI
The owner rule: **the API returns the full programme and the app caches it; the
calendar/filters run in the UI.**
- The app calls `GET /app/programme/sessions` **with no `day` filter** → the
  **whole active programme**, time-ordered. It caches the result.
- The **Upcoming / Forum pills**, the **day strip** and the **search** all filter
  the **cached list client-side** (Screen Guide: "Day selector / Search → filters
  the list inline"). No per-filter server round-trip.
- The server's optional `?day=yyyy-MM-dd` filter still exists (and is used by the
  Website / any thin client) but the **app does not need it** — it caches the
  whole programme and slices locally.

## L-2 What "full programme" means
`GET /app/programme/sessions` returns **every active session** (`Session.IsActive`),
regardless of broadcast `Status`. "Full programme" = all active sessions; the UI's
*Upcoming* pill is the client filtering on `StartUtc >= now`, not a server filter.
(A soft-deleted session never appears.)

## L-3 Per-item fields (the cached payload)
Each `PublicSessionListItem` carries — mapping to the owner's list:

| Owner field | Contract field(s) | Source |
|-------------|-------------------|--------|
| Date | `StartUtc`, `EndUtc` (UTC; app renders device-local) | `Session.StartUtc/EndUtc` |
| Code | `Code` | `Session.Code` |
| Title | `Title`, `TitleArabic` | `Session.Title/TitleArabic` |
| Body | `Description`, `DescriptionArabic` *(added D-252)* | `Session.Description/DescriptionArabic` |
| Hall | `HallId`, `HallName`, `HallNameArabic` | `Session.Hall` |
| **is-main-session / type** | `CategoryId`, `CategoryName`, `CategoryNameArabic` | `Session.Category` → `SessionCategory` (**see L-4**) |
| Speakers | `Speakers[]` (`PublicSessionSpeaker`: id, name AR/EN, title/rank, order, role, **country + photo** — L-6) *(added D-252; country+photo D-271)* | `Session.Speakers` → `Speaker` |
| (extra) | `PrimaryTheme*`, `Status` | theme chip + lifecycle badge |

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
category fields are null and the app shows no type chip.

## L-5 Ordering + active marker
The list is ordered by `StartUtc` then `Title`. The UI marks the
currently-running / next session (brass background) — a **client** decision from
the cached times + the device clock; the API does not flag "active".

## L-6 Speakers (incl. country flag + photo — D-271)
The list speaker cards mirror the detail exactly: only **active** speakers, ordered
by `DisplayOrder` (0 = primary), each with name (AR/EN), rank (`Title`), order and
role (`Speaker`/`Host` — D-225, so the mockup's "المضيف / host" marker renders).

Each `PublicSessionSpeaker` now **also carries (append-only, D-219 / D-271)**:

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
  on **both** the list (this page) and the detail (Page_017), from the one cached
  payload — covered by
  `ProgrammeSessionsTests.Session_speaker_carries_country_flag_and_photo`.

## L-7 Edge cases
- Empty programme → empty list; the UI shows a "no sessions" placeholder.
- A session with no speakers → `Speakers` is an empty array (never null on the wire).
- A speaker with no country → `CountryId` null → no flag (the name carries the
  context); a speaker with no photo → `PhotoRelativePath` null → placeholder avatar.
- A session with no category → type chip hidden.
- Body may be null (optional `Description`) → the row shows title + time only.

## L-8 Localization
Arabic primary (RTL), English secondary; bilingual data is paired
(`Title`/`TitleArabic`, `HallName`/`HallNameArabic`, `CategoryName`/`CategoryNameArabic`,
speaker `Name`/`NameArabic`, country `CountryNameEn`/`CountryNameAr`). Times are UTC
on the wire, rendered in the device tz.

## L-9 Screen rename — الأجندة → الجلسات (D-271)
The screen identity is renamed from **الأجندة · Agenda** to **الجلسات · Sessions**:
- **Title** AR `الجلسات` · EN `Sessions`.
- **Bottom-nav label** → `الجلسات` (was `الأجندة`).
- **Filter pills** → `الجلسات القادمة` (Upcoming) / `جلسات الفعالية` (Forum / full),
  replacing `أجندة قادمة` / `أجندة الفعالية`. Their **behaviour is unchanged** — they
  still filter the cached list client-side (L-1).
- The **API is unchanged**: the read stays `GET /app/programme/sessions` — the
  rename is **UI-only**, no contract change.
- The Flutter **route + nav constant** (`RouteNames.agenda` / `/agenda`) rename is a
  **coordinated follow-up** (a later pass), so existing deep links keep working
  until that pass lands.
