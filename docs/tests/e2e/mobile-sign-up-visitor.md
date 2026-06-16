# E2E test catalogue — `Sign up — profile data` (`signUpVisitor`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #7 — the profile
> **data** form (mockup 05). **Reworked under D-332:** the interests sub-step + the
> save moved to [`mobile-sign-up-interests`](mobile-sign-up-interests.md) (Page 007‑01);
> this screen now ends with **Next**, and a **نوع التسجيل (Visitor/Other)** chip filters
> the ProfileType picker. Spec: [`Page_007`](../../App/Page_007/README.md).
>
> **Status note:** the D-332 rebuild shipped and the D-368 redesign restyled it;
> `sign_up_visitor_screen_test.dart` covers the type filter, validation,
> Saudi/non-Saudi document branches, the organisation gate and the Next→draft
> hand-off on the **new** screen. Scenario wording that says "chips" reads as
> the design's segmented tabs since D-368.

| | |
|--|--|
| **Page** | [`Page_007`](../../App/Page_007/README.md) (App page docs) |
| **Route** | app screen #7 `signUpVisitor` → `/sign-up/visitor` (**auth-gated** — Page_007 L-1) |
| **APIs** | `GET /api/v1/app/account/user-profile` (pre-fill); lookups `GET …/user-profile/countries`, `GET …/profile-types?isVisitor=`, `GET /app/organisations?search=&top=`. **No POST and no interests lookup on this screen** — those are on Page 007‑01. All signed-in, no role/permission (D7). |
| **Surface** | Mobile (Flutter) — Visitor (signed-in, profile-incomplete) |
| **Auth setup** | A signed-in Visitor token (own `sub`). Obtain via the standard app sign-in; never a literal secret. |
| **Last reviewed** | 2026-06-11 |

> **KSA-Project redesign (D-368, Figma 168:2972):** the form now lives in the
> beige card under the login-style navy header (logo + forum name + wired
> globe language toggle); visitor/other + document-type are beige segmented
> tabs, gender is two radio pills, the attach control is the bordered
> إرفاق ملف box, and an underlined terms link opens Page 009. **The
> lookups/prefill/typeahead/validation/draft contract is unchanged.** Deltas:
> the frame's "رقم اللوحة (اختياري)" has no backend field and is not rendered;
> DOB, place of birth and the Saudi switch + national-ID path are kept
> (API-required) though the frame omits them. Old screen parked in
> `lib/features/_legacy_mockup/`. Live browser check N/A (auth-gated);
> widget tests cover render + contract.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB007-001 | نوع التسجيل shows **2** chips (Visitor/Other); picking one filters the ProfileType picker via `?isVisitor=` | happy | P0 | spec (D-332) |
| E2E-MOB007-002 | Changing Visitor↔Other re-filters ProfileType and clears a now-invalid ProfileType selection | edge | P1 | spec (D-332) |
| E2E-MOB007-003 | Valid data filled → **Next** → interests screen (Page 007‑01), carrying the form state; **no POST** here | happy | P0 | spec (D-332) |
| E2E-MOB007-004 | First-time empty → required errors (Arabic/English name, nationality, DOB); Next blocked; no request | error | P0 | spec (D-332) |
| E2E-MOB007-005 | Saudi national ID — `^1\d{9}$` + Luhn client-side, re-checked server-side | validation | P1 | authored ✓ (Luhn unit + server) |
| E2E-MOB007-006 | Non-Saudi → Iqama/Passport picker; Iqama `^2\d{9}$`+Luhn / passport 6–9; one required | validation | P1 | authored ✓ (server `When(!IsSaudi)`) |
| E2E-MOB007-007 | Date of birth required + registrant ≥ 18 (picker caps at today−18, D-197) | validation | P0 | authored ✓ (server + picker) |
| E2E-MOB007-008 | Organisation typeahead — debounced search, select sets id, Clear unlinks; empty state | happy | P1 | spec (D-332) |
| E2E-MOB007-009 | Auth gate — anonymous open redirects to sign-in | auth | P0 | authored ✓ (router-gate test) |
| E2E-MOB007-010 | Empty lookup → picker empty state, never a blocking error | edge | P1 | spec (D-332) |
| E2E-MOB007-011 | RTL render (Arabic) mirrors; lookup labels switch with locale | i18n | P1 | spec (D-332) |
| E2E-MOB007-012 | Saudi mobile standard (C4, D-371): `0501234567` / `+966501234567` / separators accepted; `04…`, 9/11 digits, `+9664…` rejected — client inline + server 400 | validation | P0 | authored ✓ (client `phone_validation_test` + server `UserProfileTests` theories) |
| E2E-MOB007-013 | International mobile E.164 (C4, D-371): `+447700900123` accepted (dash ok); `0044…`, `+0…`, too-short rejected — client inline + server 400 | validation | P0 | authored ✓ (client `phone_validation_test` + server `UserProfileTests` theories) |
| E2E-MOB007-014 | Visitor type lock (C5, D-371): Visitor tab shows **no** profile-type picker; the draft auto-carries the seeded **"Normal" (عادي)** id; server rejects any other audience-tier self-pick with 400 | validation | P0 | authored ✓ (widget test + server C5 tests) |
| E2E-MOB007-015 | Other tab (C5, D-371): the filtered picker is shown and a pick is **required** (inline error blocks Next); partner-side picks accepted by the server | validation | P0 | authored ✓ (widget test + server C5 test) |
| E2E-MOB007-016 | Plate number (C6, D-371): optional — empty saves fine; `ABJ1234` / `abj 1234` / `1234-ABJ` / `أبج1234` accepted and stored normalized upper-cased; 2/4 letters, 5 digits, digits-only, symbols rejected — client inline + server 400 | validation | P0 | authored ✓ (client `plate_validation_test` + server `UserProfileTests` theories incl. stored-value round-trip) |
| E2E-MOB007-017 | Male photo gate (C7, D-371): gender=male + no stored/attached photo → Next blocked with the camera-capture error; female without a photo proceeds (optional). **D-434:** the attach box now launches the shared guided face-capture / liveness screen (`identityVerification`), not a bare camera picker — the returned selfie becomes the attached ID image | validation | P0 | authored ✓ (widget tests; live face-capture drive shares the My-Area avatar flow — needs a real device/emulator) |
| E2E-MOB007-018 | Face check (C7, D-371): the shared liveness capture (smile→turn→turn) verifies a live human face on-device; the server's offline FaceAiSharp gate rejects a no-face/undecodable upload with 400 `VISITOR_ID_IMAGE_NO_FACE` (audited) | validation | P0 | authored ✓ (server `UserProfileFaceGateTests` against the real ONNX model; positive real-face path → Wave-1 live run) |
| E2E-MOB007-019 | Missing-items feedback (D-434): a blocked Next shows the bilingual "complete the required fields" toast (not a silent no-op), and the form carries an info banner on entry so a user routed in to complete their profile knows why | validation | P0 | authored ✓ (`completeProfilePrompt` toast on every blocked-Next test path; banner renders on load) |

