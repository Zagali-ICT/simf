# E2E test catalogue — `Contact us` (`contact-us`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> the form posts to the new **public** `POST /app/contact-inquiry` (anonymous,
> rate-limited); the info panel + social links read the shared
> `GET /app/organization-profile` (D-495). Built to KSA Figma frame
> **`1388:7711`**. Tested in
> `src/Mobile/simf_app/test/features/contact_us/contact_us_screen_test.dart`;
> backend in `tests/SIMF.Api.Tests/ContactInquiryTests.cs` *(pending — see
> note)*. Previously a ComingSoon placeholder (D-464).

| | |
|--|--|
| **Page** | app screen #203 `contactUs` |
| **Route** | `/contact-us` (`POST /app/contact-inquiry` + `GET /app/organization-profile`) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1388:7711` |
| **Auth setup** | **None** — submit is `AllowAnonymous`; a signed-in caller's `sub` is captured server-side. |
| **Last reviewed** | 2026-06-26 |

> **Build notes (pending an API-free window):** the EF migration for the new
> `ContactInquiries` table, the backend `ContactInquiryTests`, and the Control
> Panel inbox page (`/admin/contact-inquiries`, perms `ContactInquiries.View` /
> `.Manage`) are authored in code/endpoints but their generation/run is deferred
> until the dev API is stopped. **Brand-accurate social glyphs** are pending a
> Figma asset export (Material approximations used until then).

## Layout

- **Header**: back chevron + centred title **تواصل معنا**.
- **أرسل رسالة** card: الاسم / البريد الإلكتروني / الرسالة fields + a gold
  **إرسال** button (validates: name + message required, email well-formed).
- **معلومات التواصل** card: phone (الخط الساخن), email, location rows, each with
  a gold icon — populated from the org profile (only set fields show).
- **وسائل التواصل الاجتماعي** row: a bordered tap box per set social link.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB203-001 | Form + info panel render; info hydrated from the org profile | happy | P0 | authored ✓ (screen `renders the form + contact-info panel from the profile`) |
| E2E-MOB203-002 | Empty submit → validation; no API call | validation | P0 | authored ✓ (screen `empty submit shows validation and does not call the API`) |
| E2E-MOB203-003 | Valid submit → POST + success toast + form cleared | happy | P0 | authored ✓ (screen `valid submit posts the inquiry and shows the toast`) |
| E2E-MOB203-004 | Submit failure → error toast | error | P1 | covered (`ApiFailure` path → `contactSendFailed`) |
| E2E-MOB203-005 | `POST /app/contact-inquiry` persists anonymously; bad input → 400 | api | P0 | pending (`ContactInquiryTests` — deferred until API freed) |
| E2E-MOB203-006 | CP inbox lists inquiries; mark-handled toggles + gates on perms | cp | P1 | pending (CP inbox page — deferred until API/CP build freed) |
| E2E-MOB203-007 | RTL — Arabic labels + LTR phone/email values | rtl | P2 | covered (`textDirection.ltr` on phone/email) |

## Scenarios

```gherkin
Feature: Contact us (Figma 1388:7711, POST /app/contact-inquiry)

Scenario: The form and contact info render
  Given the org profile has a phone, email and location set
  When the user opens /contact-us
  Then the "أرسل رسالة" form shows name, email and message fields with a Send button
  And the "معلومات التواصل" panel shows the phone, email and location
  And a tap box renders for each set social link

Scenario: Submitting an empty form is blocked
  When the user taps Send with empty fields
  Then "Name is required", "A valid email is required" and "Message is required" are shown
  And no request is sent

Scenario: Submitting a valid message
  Given the user entered a name, a valid email and a message
  When the user taps Send
  Then POST /app/contact-inquiry is called with those values
  And a success toast "Your message has been sent…" is shown
  And the form is cleared

Scenario: The submit endpoint is anonymous and validated
  When an anonymous client POSTs /api/v1/app/contact-inquiry with a name, valid email and message
  Then it returns 200 and the inquiry is stored
  And a blank name or malformed email returns 400
```

**Evidence:** screen tests (3 — render + validation + submit); backend
`ContactInquiryTests` + the CP inbox E2E are **pending** the API-free window
(tracked in the wave checkpoint).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team`.
