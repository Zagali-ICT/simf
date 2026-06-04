# E2E test catalogue — My profile (`/account/profile`)

| | |
|--|--|
| **Page** | [`web/account-profile.md`](../../pages/web/account-profile.md) |
| **Route** | `/account/profile` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | An **Approved Visitor** account signed in via `/login` (+ OTP if 2FA is on). The page is `[Authorize]`-gated only — it is NOT behind a `PermissionCatalog` permission. Admin TOTP via `Get-Totp` is only needed for the audit-trail assertions (the CP `/admin/visitors` review surface). |
| **Last reviewed** | 2026-06-02 |

> **Surface note.** This is a Website (Blazor SSR + interactive server island)
> self-service page, not a Control Panel admin page. It is reachable by any
> signed-in account whose `account_state` is **Approved** (or `EmailVerified`
> before first save). `OnParametersSetAsync` redirects a `PendingApproval`
> user to `/account/pending` and a `Rejected` user to `/account/rejected`.
> Backend routes are all same-origin BFF proxies under `/account/api/...`
> that forward to the SIMF API (`http://localhost:5175`) carrying the cookie's
> access token — the browser never sees the token (D-037 pattern).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WPR-001 | Golden path — load, fill identity + nationality + Saudi ID + mobile + interests, Save → success alert + round-trip | happy | P0 | _to author_ |
| E2E-WPR-002 | Approved account renders the QR card (SVG + QR id) | happy | P1 | _to author_ |
| E2E-WPR-003 | Pending account is redirected away to `/account/pending` (no QR) | auth | P0 | _to author_ |
| E2E-WPR-004 | Auth gate — anonymous visitor → login redirect | auth | P0 | _to author_ |
| E2E-WPR-005 | Non-Saudi branch — Iqama/Passport fields swap in when "Saudi national? = No" | happy | P1 | _to author_ |
| E2E-WPR-006 | Upload ID image (PNG ≤ 5 MB) → "ID image saved" + HasIdImage flips | happy | P1 | _to author_ |
| E2E-WPR-007 | Empty interests lookup → info alert, picker hidden | happy | P1 | _to author_ |
| E2E-WPR-008 | Notifications link routes to `/account/notifications` (D-132 orphan fix) | nav | P1 | _to author_ |
| E2E-WPR-009 | Sign out clears the session and bounces to the login surface | nav | P1 | _to author_ |
| E2E-WPR-010 | Validation — under-18 DOB + 0 interests → bilingual server error, no QR change | error | P1 | _to author_ |
| E2E-WPR-011 | Validation — bad Saudi national ID (fails Luhn) → bilingual server error | error | P1 | _to author_ |
| E2E-WPR-012 | Unknown nationality code → 400 `PROFILE_NATIONALITY_UNKNOWN` bilingual error | error | P2 | _to author_ |
| E2E-WPR-013 | ID image too large (> 5 MB) → 400 `VISITOR_ID_IMAGE_TOO_LARGE` bilingual error | error | P1 | _to author_ |
| E2E-WPR-014 | ID image wrong MIME / magic-byte mismatch → 400 `VISITOR_ID_IMAGE_MIME_UNSUPPORTED` | error | P2 | _to author_ |
| E2E-WPR-015 | Server 500 on `/account/api/user-profile` load → bilingual load-failed alert | resilience | P2 | _to author_ |
| E2E-WPR-016 | RTL / Arabic render — page mirrors, labels + alerts in Arabic | i18n | P1 | _to author_ |

## Scenarios

### E2E-WPR-001 — Golden path

