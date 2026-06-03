# Page 016 — Logic (الأجندة · Agenda)

Business rules behind the agenda. Verified against the domain model + the public
programme reads (D-199) and the D-252 enrichment.

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
| Speakers | `Speakers[]` (`PublicSessionSpeaker`: id, name AR/EN, title/rank, order, role) *(added D-252)* | `Session.Speakers` → `Speaker` |
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

## L-6 Speakers
The list speaker cards mirror the detail exactly: only **active** speakers, ordered
by `DisplayOrder` (0 = primary), each with name (AR/EN), rank (`Title`), order and
role (`Speaker`/`Host` — D-225, so the mockup's "المضيف / host" marker renders).

## L-7 Edge cases
- Empty programme → empty list; the UI shows a "no sessions" placeholder.
- A session with no speakers → `Speakers` is an empty array (never null on the wire).
- A session with no category → type chip hidden.
- Body may be null (optional `Description`) → the row shows title + time only.

## L-8 Localization
Arabic primary (RTL), English secondary; bilingual data is paired
(`Title`/`TitleArabic`, `HallName`/`HallNameArabic`, `CategoryName`/`CategoryNameArabic`,
speaker `Name`/`NameArabic`). Times are UTC on the wire, rendered in the device tz.