## Scenarios

### E2E-MOB007-001 — نوع التسجيل (Visitor / Other) filters the ProfileType picker

```gherkin
Feature: Registration type filters ProfileType
Scenario: Two type chips, each filtering the ProfileType list
  Given a signed-in, profile-incomplete visitor opens /sign-up/visitor
  Then the "نوع التسجيل / Registration type" field shows exactly two chips: "زائر / Visitor" and "أخرى / Other"
  When they pick "Visitor"
  Then the ProfileType picker is loaded with GET /app/account/profile-types?isVisitor=true
  When they pick "Other"
  Then the ProfileType picker is reloaded with ?isVisitor=false
  And the chip is never sent to the server (it only filters the lookup)
```

### E2E-MOB007-002 — Switching type clears an invalid ProfileType

```gherkin
Scenario: Re-filtering drops a now-invalid ProfileType
  Given "Visitor" is selected and a visitor-scope ProfileType is chosen
  When the visitor switches to "Other"
  Then the ProfileType list re-filters to the Other (isVisitor=false) rows
  And the previously chosen ProfileType is cleared (it is not in the new list)
```

### E2E-MOB007-003 — Valid data → Next → interests (no POST here)

```gherkin
Scenario: The data screen advances to interests without saving
  Given the lookups (countries, profile-types, organisations) load
  When they enter "راكان السالم" / "Rakan Alsalem"
  And they pick nationality "Saudi Arabia"
  And the "Saudi national" toggle is on with national ID "1000000008"
  And they pick a date of birth "2000-01-31"
  And they tap "Next"
  Then NO request is sent to /app/account/user-profile (the save is on Page 007‑01)
  And the app navigates to the interests screen (Page 007‑01) carrying the form state
```

### E2E-MOB007-004 — Required fields block Next

```gherkin
Scenario: An empty first-time data form cannot advance
  Given a first-time (empty) profile is open
  When the visitor taps "Next" without filling anything
  Then the Arabic and English name fields show "This field is required"
  And the nationality field shows "Nationality is required"
  And the date-of-birth field shows "Date of birth is required"
  And the app does not navigate and no request is sent
```

### E2E-MOB007-005 — Saudi national ID shape + Luhn

```gherkin
Scenario: A malformed Saudi national ID is rejected
  Given the "Saudi national" toggle is on
  When the visitor enters "1234567890" (fails Luhn)
  Then the field shows "Invalid national ID (10 digits starting with 1)"
  And "1000000008" passes the client check; the server re-validates prefix + Luhn (D-197)
```

### E2E-MOB007-006 — Non-Saudi Iqama / Passport branch

```gherkin
Scenario: A non-Saudi registrant supplies an Iqama or a passport
  Given the "Saudi national" toggle is off
  Then a segmented control offers "Iqama" and "Passport"; the National ID field is hidden
  When "Iqama" "2000000004" (fails Luhn) is entered → "Invalid Iqama number (10 digits starting with 2)"
  When "Passport" "AB12" (too short) is entered → "Invalid passport number (6–9 letters or digits)"
  And leaving both empty shows "An Iqama or passport number is required"
```