```gherkin
Feature: Visitor self-service profile round-trip
  As an Approved visitor
  I want to complete my SIMF profile and save it
  So that I get my event QR badge and admins can review me

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And an Approved visitor has signed in via /login (and OTP if 2FA is on)
  And they have navigated to /account/profile
  And the page issues GET /account/api/user-profile,
      GET /account/api/user-profile/countries and GET /account/api/interests in parallel
  And all three return HTTP 200 with Success=true

Scenario: Fill the profile form and save
  Given the profile form has rendered with the empty/loaded model
  When the visitor fills "Name in Arabic" = "عبدالله القحطاني"
  And fills "Name in English" = "Abdullah Al-Qahtani"
  And fills "Job title (optional)" = "Director of Operations"
  And picks "Nationality" = "Saudi Arabia" (code "SA")
  And sets "Date of birth" = "1990-04-15"
  And fills "Place of birth" = "Riyadh"
  And leaves "Are you a Saudi national?" = "Yes"
  And fills "Saudi national ID" = "1098765432"   # 10 digits, prefix 1, Luhn-valid
  And fills "Saudi mobile" = "+966555123456"
  And ticks 3 interests from "Your interests"
  And clicks "Save my profile"
  Then the BFF forwards POST /account/api/user-profile and the API returns HTTP 200 Success=true
  And a green SimfAlert reads "Your profile has been saved." / "تم حفظ ملفك الشخصي."
  And the returned UserProfileResponse echoes EnglishName="Abdullah Al-Qahtani", IsSaudi=true,
      NationalId="1098765432", NationalityCode="SA" and the 3 InterestIds
  And on reload GET /account/api/user-profile returns the same values (persisted round-trip)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-account-profile-golden-before.png`
- Screenshot after: `docs/screenshots/web-account-profile-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/user-profile`, `/account/api/user-profile/countries`
  and `/account/api/interests` call returns 200
- Audit row: audit entry with `Event = 'UserProfile.Saved'`, the actor's user id,
  and `Detail` containing `created` (first save) or `updated`, plus `profileTypeId=...`
- Note: a first-ever save while `account_state = EmailVerified` also flips the
  account to `PendingApproval`, revokes refresh tokens, and dispatches the
  `AccountProfileSubmitted` + `AdminPendingVisitor` notifications — the visitor
  is then bounced on next navigation by the state guard (covered by E2E-WPR-003).

### E2E-WPR-002 — Approved account renders QR card

```gherkin
Scenario: An Approved account shows the event QR
  Given the signed-in account is in state Approved with a minted QrId
  When the visitor opens /account/profile
  Then a card titled "Your event QR" / "رمز QR الخاص بك للملتقى" renders
  And it shows the supporting line "Show this code at event entry. RSNF staff scan it to check you in."
  And an inline SVG QR (rendered server-side via QRCoder) is visible
  And the 12-character Crockford QR id text appears below the SVG
```

### E2E-WPR-003 — Pending account redirected (no QR)

```gherkin
Scenario: A PendingApproval account is sent to the pending banner page
  Given the signed-in account has account_state = "PendingApproval"
  When they navigate to /account/profile
  Then OnParametersSetAsync redirects them to /account/pending
  And the profile form never renders
  And no QR card is shown

Scenario: A Rejected account is sent to the rejected banner page
  Given the signed-in account has account_state = "Rejected"
  When they navigate to /account/profile
  Then they are redirected to /account/rejected
```

### E2E-WPR-004 — Auth gate (anonymous)

```gherkin
Scenario: An unauthenticated visitor cannot reach the profile
  Given no SIMF auth cookie is present
  When the browser requests /account/profile
  Then the [Authorize] gate redirects to the Website login surface
  And no /account/api/user-profile request fires
```

### E2E-WPR-005 — Non-Saudi branch swaps the ID fields

```gherkin
Scenario: Selecting "No" reveals Iqama + Passport instead of National ID
  Given the profile form has rendered with "Are you a Saudi national?" = "Yes"
  And the "Saudi national ID" field is visible
  When the visitor selects "No"
  Then the "Saudi national ID" field disappears
  And two fields appear: "Iqama number" and "Passport number"
  And the Iqama hint reads "10 digits starting with 2 — required if you are resident in KSA."
  When the visitor fills "Passport number" = "AB1234567" and leaves Iqama blank
  And picks a non-Saudi nationality (e.g. "Egypt" / code "EG")
  And completes the rest of the required fields
  And clicks "Save my profile"
  Then the API accepts the save (passport satisfies the "Iqama OR Passport" rule)
  And the green "Your profile has been saved." alert appears
```

### E2E-WPR-006 — Upload ID image

