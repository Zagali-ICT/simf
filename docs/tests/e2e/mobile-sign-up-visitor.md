# E2E test catalogue — `Sign up — visitor` (`signUpVisitor`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #7 — visitor
> profile completion. Spec: [`Page_007`](../../App/Page_007/README.md). Runner-agnostic
> Gherkin. The screen glue is widget-tested in
> `src/Mobile/simf_app/test/features/profile/sign_up_visitor_screen_test.dart`; the
> profile model + `isComplete` in `…/profile/profile_models_test.dart`; the upload
> MIME derivation in `…/profile/profile_repository_mime_test.dart`. The backend
> contract is covered by `tests/SIMF.Api.Tests/UserProfileTests.cs`.

| | |
|--|--|
| **Page** | [`Page_007`](../../App/Page_007/README.md) (App page docs) |
| **Route** | app screen #7 `signUpVisitor` → `/sign-up/visitor` (**auth-gated** — Page_007 L-1) |
| **APIs** | `GET/POST /api/v1/app/account/user-profile` (`UpsertUserProfileRequest` → `UserProfileResponse`); lookups `GET …/user-profile/countries`, `…/profile-types`, `…/interests`, `GET /app/organisations?search=&top=`; `POST …/user-profile/id-image` (multipart). All signed-in, no role/permission (D7). |
| **Surface** | Mobile (Flutter) — Visitor (signed-in, profile-incomplete) |
| **Auth setup** | A signed-in Visitor token (own `sub`). No admin role. Obtain via the standard app sign-in; never a literal secret. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB007-001 | Pre-filled valid profile → Save → `POST user-profile` → wait-for-approval (registration-status) | happy | P0 | authored ✓ (widget test) |
| E2E-MOB007-002 | First-time empty profile → required errors (Arabic/English name, nationality, DOB, ≥1 interest); no request | error | P0 | authored ✓ (widget test) |
| E2E-MOB007-003 | Saudi national ID — `^1\d{9}$` + Luhn enforced client-side, re-checked server-side | validation | P1 | authored ✓ (Luhn unit + server) |
| E2E-MOB007-004 | Non-Saudi → Iqama/Passport segmented picker; Iqama `^2\d{9}$`+Luhn / passport 6–9; one required | validation | P1 | authored ✓ (widget test) |
| E2E-MOB007-005 | Date of birth required + registrant ≥ 18 (picker caps at today−18, D-197) | validation | P0 | authored ✓ (widget test + server) |
| E2E-MOB007-006 | Interests sub-step — ≥1 required, cap at 10 (toast), `n/10` counter | validation | P0 | authored ✓ (widget test) |
| E2E-MOB007-007 | Organisation typeahead — debounced search, select sets id, Clear unlinks; empty state | happy | P1 | authored ✓ (widget test) |
| E2E-MOB007-008 | ID image — pick → attached → multipart upload after save; failure is non-blocking | happy | P1 | authored ✓ (MIME unit + screen) |
| E2E-MOB007-009 | Auth gate — anonymous open redirects to sign-in | auth | P0 | authored ✓ (router-gate test) |
| E2E-MOB007-010 | Empty lookup → picker empty state, never a blocking error | edge | P1 | authored (screen) |
| E2E-MOB007-011 | Server validation / 500 → message shown, form state preserved | resilience | P1 | authored ✓ (load-error widget test) |
| E2E-MOB007-012 | RTL render (Arabic) mirrors; lookup labels switch with locale | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB007-001 — Golden path: complete profile → wait-for-approval

```gherkin
Feature: Visitor profile completion
Scenario: A signed-in visitor completes the profile and is sent to wait for approval
  Given a signed-in, profile-incomplete visitor opens /sign-up/visitor
  And the four lookups (countries, profile-types, interests, organisations) load
  When they enter "راكان السالم" (Arabic) and "Rakan Alsalem" (English)
  And they pick nationality "Saudi Arabia"
  And the "Saudi national" toggle is on and they enter national ID "1000000008"
  And they pick a date of birth of "2000-01-31"
  And they pick at least one interest
  And they tap "Save"
  Then the app POSTs UpsertUserProfileRequest (with InterestIds, no user id) to /app/account/user-profile
  And on ApiResult.Ok it shows "Profile saved"
  And it navigates to the registration-status (wait-for-approval) screen
```

**Evidence:** `sign_up_visitor_screen_test` — "a pre-filled valid profile upserts and routes to registration-status" (asserts the request carries `interestIds`, `nationalId`, `isSaudi`).

### E2E-MOB007-002 — Required fields block save

```gherkin
Scenario: An empty first-time profile cannot be saved
  Given a first-time (empty) profile is open
  When the visitor taps "Save" without filling anything
  Then the Arabic and English name fields show "This field is required"
  And the nationality field shows "Nationality is required"
  And the date-of-birth field shows "Date of birth is required"
  And the interests section shows "Pick at least one interest"
  And no request is sent to /app/account/user-profile
```

**Evidence:** `sign_up_visitor_screen_test` — "a first-time empty profile blocks save with required errors".

### E2E-MOB007-003 — Saudi national ID shape + Luhn

```gherkin
Scenario: A malformed Saudi national ID is rejected
  Given the "Saudi national" toggle is on
  When the visitor enters "1234567890" (fails Luhn) as the national ID
  Then the field shows "Invalid national ID (10 digits starting with 1)"
  And a valid id like "1000000008" passes the client check
  And the server re-validates the prefix + Luhn (UpsertUserProfileRequestValidator)
```

> The client mirrors `^1\d{9}$` + Luhn for instant feedback; the server is the
> authority (D-197).

**Evidence:** Luhn mirror in `SignUpVisitorScreen._isValidLuhn`; server in
`UpsertUserProfileRequestValidator` (covered by `UserProfileTests`).