### E2E-MOB007-007 — Date of birth required, age ≥ 18

```gherkin
Scenario: The registrant must be at least 18
  Given the date-of-birth picker is opened
  Then the latest selectable date is today minus 18 years (under-18 is unreachable)
  And advancing without a date of birth shows "Date of birth is required"
  And the server re-validates the 18+ rule (D-197, leap-safe)
```

### E2E-MOB007-008 — Organisation typeahead

```gherkin
Scenario: The organisation field searches and links by id
  When the visitor types part of an organisation name
  Then after a short debounce the app calls GET /app/organisations?search=&top=20
  And selecting a row shows it as chosen with "Clear"; "Clear" unlinks it
  And a search with no matches shows "No organisations found" (never a blocking error)
```

### E2E-MOB007-008b — Organisation is required (D-354)

```gherkin
Scenario: Next is blocked until an organisation is picked
  Given every other field on the data screen is valid
  But no organisation is selected
  When the visitor taps Next
  Then the screen does not navigate to the interests screen
  And an inline error under the organisation field reads
    "Pick your organisation from the list" ("اختر جهتك من القائمة" in Arabic)
```

**Evidence:** `sign_up_visitor_screen_test` — "a profile missing only the
organisation blocks Next (B3 — D-221)".

### E2E-MOB007-009 — Auth gate

```gherkin
Scenario: An anonymous open is impossible
  Given no session
  When /sign-up/visitor is requested
  Then the router redirects to /sign-in (route 7 is in the auth gate, Page_007 L-1)
```

**Evidence:** `router_gate_test` — `routePathRequiresAuth('/sign-up/visitor')` is true.

### E2E-MOB007-010 — Lookup fetch states are always visible (D-375)

```gherkin
Scenario: A lookup that returns no rows shows an empty state
  Given the profile-types (or organisation) lookup returns []
  Then that picker shows its empty state (not a blocking error)

Scenario: A profile-types lookup in flight shows loading, a failure shows retry
  Given the user switches نوع التسجيل to "أخرى" (Other)
  Then while GET /app/account/profile-types?isVisitor=false is in flight the
       التصنيف field shows a loading spinner (never a blank gap)
  When the lookup fails (network / 5xx)
  Then the field stays visible with the bilingual "Could not load the list."
       message and an inline Retry button
  And tapping Retry re-runs the lookup and, on success, renders the dropdown
       with the partner types (Media / Sponsor / Staff)

Scenario: An organisation search failure is not dressed as "no matches"
  Given the user has typed in the الجهة / المنظمة typeahead
  Then while the search is in flight a small spinner row shows
  And a failed search shows "Could not load the list." + Retry —
       "no matches" only ever describes a completed empty search
```

**Evidence:** `sign_up_visitor_screen_test` — "a failed Other profile-types
lookup shows the inline retry — never a silently hidden picker (D-375)".

### E2E-MOB007-011 — RTL render (Arabic)

```gherkin
Scenario: The form mirrors under Arabic
  Given the app language is Arabic
  Then the type chips, sections, fields and toggles mirror right-to-left
  And each lookup row's Arabic label is shown; switching to English flips labels without a re-fetch
```

### E2E-MOB007-017 — Male ID photo via the shared face-capture flow (D-434)

```gherkin
Scenario: The male ID photo is captured by the reused liveness screen
  Given a signed-in male visitor on the complete-profile form with no stored photo
  Then the إرفاق ملف box shows the "A photo is required — capture it with the camera" hint up front
  When the visitor taps the attach box
  Then the app opens the guided face-capture / liveness screen (identityVerification) —
       the same flow the My-Area avatar uses (it requests the camera permission,
       runs the smile→turn-right→turn-left liveness and has a gallery fallback)
  When the liveness completes
  Then the screen returns the captured selfie and the attach box shows the thumbnail + name
  And Next then proceeds (the server still re-checks the face on upload, Page 007‑01)
```

### E2E-MOB007-019 — Blocked Next surfaces the missing items (D-434)

```gherkin
Scenario: A blocked Next is never silent
  Given any required field is missing (e.g. a male with no photo, or no organisation)
  When the visitor taps "Next"
  Then a toast shows "Please complete the required fields below to finish your profile."
       ("يرجى إكمال الحقول المطلوبة أدناه لإنهاء ملفك الشخصي." in Arabic)
  And the individual field errors/hints stay shown
  And the app does not navigate

Scenario: The complete-profile entry shows an attention banner
  Given a signed-in profile-incomplete visitor opens the form
  Then a gold-bordered info banner at the top reads the same "complete the required
       fields" prompt, so the user knows this is the completion step
```

---

_Last reviewed:_ `2026-06-12` by `SIMF Team` — reworked under D-332 (data screen; interests + save → Page 007‑01); C4 phone-standard scenarios (012/013) added under D-371.