```gherkin
Scenario: Upload a valid PNG ID image under 5 MB
  Given the profile is loaded and the "ID image" card is visible
  When the visitor chooses a 1.2 MB PNG file in "Choose a file"
  And clicks "Upload"
  Then the BFF forwards POST /account/api/user-profile/id-image (multipart, field "file")
  And the API verifies content-type + magic bytes and returns HTTP 200 Success=true
  And a green alert reads "Your ID image has been saved." / "تم حفظ صورة الهوية."
  And on the next load the card shows "An ID image is already on file. Upload again to replace it."
```

**Evidence captured:**
- Screenshot after upload: `docs/screenshots/web-account-profile-idimage-after.png`
- Network: `POST /account/api/user-profile/id-image` returns 200
- Audit row: `UserProfile.IdImageUploaded` with the actor id and `Detail` =
  byte count + content type

### E2E-WPR-007 — Empty interests lookup

```gherkin
Scenario: Interests lookup published empty renders the info alert
  Given GET /account/api/interests returns an empty Interests array
  When the visitor opens /account/profile
  Then the SimfCheckList interests picker does NOT render
  And an info SimfAlert reads
      "The interests list has not been published yet. Please check back soon."
      / "قائمة الاهتمامات لم تُنشر بعد. يُرجى المراجعة لاحقًا."
  And the rest of the form still renders normally
```

### E2E-WPR-008 — Notifications link (D-132 orphan fix)

```gherkin
Scenario: The header Notifications link reaches the inbox
  Given the visitor is on /account/profile
  When they click the "Notifications" / "الإشعارات" link in the header actions
  Then the browser navigates to /account/notifications
  And the notifications page loads
```

### E2E-WPR-009 — Sign out

```gherkin
Scenario: Sign out ends the session
  Given the visitor is on /account/profile
  When they click "Sign out" / "تسجيل الخروج"
  Then simfAccount.signOut runs (the auth cookie is cleared)
  And the visitor lands on the public login surface
  And a follow-up request to /account/profile redirects to login (no longer authenticated)
```

### E2E-WPR-010 — Validation: under-18 DOB + no interests

```gherkin
Scenario: Server rejects an under-age, no-interest save
  Given the profile form has rendered
  When the visitor sets "Date of birth" to a date less than 18 years ago (e.g. today − 5 years)
  And selects zero interests
  And fills the remaining required fields validly
  And clicks "Save my profile"
  Then the API returns a 400 validation failure (FluentValidation)
  And the red SimfAlert surfaces the bilingual message, e.g.
      "You must be at least 18 years old to register."
      / "يجب أن يكون عمرك 18 عامًا على الأقل للتسجيل."
      and/or "Pick between 1 and 10 interests." / "اختر ما بين 1 و 10 اهتمامات."
  And no green "saved" alert appears
  And the QR card (if present) is unchanged
```

### E2E-WPR-011 — Validation: bad Saudi national ID

```gherkin
Scenario: A Saudi national ID that fails the Luhn check is rejected
  Given "Are you a Saudi national?" = "Yes"
  When the visitor fills "Saudi national ID" = "1111111111"  # prefix 1, 10 digits, but NOT Luhn-valid
  And completes the other required fields
  And clicks "Save my profile"
  Then the API returns a 400 validation failure
  And the red SimfAlert reads
      "The Saudi national id is not a valid number." / "رقم الهوية الوطنية غير صحيح."
  And the form stays on the page with no green alert
```

### E2E-WPR-012 — Unknown nationality code

```gherkin
Scenario: A nationality code that does not resolve to a Country row
  Given the request body carries NationalityCode = "ZZ" (two chars, passes shape, no Country row)
  When the visitor saves the profile
  Then the API returns HTTP 400 with ApiResult.Error.Code = "PROFILE_NATIONALITY_UNKNOWN"
  And the red SimfAlert surfaces the bilingual MessageForCurrentCulture(), e.g.
      "Nationality code 'ZZ' is not supported." / "الجنسية 'ZZ' غير مدعومة."
```

### E2E-WPR-013 — ID image too large

```gherkin
Scenario: An ID image larger than 5 MB is rejected
  Given the "ID image" card is visible
  When the visitor chooses a 6 MB JPEG file
  And clicks "Upload"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "VISITOR_ID_IMAGE_TOO_LARGE"
  And the red SimfAlert reads
      "The ID image must be 5 MB or less." / "يجب ألا يتجاوز حجم صورة الهوية 5 ميغابايت."
  And an audit row UserProfile.IdImageRejected is written with the error code
```