### E2E-MOB007-004 — Non-Saudi Iqama / Passport branch

```gherkin
Scenario: A non-Saudi registrant supplies an Iqama or a passport
  Given the "Saudi national" toggle is off
  Then a segmented control offers "Iqama" and "Passport" and the National ID field is hidden
  When "Iqama" is selected and "2000000004" (fails Luhn) is entered
  Then the field shows "Invalid Iqama number (10 digits starting with 2)"
  When "Passport" is selected and "AB12" (too short) is entered
  Then the field shows "Invalid passport number (6–9 letters or digits)"
  And leaving the document number empty shows "An Iqama or passport number is required"
```

**Evidence:** `sign_up_visitor_screen_test` — "a non-Saudi profile shows the Iqama / Passport document picker" + "toggling Saudi national switches to the National ID field"; server `When(!IsSaudi)` rules.

### E2E-MOB007-005 — Date of birth required, age ≥ 18

```gherkin
Scenario: The registrant must be at least 18
  Given the date-of-birth picker is opened
  Then the latest selectable date is today minus 18 years (under-18 is unreachable)
  And saving without a date of birth shows "Date of birth is required"
  And the server re-validates the 18+ rule (D-197, leap-safe)
```

**Evidence:** `_pickDateOfBirth` sets `lastDate = today − 18y`; `sign_up_visitor_screen_test`
asserts the DOB-required error; server `BeAtLeastEighteen`.

### E2E-MOB007-006 — Interests sub-step (1–10)

```gherkin
Scenario: The interests picker enforces 1..10
  Given the interests cards are shown with a "0 / 10 selected" counter
  When the visitor selects one interest
  Then the counter reads "1 / 10 selected"
  And attempting to select an 11th shows "You can pick at most 10 interests" and is ignored
  And saving with zero interests shows "Pick at least one interest"
```

**Evidence:** `sign_up_visitor_screen_test` — "selecting an interest updates the counter";
the cap is enforced in `_toggleInterest`; server `InterestIds` 1–10 rule (D-050 / D12).

### E2E-MOB007-007 — Organisation typeahead

```gherkin
Scenario: The organisation field searches and links by id
  Given the organisation search field is shown (الجهة / Organisation)
  When the visitor types part of an organisation name
  Then after a short debounce the app calls GET /app/organisations?search=&top=20
  And matching rows appear; selecting one shows it as the chosen organisation with a "Clear"
  And "Clear" unlinks it and restores the search field
  And a search with no matches shows "No organisations found" (never a blocking error)
```

**Evidence:** `_onOrganisationSearchChanged` (350 ms debounce) → `searchOrganisations`;
`_selectOrganisation` / `_clearOrganisation`; the field is optional (D6 / D-220 / D-221).

### E2E-MOB007-008 — ID image upload (multipart, after save)

```gherkin
Scenario: The optional ID image is attached and uploaded after the profile saves
  Given the visitor taps "Attach ID image" and picks a JPEG/PNG/WebP
  Then a thumbnail + "Image attached" + "Remove" is shown
  When they Save
  Then the profile is upserted first, then the image is POSTed multipart to
       /app/account/user-profile/id-image with the correct Content-Type
  And the server enforces 5 MB + content-type + magic-byte (PNG/JPEG/WebP)
  And if only the image upload fails, "Profile saved, but the image upload failed." is shown
       (the profile save still succeeded — non-blocking)
```

> The Content-Type is derived from the filename and sent on the file part — without
> it dio would send `application/octet-stream` and the server would 400 every upload.

**Evidence:** `profile_repository_mime_test` (jpg/jpeg/png/webp → MIME; unknown → null);
`SimfApiClient.upload` sets `DioMediaType`; server `UserIdDocumentUploadEndpoint`
(magic-byte gate, covered by `UserProfileTests`).

### E2E-MOB007-009 — Auth gate

```gherkin
Scenario: An anonymous open is impossible
  Given no session
  When /sign-up/visitor is requested
  Then the router redirects to /sign-in (route 7 is in the auth gate, Page_007 L-1)
```

**Evidence:** `router_gate_test` — "the explicitly gated screens require auth" asserts
`routePathRequiresAuth('/sign-up/visitor')` is true.

### E2E-MOB007-010 — Empty lookup is not an error

```gherkin
Scenario: A lookup that returns no rows shows an empty state
  Given the interests lookup returns []
  Then the interests section shows "No interests available" (not a blocking error)
  And the same applies to the organisation typeahead empty result
```

**Evidence:** `_buildInterestsField` empty branch; `_buildOrganisationField` empty branch.

### E2E-MOB007-011 — Load / server failure preserves state

```gherkin
Scenario: The initial load fails, then recovers on retry
  Given GET /app/account/user-profile fails on open
  Then the screen shows "Could not load the form." and a "Retry" button
  When the visitor taps "Retry" and the call succeeds
  Then the form renders with the lookups
Scenario: A save fails
  When the upsert returns a validation error / 500
  Then the message is shown and the form keeps its values (no navigation)
```

**Evidence:** `sign_up_visitor_screen_test` — "a load failure shows the retry, which
reloads the form"; the save catch sets `_submitError` and stays on the screen.

### E2E-MOB007-012 — RTL render (Arabic)

```gherkin
Scenario: The form mirrors under Arabic
  Given the app language is Arabic
  Then the sections, fields, toggles, and the interests grid mirror right-to-left
  And each lookup row's Arabic label (nameArabic / nameAr) is shown
  And switching to English flips the labels without a re-fetch
```

> By construction: the screen uses localized `AppL10n` strings + Material RTL; each
> lookup row carries its own AR/EN label.

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
