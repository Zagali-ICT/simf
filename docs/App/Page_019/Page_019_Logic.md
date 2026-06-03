# Page 019 — Logic (المتحدثون · Speakers list)

Business rules behind the speakers list. Verified against the public speakers read
(D-199). **No new backend behaviour** — this page reads an endpoint that already
exists; it is the list half of the Speakers list (19) → Speaker profile (20) pair.

## L-1 One call draws the whole list
The screen renders from a **single, anonymous** read:
`GET /app/speakers` → `PublicSpeakers`:

| Field | Drives |
|-------|--------|
| `items[]` (`PublicSpeakerSummary`) | one **`sp-card`** per entry |

Each `PublicSpeakerSummary` is:

| Field | Drives |
|-------|--------|
| `id` | the tap-through target (→ profile 20) |
| `name` / `nameArabic` | the card **name** (Arabic primary, English secondary) |
| `rank` | the card **rank line** (e.g. `القبطان البحري`) |
| `countryId`, `countryNameEn`, `countryNameAr` | the speaker's country (available for an optional country label) |
| `photoRelativePath` | the card **avatar** image (placeholder `⚓` / `★` when empty) |
| `displayOrder` | the **list order** (L-2) |

This is a **summary** projection — it deliberately omits the bio / CV / social
URLs / sessions; those load on the **profile** (20) via `GET /app/speakers/{id}`.

## L-2 Ordering = `displayOrder`, then name
The list is returned **ordered by `displayOrder` ascending, then by name** — an
admin-controlled order with a stable name tie-break. The app renders the `items`
**in the order received**; it does **not** re-sort client-side.

## L-3 Tap-through = the speaker's `id` → profile (20)
Tapping a card / its `المزيد` link navigates to **Speaker profile (20,
[Page_020](../Page_020/README.md))** for `summary.id`
(`RouteNames.speakerProfile` → `/speakers/:speakerId`). The profile then does its
own `GET /app/speakers/{id}` read. The list passes only the **id**; it does not
hand the profile any cached body.

## L-4 Anonymous — no auth, no permission
The read is `AllowAnonymous` (D-199): no token, no permission, no account state is
required. A guest sees the same list as an approved visitor. Nothing on **this**
page is login-gated — the login-only **meeting request** (D-269) is a separate
action on the **profile** (20).

## L-5 Edge cases
- **No speakers** → `items` is empty → the screen shows an **empty state** ("no
  speakers yet"), not an error.
- **Speaker with no photo** → `photoRelativePath` empty → the card shows the
  **placeholder** avatar (`⚓` / `★`).
- **A soft-deleted / inactive speaker** → never appears in `items` (the read
  returns active speakers only); tapping a stale id on the profile (20) yields a
  `404 SPEAKER_NOT_FOUND` there, handled on that page.
- **Offline / network error** → the standard list error/retry state (no cached
  guarantee for an anonymous first load).

## L-6 Localization
Arabic primary (RTL), English secondary. The card name uses `nameArabic` in
Arabic and `name` in English; the **rank line** (`rank`) and any country label
render per the active locale. The list mirrors RTL; the avatar leads on the
trailing (right) side per the mockup `sp-card` layout.