### E2E-WPR-014 — ID image wrong MIME / magic bytes

```gherkin
Scenario: A non-image (or mismatched magic-byte) file is rejected
  Given the "ID image" card is visible
  When the visitor chooses a .pdf renamed to .png (or a real PDF) and clicks "Upload"
  Then the API rejects it with ApiResult.Error.Code = "VISITOR_ID_IMAGE_MIME_UNSUPPORTED"
  And the red SimfAlert reads
      "The ID image must be PNG, JPEG or WebP." / "يجب أن تكون صورة الهوية بصيغة PNG أو JPEG أو WebP."
  And an audit row UserProfile.IdImageRejected is written
```

### E2E-WPR-015 — Server 500 on load

```gherkin
Scenario: A 500 from the profile load surfaces the load-failed alert
  Given GET /account/api/user-profile (or /countries or /interests) returns a failure / 500 (e.g. DB down)
  When the visitor opens /account/profile
  Then the page does not render the form
  And a red SimfAlert reads "The profile could not be loaded. Please try again."
      / "تعذّر تحميل الملف الشخصي. حاول مرة أخرى."
  And no green "saved" alert appears
```

### E2E-WPR-016 — RTL / Arabic render

```gherkin
Scenario: Arabic culture mirrors the page and localises every label
  Given the visitor is on /account/profile in English
  When they switch the UI culture to Arabic (ar)
  Then the page reloads with <html dir="rtl" lang="ar">
  And the title reads "ملفي الشخصي"
  And the Save button reads "حفظ ملفي الشخصي"
  And the QR card title reads "رمز QR الخاص بك للملتقى"
  And the nationality + interest labels render their Arabic values
  And the form fields and header actions mirror to the right
```

---

## Implementation notes

- **API integration tests cover the same surface at a lower layer.**
  - `tests/SIMF.Api.Tests/UserProfileTests.cs` — upsert round-trip, ID-image
    round-trip + magic-byte gate, get-empty-when-not-saved-yet, and the
    nationality-unknown path.
  - `tests/SIMF.Api.Tests/UserProfileRollbackTests.cs` — H16 transaction
    rollback (an Identity-side failure drops the App-side profile changes).
  These exercise the backend without a browser; the E2E scenarios above add
  the BFF proxy + Blazor interactive island + bilingual UI surface on top.
- **Exact backend routes (BFF → API).** The page calls same-origin BFF proxies
  (`src/Website/SIMF.Web/Endpoints/AccountEndpoints.cs`) that forward to the API:
  - `GET  /account/api/user-profile`            → `GET  /api/v1/account/user-profile`
  - `POST /account/api/user-profile`            → `POST /api/v1/account/user-profile`
  - `GET  /account/api/user-profile/countries`  → countries lookup
  - `GET  /account/api/interests`               → active interests lookup
  - `POST /account/api/user-profile/id-image`   → `POST /api/v1/account/user-profile/id-image` (multipart, field `file`, `DisableAntiforgery`)
  - `GET  /account/api/user-profile/id-image`   → streams the decrypted image same-origin
- **No CP permission gate.** Unlike admin pages, this Website page is gated only
  by `[Authorize]` + the in-page `account_state` guard. There is no
  `PermissionCatalog` entry and no `/not-permitted` redirect — the auth-gate
  scenario (E2E-WPR-004) is an anonymous-user login redirect, and E2E-WPR-003
  covers the non-Approved-state redirect.
- **Validation lives server-side.** The form posts the whole model and lets the
  API's `UpsertUserProfileRequestValidator` (FluentValidation) + the service-layer
  existence checks decide; the page surfaces `Error.MessageForCurrentCulture()`
  in a red `SimfAlert`. Key error codes: `PROFILE_NATIONALITY_UNKNOWN`,
  `ADMIN_PROFILE_TYPE_INVALID`, `ORGANISATION_INVALID`, `INTEREST_INVALID`,
  `VISITOR_ID_IMAGE_TOO_LARGE`, `VISITOR_ID_IMAGE_MIME_UNSUPPORTED`,
  `VISITOR_ID_IMAGE_MISSING`.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) + a
  step-definition class. The Gherkin shape is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
