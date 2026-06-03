# Page 020 — Logic (ملف متحدث · تفاصيل المتحدث · Speaker profile)

Business rules behind the speaker profile. The profile read is anonymous and was
shipped with the Speakers branch (D-199); the meeting request is the new,
login-only D-269 addition. Verified against `PublicSpeakerDetail` and the new
`SpeakerMeetingRequest` entity.

## L-1 One anonymous call draws the whole profile
The screen renders from a **single** anonymous read:
`GET /app/speakers/{id}` → `PublicSpeakerDetail`:

| Field | Drives |
|-------|--------|
| `id` | identity (and the `{id}` for the meeting request) |
| `name`, `nameArabic` | the hero name (locale-picked) |
| `rank` | the hero rank line (e.g. `القبطان البحري`) |
| `countryId`, `countryNameEn`, `countryNameAr` | the speaker's country |
| `bio` / `bioArabic` | **tab 1** — `نبذة عنه` |
| `qualifications` / `qualificationsArabic` | **tab 2** — `المؤهلات العلمية` |
| `trainingExperience` / `trainingExperienceArabic` | **tab 3** — `الخبرات التدريبية` |
| `awards` / `awardsArabic` | **tab 4** — `الجوائز` |
| `allowsMeetingRequests` (bool) | **gates** the `طلب مقابلة` button (L-5) |
| `allowsDataSharing` (bool) | **gates** the social links (L-3) |
| `facebookUrl`, `linkedInUrl`, `xUrl` | the social links (only when `allowsDataSharing`) |
| `photoRelativePath` | the large avatar |
| `displayOrder` | ordering metadata (carried from the list) |
| `sessions[]` | the speaker's sessions list (L-4) |

A missing / soft-deleted speaker returns **404 `SPEAKER_NOT_FOUND`**.

## L-2 The four tabs map exactly to the four rich-text pairs
The four mockup profile tabs are a **fixed, one-to-one** mapping onto the four
rich-text pairs in `PublicSpeakerDetail` — the speaker's "CV":

| Tab (mockup) | English/Arabic field pair |
|---|---|
| `نبذة عنه` | `bio` / `bioArabic` |
| `المؤهلات العلمية` | `qualifications` / `qualificationsArabic` |
| `الخبرات التدريبية` | `trainingExperience` / `trainingExperienceArabic` |
| `الجوائز` | `awards` / `awardsArabic` |

Switching tabs is a **client-local** repaint of the bio card — **no** second
fetch (everything is already in the one `PublicSpeakerDetail`). The active locale
chooses the Arabic vs English member of the pair; if a pair is empty the tab
shows an empty/"no content" state rather than blank.

## L-3 Social links are gated on `allowsDataSharing`
The `facebookUrl` / `linkedInUrl` / `xUrl` values are **only meaningful when
`allowsDataSharing == true`**. The screen shows the social links **only** in that
case; when `allowsDataSharing` is false the social row is **hidden entirely**
(even if a URL value happens to be present, it is not surfaced). A missing
individual URL simply hides that one icon.

## L-4 The speaker's sessions list
`sessions[]` is a list of `PublicSpeakerSession`:

| Field | Drives |
|---|---|
| `id` | the session identity (tap-through target) |
| `code` | the session code |
| `title` / `titleArabic` | the session title (locale-picked) |
| `hallId` | the hall identity |
| `hallName` / `hallNameArabic` | the hall name (locale-picked) |
| `startUtc`, `endUtc` | the session time window (rendered in the user's locale/timezone) |

Each row taps through to that session's detail. An empty `sessions[]` renders a
"no sessions yet" state — the rest of the profile still shows.

## L-5 "Request a meeting" — the login-only D-269 write
The `طلب مقابلة` (request a meeting) button is shown **only when
`allowsMeetingRequests == true`** (L-1). Submitting is **login-only**:

1. **Gate** — the action is `RequireApprovedAccount`. A **guest / pending**
   account is **prompted to sign in**; only an **approved Visitor** can submit.
2. **Submit** — `POST /app/speakers/{speakerId}/meeting-requests` with body
   `SubmitSpeakerMeetingRequestRequest = { requesterName, subject }`. Server
   validates, in order:
   - the speaker **exists + is active** → else **404 `SPEAKER_NOT_FOUND`**,
   - the speaker **allows meeting requests** (`allowsMeetingRequests == true`)
     → else **409 `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED`**,
   - `requesterName` is **1..128** and `subject` is **1..1000** → else
     **400 `SPEAKER_MEETING_REQUEST_INVALID`**.
   The endpoint is **rate-limited** (SIMF-MOB-API-001).
3. **Result** — on success the request is created **`Pending`**; the response is
   `SpeakerMeetingRequestSubmitted = { id, speakerId, status (Pending),
   createdAt }`. The app shows a "request submitted — pending review"
   confirmation.
4. **Review** — the request is **not** auto-accepted. An **admin** lists, opens
   and responds to it in the Control Panel desk
   (`/admin/speaker-meeting-requests`, permission `SpeakerMeetingRequests.View`
   / `Manage`) — Accepted / Rejected with an optional note
   (Page_020_API E3). The requester's e-mail (PII) is exposed **only** on the
   admin detail, not back to the app.

This is a **new, dedicated** `SpeakerMeetingRequest` entity/table — **separate**
from the session-scoped `MeetingRequest` (the mockup screen 27 "Request
interview"). A speaker-profile meeting request is **not** tied to a session.

## L-6 Edge cases
- **Speaker soft-deleted / missing** → the profile read returns 404
  `SPEAKER_NOT_FOUND` → "speaker not found" state (so does a meeting-request
  POST against a missing/inactive speaker).
- **`allowsMeetingRequests == false`** → the `طلب مقابلة` button is **not
  shown**; a direct POST is refused with 409
  `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED` (defence in depth).
- **`allowsDataSharing == false`** → no social links shown.
- **Empty CV tab** → the tab renders an empty state, the other tabs are
  unaffected.
- **Empty `sessions[]`** → "no sessions yet" within the profile.
- **Guest taps `طلب مقابلة`** → sign-in prompt (401/403 if attempted without an
  approved token).
- **Invalid name/subject** → 400 `SPEAKER_MEETING_REQUEST_INVALID`; the form
  shows a field error.

## L-7 Localization
Arabic primary (RTL), English secondary. The hero (back chevron, rank, name),
the four tab labels, the bio card, the sessions list and the meeting form all
mirror RTL. Each Arabic/English field pair (name, the four CV pairs, session
title, hall name) is locale-picked. Session times render in the user's
locale/timezone. The social icons keep their brand glyphs; everything else uses
theme tokens.
