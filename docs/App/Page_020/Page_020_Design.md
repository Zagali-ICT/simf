# Page 020 — Design (ملف متحدث · تفاصيل المتحدث · Speaker profile)

Flutter screen design. Grounded in `Mockup.html` screen 20 (line ~1355). RTL,
Arabic-primary.

## Layout (top → bottom, from the mockup)
1. **Hero** —
   - a **back chevron** (RTL-aware),
   - the speaker's **rank** line (e.g. `القبطان البحري`, from `rank`),
   - the **name** as an `h3` (locale-picked `name` / `nameArabic`).
2. **Large avatar** — the speaker photo (`photoRelativePath`); the mockup
   placeholder glyph (⚓ / ★) when there is no photo.
3. **Four tabs** — a tab strip:
   - `نبذة عنه` · `المؤهلات العلمية` · `الخبرات التدريبية` · `الجوائز`.
   - The active tab is highlighted (brass accent).
4. **Bio card** — a card showing the **active tab's** rich-text content
   (`bio` / `qualifications` / `trainingExperience` / `awards`, +Arabic).
5. **Social links** (conditional) — a row of Facebook / LinkedIn / X icons —
   rendered **only when `allowsDataSharing == true`**; a missing single URL hides
   that one icon.
6. **Sessions list** — the speaker's sessions (`sessions[]`): each row is the
   session title + hall + time, tappable to the session detail.
7. **`طلب مقابلة` (request a meeting)** (conditional) — a filled primary action,
   rendered **only when `allowsMeetingRequests == true`**.
8. **Bottom nav** — the five-slot bar.

## Data binding
- **Hero / avatar** bind to `rank`, the locale-picked `name`/`nameArabic`, and
  `photoRelativePath` from `PublicSpeakerDetail` (Page_020_API E1).
- **Tabs / bio card** bind to the four field pairs by the fixed mapping
  (Page_020_Logic L-2); switching tabs is **client-local** (no re-fetch) — pick
  the AR/EN member by the active locale; show an empty state for an empty pair.
- **Social links** are shown only when `allowsDataSharing == true`, binding to
  `facebookUrl` / `linkedInUrl` / `xUrl` (Page_020_Logic L-3).
- **Sessions** bind to `sessions[]` (`PublicSpeakerSession`): locale-picked
  `title`/`hallName`, the `start`/`end` window rendered in the user's
  locale/timezone; tap → session detail (Page_020_Logic L-4).
- **`طلب مقابلة`** is shown only when `allowsMeetingRequests == true`. Tapping it:
  - **guest / pending** → sign-in prompt (the action is
    `RequireApprovedAccount`),
  - **approved Visitor** → a short form (`requesterName`, `subject`) that submits
    `POST /app/speakers/{id}/meeting-requests` (Page_020_API E2); on success a
    "submitted — pending review" confirmation; the admin reviews it in the CP
    desk.

## States
- **Loading** — skeleton hero + tabs + bio card while `GET /app/speakers/{id}`
  runs.
- **Loaded (guest)** — the full CV, sessions and (if `allowsDataSharing`) social
  links render with **no** sign-in; the `طلب مقابلة` button shows only when the
  speaker allows it.
- **Tab switch** — instant repaint of the bio card (no spinner — data already in
  the one payload).
- **Empty tab** — the active tab shows an "no content" state; siblings
  unaffected.
- **No sessions** — the sessions block shows "no sessions yet".
- **Meeting not allowed** — `allowsMeetingRequests == false` → the `طلب مقابلة`
  button is **absent** (and a direct POST would 409
  `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED`).
- **Meeting form / submit** — guest is prompted to sign in; an approved Visitor
  sees field validation (`requesterName` 1..128, `subject` 1..1000 → 400
  `SPEAKER_MEETING_REQUEST_INVALID`), then a "pending review" confirmation on
  success.
- **Not found** — speaker soft-deleted / missing → 404 `SPEAKER_NOT_FOUND` →
  "speaker not found" state.

## RTL / localization
- Whole screen mirrored RTL; the hero back chevron follows RTL.
- The four tab labels, the bio card, the sessions rows and the meeting form are
  Arabic-primary, English secondary — each AR/EN field pair locale-picked.
- Session times render in the user's locale/timezone.
- The active tab and the `طلب مقابلة` button use the **brass** accent; the social
  icons keep their brand glyphs; everything else uses theme tokens (no raw
  colours).
