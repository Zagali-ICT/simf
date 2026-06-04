# Page 020 — Function (ملف متحدث · تفاصيل المتحدث · Speaker profile)

What the user does on this screen. Grounded in `Mockup.html` screen 20 (line
~1355) and the Screen Guide SCREEN20 ("the speaker's CV — rank, name, photo and
the four tabs نبذة عنه / المؤهلات العلمية / الخبرات التدريبية / الجوائز").

## Privilege / auth gate
**Reads: Guest+ (anonymous).** The profile read (`GET /app/speakers/{id}`) is
`AllowAnonymous` (D-199) — a guest can open and read the whole CV and the
speaker's sessions without signing in. **One** action is login-only: the
**`طلب مقابلة` (request a meeting)** affordance is gated by
`RequireApprovedAccount` (D-269) and is shown **only when the speaker allows
meeting requests**. A guest who taps it is prompted to sign in.

## Elements (top → bottom, from the mockup)
1. **Hero** — a **back chevron**, the speaker's **rank** line (e.g. `القبطان
   البحري`) and the **name** as an `h3`.
2. **Large avatar** — the speaker photo (`photoRelativePath`), or the mockup
   placeholder glyph (⚓ / ★) when there is none.
3. **Four tabs** — `نبذة عنه` · `المؤهلات العلمية` · `الخبرات التدريبية` ·
   `الجوائز`. These map **exactly** to the four rich-text pairs
   bio / qualifications / trainingExperience / awards (+ Arabic) — the "CV".
4. **Bio card** — shows the **active tab's** rich-text content (Arabic primary).
5. **Social links** (conditional) — Facebook / LinkedIn / X — shown **only when
   `allowsDataSharing == true`** (the URLs are otherwise not meaningful).
6. **Sessions list** — the speaker's sessions (`sessions[]`): each a session
   title + hall + time, tapping through to the session detail.
7. **`طلب مقابلة` (request a meeting)** (conditional) — shown **only when
   `allowsMeetingRequests == true`** (the D-269 owner addition).
8. **Bottom nav** — the five-slot bar.

## What the user does
1. **Read the CV** — switch between the four tabs (`نبذة عنه`, `المؤهلات
   العلمية`, `الخبرات التدريبية`, `الجوائز`); each tab repaints the bio card
   with that text. All four come from the **one** `PublicSpeakerDetail`
   (Page_020_Logic L-1/L-2) — switching tabs is **client-local**, no re-fetch.
2. **Follow the speaker** (optional) — when `allowsDataSharing` is true, tap a
   social link (Facebook / LinkedIn / X) to open it; when it is false, no social
   links are shown (Page_020_Logic L-3).
3. **Browse the speaker's sessions** — read the `sessions[]` list (title, hall,
   start/end) and tap a session to open its detail (Page_020_Logic L-4).
4. **Request a meeting** (Visitor, login-only) — when `allowsMeetingRequests`
   is true the `طلب مقابلة` button is shown. Tapping it:
   - if the user is a **guest / pending** → prompts sign-in (the action is
     `RequireApprovedAccount`),
   - if the user is an **approved Visitor** → opens a short form
     (`requesterName`, `subject`) and submits
     `POST /app/speakers/{id}/meeting-requests` → the request is created
     **Pending** and an **admin reviews it** in the CP desk
     (Page_020_Logic L-5, Page_020_API E2).

## Acceptance criteria
- A **guest** can open the screen and read the whole profile — the four CV tabs,
  the avatar, the sessions list — with **no** sign-in (the read is anonymous).
- The four tabs render bio / qualifications / trainingExperience / awards in the
  active locale (Arabic primary), switching **without** a second network call.
- Social links appear **only** when `allowsDataSharing == true`; otherwise they
  are hidden.
- The `طلب مقابلة` button appears **only** when `allowsMeetingRequests == true`.
- A **guest** who taps `طلب مقابلة` is prompted to sign in; an **approved
  Visitor** can submit and gets a **Pending** request back.
- Submitting against a speaker that does **not** allow meeting requests is
  refused (409 `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED`); an invalid name/subject
  is refused (400 `SPEAKER_MEETING_REQUEST_INVALID`); a missing/soft-deleted
  speaker is 404 (`SPEAKER_NOT_FOUND`).
- A **soft-deleted / missing** speaker shows a "speaker not found" state.

## Where it fits in the journey
**Speakers branch:** Home (13) → **Speakers list (19)** → **Speaker profile
(20)**. Reached from a speaker card's `المزيد` / *More* link on screen 19. The
meeting request is the only write on the branch and is the only login-gated
action.
