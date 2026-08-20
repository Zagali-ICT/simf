# E2E test catalogue — `Sign up — profile data` (`signUpVisitor`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #7 — the profile
> **data** form (mockup 05). **Reworked under D-332:** the interests sub-step + the
> save moved to [`mobile-sign-up-interests`](mobile-sign-up-interests.md) (Page 007‑01);
> this screen now ends with **Next**, and a **نوع التسجيل (Visitor/Other)** chip filters
> the ProfileType picker. Spec: [`mobile/sign-up-visitor/`](../../pages/mobile/sign-up-visitor/README.md).
>
> **Status note:** the D-332 rebuild shipped and the D-368 redesign restyled it;
> `sign_up_visitor_screen_test.dart` covers the type filter, validation,
> Saudi/non-Saudi document branches, the organisation gate and the Next→draft
> hand-off on the **new** screen. Scenario wording that says "chips" reads as
> the design's segmented tabs since D-368.
>
> **Clean-code freeze (D-546, 2026-06-30):** the screen was decomposed (behaviour
> unchanged) and locked by the golden `sign_up_visitor_168-2972.png`; this catalogue
> still applies verbatim. Full per-page doc:
> [`mobile/sign-up-visitor/`](../../pages/mobile/sign-up-visitor/README.md).

| | |
|--|--|
| **Page** | [`Page_007`](../../App/Page_007/README.md) (App page docs) |
| **Route** | app screen #7 `signUpVisitor` → `/sign-up/visitor` (**auth-gated** — Page_007 L-1) |
| **APIs** | `GET /api/v1/app/account/user-profile` (pre-fill); lookups `GET …/user-profile/countries`, `GET …/profile-types?isVisitor=`, `GET /app/organisations?search=&top=`. **No POST and no interests lookup on this screen** — those are on Page 007‑01. All signed-in, no role/permission (D7). |
| **Surface** | Mobile (Flutter) — Visitor (signed-in, profile-incomplete) |
| **Auth setup** | A signed-in Visitor token (own `sub`). Obtain via the standard app sign-in; never a literal secret. |
| **Last reviewed** | 2026-08-20 (the type-ahead race guard + the clamped date-of-birth picker, MOB007-026/027) |

> **KSA-Project redesign (D-368, Figma 168:2972):** the form now lives in the
> beige card under the login-style navy header (logo + forum name + wired
> globe language toggle); visitor/other + document-type are beige segmented
> tabs, gender is two radio pills (gold ring on the leading/right edge, then the
> label — D-698), the attach control is the bordered
> إرفاق ملف box, and an underlined terms link opens Page 009. **The
> lookups/prefill/typeahead/validation/draft contract is unchanged.** Deltas:
> the plate field "رقم اللوحة (اختياري)" is rendered as three 17-letter Saudi
> plate dropdowns (Arabic · Latin) + a 1–4 digit field (D-459);
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
| E2E-MOB007-015 | Other tab (C5, D-371): the filtered ProfileType **dropdown/select** (D-722 — a simple dropdown, not the full-screen search sheet) is shown and a pick is **required** (inline error blocks Next); partner-side picks accepted by the server | validation | P0 | authored ✓ (widget test + server C5 test) |
| E2E-MOB007-016 | Plate number (C6, D-371/D-459): optional — empty saves fine; restricted to the **official 17 Saudi plate letters** (Arabic or Latin), 3 letters + 1–4 digits, either order; `ABJ1234` / `abj 1234` / `1234-ABJ` / `ابح1234` / `ابح١٢٣٤` accepted and stored as the canonical Latin **code**; the response returns both `plateNumberAr` (ابح١٢٣٤) and `plateNumberEn` (ABJ1234); 2/4 letters, 5 digits, digits-only, symbols, and out-of-set letters (C, ج) rejected — app uses three 17-letter **searchable pickers** (the same beige search sheet as the country/region picker, D-471); client inline + server 400 | validation | P0 | authored ✓ (client `plate_validation_test` + `SaudiPlateTests` + server `UserProfileTests` theories incl. AR/EN round-trip; widget open-sheet test) |
| E2E-MOB007-017 | **Two-photo split (D-437):** the form ends with **two** image actions — **"Upload ID"** (gallery pick of the ID DOCUMENT, **mandatory for everyone**, no face check) and **"Face photo"** (captured via the existing **face-detection / liveness page** `identityVerification` → the avatar, **mandatory for men, optional for women**). A missing ID blocks Next with "An ID image is required" (all genders); a male missing the face photo blocks with "A face photo is required — capture it with the camera" (shown up front); a female proceeds with the ID alone | validation | P0 | authored ✓ (widget tests; live face-capture drive shares the My-Area avatar flow — needs a real device, verified on the Huawei) |
| E2E-MOB007-018 | **Face capture page (D-437):** the FACE photo reuses the guided face-detection / liveness screen (smile→turn→turn, **live-only — no gallery fallback, D-662**) the My-Area avatar uses — it owns the camera permission + the on-device face/liveness check; the returned selfie becomes the avatar. Route 103 is **universal-auth (D-694)** so a pending sign-up account reaches it instead of bouncing to Home. The self-service **id-image endpoint no longer face-gates** (it is a document now); the admin walk-in id-document path keeps its server FaceAiSharp gate | validation | P0 | authored ✓ (server `UserProfileFaceGateTests`; router `router_role_matrix_test` + flow `app_flows_test` prove pending reaches route 103; the liveness capture is verified live on the Huawei) |
| E2E-MOB007-019 | Missing-items feedback (D-434): a blocked Next shows the bilingual "complete the required fields" toast (not a silent no-op), and the form carries an info banner on entry so a user routed in to complete their profile knows why | validation | P0 | authored ✓ (`completeProfilePrompt` toast on every blocked-Next test path; banner renders on load) |
| E2E-MOB007-020 | **Top avatar (D-437):** once the face photo is captured, the placeholder person icon at the top of the card is replaced by the captured face | happy | P1 | authored ✓ (widget — header avatar swaps to the captured bytes) |
| E2E-MOB007-021 | **Name rules (D-437/D-459):** the Arabic name accepts only Arabic letters + spaces (Latin/digits filtered at the keystroke); the English name only Latin letters + spaces; each must be a **full name of 2 to 4 parts** or Next is blocked with "Enter your full name (2 to 4 parts)". Mirrored server-side in `UpsertUserProfileRequestValidator` (400) | validation | P0 | authored ✓ (widget formatter+validator test + server name-rule tests) |
| E2E-MOB007-022 | **Birth location (D-469/D-471):** a Saudi registrant's "place of birth" is a **searchable region picker** — the same beige search sheet the country picker uses (D-471) — over the 13 official Saudi regions (stored as the region's localized name); a non-Saudi gets the free-text field with an "as in passport" hint. The picker is **code-keyed**, so a region stored in the other language still preselects, and a stored value that is not a region (legacy free text) is kept, not erased. Switching nationality Saudi↔non-Saudi reconciles the field | validation | P1 | authored ✓ (widget — prefill + cross-locale select + open-search-pick; `SaudiRegionsTests` ByName/ByCode) |
| E2E-MOB007-023 | **All fields mandatory except plate + Arabic job title (owner item 4 / D-723; #37):** job title, place of birth (Saudi region / non-Saudi free text) and the mobile number are now **required** — an empty one blocks Next with its inline error ("Job title is required" / "Place of birth is required" / "Mobile number is required"); only the plate and the Arabic job title stay optional. The women's face-photo exception (D-694) is unchanged | validation | P0 | authored ✓ (widget — each empty field blocks Next) |
| E2E-MOB007-024 | **Arabic job title (backlog #37):** an **optional** "المسمى الوظيفي (بالعربية)" input sits right after the job title (RTL). Leaving it empty still advances Next; when filled, `Next` carries it into the profile upsert (`jobTitleArabic`) + the interests-screen draft, so `UserProfile.JobTitleArabic` (already carried by the backend + CP) is finally captured by the app. Prefilled on re-entry from the stored profile | happy | P1 | authored ✓ (widget — prefill → upsert + draft round-trip) |
| E2E-MOB007-025 | **Job-title labels + per-script masks (owner request):** the two job-title fields are language-labelled — "المسمى الوظيفي (بالإنجليزية)" (English, LTR) and "المسمى الوظيفي (بالعربية)" (Arabic, RTL) — so which is which is unambiguous. Each takes the **same per-script keystroke filter as its name field**: the English job title accepts **Latin letters + spaces only** (Arabic/digits/punctuation filtered at the keystroke); the Arabic job title accepts **Arabic letters + spaces only** (Latin filtered). Neither field can ever hold the other's script | validation | P1 | authored ✓ (golden `sign_up_visitor_168-2972` shows both labels; formatters mirror the verified name-field filters E2E-MOB007-021) |
| E2E-MOB007-026 | **Type-ahead results always match the text in the box:** two organisation searches in flight at once resolve out of order, and the list shows the **latest** query's rows — a slow earlier response is dropped, never painted over the newer one | edge | P0 | authored ✓ (`organisation_typeahead_race_test`) |
| E2E-MOB007-027 | **A stored date of birth outside 18–120 opens the picker clamped, not crashed:** the picker's initial date is pulled inside `firstDate..lastDate` before `showDatePicker` runs, so a server profile holding an under-18 or over-120 date opens on the nearest eligible date instead of tripping the framework assert. The window itself is measured from the **Saudi** clock, not the device clock | edge | P0 | authored ✓ (`sign_up_visitor_pickers_test`, 9 cases) |
| E2E-MOB007-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB007-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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
       الفئة field shows a loading spinner (never a blank gap)
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

### E2E-MOB007-017 — Two-photo split: Upload ID (gallery) + Face photo (camera) (D-437)

```gherkin
Feature: Two distinct profile images
Scenario: The ID document is uploaded from the gallery (mandatory for all)
  Given a signed-in visitor on the complete-profile form with no stored ID image
  Then the "Upload ID" box shows the "An ID image is required" hint
  When the visitor taps the box
  Then the gallery opens (image_picker, ImageSource.gallery) — no camera, no face check
  And picking a document shows its thumbnail + filename + Remove
  And without an ID image Next is blocked for every gender

Scenario: The face photo is captured via the face-detection page (mandatory for men)
  Given a signed-in male visitor with the ID document attached but no face photo
  Then the "Face photo" field shows "A face photo is required — capture it with the camera" up front
  When the visitor taps the capture box
  # Route 103 is universal-auth (D-694) — a pending sign-up account reaches it, not Home.
  Then the app opens the existing face-detection / liveness page (identityVerification) —
       the same flow the My-Area avatar uses (it owns the camera permission and the
       on-device face + liveness check; live-only, no gallery fallback — D-662)
  When the liveness completes
  Then the returned selfie is shown as the captured face and Next proceeds

Scenario: A woman may skip the face photo
  Given a signed-in female visitor with the ID document attached and no face photo
  When she taps Next
  Then the app proceeds to the interests screen (the face photo is optional for women)
```

### E2E-MOB007-020 — The face photo replaces the top placeholder icon (D-437)

```gherkin
Scenario: The captured face is shown at the top of the card
  Given the complete-profile form is open and the card head shows the placeholder person icon
  When the visitor captures a face photo
  Then the placeholder icon at the top is replaced by the captured face image
```

### E2E-MOB007-021 — Arabic-only / English-only full-name (2–4 parts) (D-437/D-459)

```gherkin
Feature: Name rules
Scenario: The Arabic field accepts only Arabic letters
  Given the complete-profile form is open
  When the visitor types "Ahmed 123" into the Arabic name field
  Then the Latin letters and digits are filtered out at the keystroke (the field never holds them)

Scenario: A full name needs 2 to 4 parts in one language
  Given the Arabic name is "محمد" (one part) and every other field is valid
  When the visitor taps Next
  Then the app does not navigate
  And the Arabic name shows "Enter your full name (2 to 4 parts)"
  And a 5-part name is likewise rejected (the ceiling is 4)
  And the same rule applies to the English name (Latin letters only, 2–4 parts)
  And the server's UpsertUserProfileRequestValidator re-checks both (400 on violation)

Scenario: Tashkeel is part of an ordinary Arabic name (BUG-021)
  Given the complete-profile form is open
  When the visitor types "محمَّد عبدالله" (fatha + shadda on the meem) into the Arabic name
  Then every character is kept — the keystroke filter accepts U+0621-U+0652
       (Arabic letters, tatweel and the tashkeel marks), not only U+0621-U+064A
  And tapping Next saves: the server accepts the shadda-bearing name
  And "محمد Ahmed" is still filtered / rejected, and digits are still rejected
```

### E2E-MOB007-022 — Birth location: Saudi region dropdown / non-Saudi free text (D-469)

```gherkin
Feature: Birth location
Scenario: A Saudi registrant picks a region from the searchable picker (D-471)
  Given the "Saudi national" branch is active
  Then the "place of birth" field opens the SAME beige searchable sheet the
       country picker uses (a type-to-filter list with a search box + magnifier icon)
  And the sheet lists the 13 official Saudi regions
       (Riyadh, Makkah, Al Madinah, Eastern Province, Asir, Tabuk, Hail,
        Northern Borders, Jazan, Najran, Al Bahah, Al Jawf, Al Qassim)
  When the registrant types part of a region name and taps the match
  Then its localized name is stored in the existing place-of-birth field (no schema change)

Scenario: A stored region preselects regardless of the language it was saved in
  Given a Saudi profile whose stored place of birth is "منطقة الرياض" (Arabic)
  When the form is opened under an English UI
  Then the dropdown preselects "Riyadh" (it is keyed on the region code, not the name)

Scenario: A non-region stored value is kept, not erased
  Given a Saudi profile whose stored place of birth is a free-text city ("Jeddah City")
  Then the dropdown shows the "Select region" placeholder
  And the stored value is preserved unless the registrant actively picks a region

Scenario: A non-Saudi registrant types the place of birth
  Given the "Saudi national" branch is off
  Then the place-of-birth field is a free-text input with the hint "As in your passport"

Scenario: Switching nationality reconciles the field
  Given a Saudi registrant has picked "Riyadh"
  When they switch to a non-Saudi nationality
  Then the field becomes free text carrying the prior value
  When they switch back to Saudi
  Then the value reselects the region if it maps to one, else the field clears
```

**Evidence:** `sign_up_visitor_screen_test` — "a Saudi profile shows the
birth-location region dropdown with the stored region selected (D-469)" and "a
region stored in Arabic still selects under an English UI". Shared constant +
either-language lookup: `SaudiRegionsTests` (ByName/ByCode).

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

### E2E-MOB007-024 — Optional Arabic job title captured (backlog #37)

```gherkin
Feature: Arabic job title
Scenario: The optional Arabic job title is not required
  Given a signed-in visitor on the complete-profile form
  Then an optional "المسمى الوظيفي (بالعربية)" input renders right after the
       job title (right-to-left)
  When every required field is valid and the Arabic job title is left empty
  Then Next still advances to the interests screen (the field is optional)

Scenario: A filled Arabic job title is carried into the save
  Given the visitor types "مهندس بحري" into the Arabic job title
  When they tap Next
  Then the profile upsert (POST /app/account/user-profile on the interests step)
       carries jobTitleArabic = "مهندس بحري"
  And the value persists to UserProfile.JobTitleArabic (already carried by the
      backend + CP)

Scenario: The stored Arabic job title prefills on re-entry
  Given a profile whose stored JobTitleArabic is "مهندس بحري"
  When the form is reopened
  Then the Arabic job title field is prefilled with "مهندس بحري"
```

**Evidence:** `sign_up_visitor_screen_test` — "carries the optional Arabic job
title into the saved request (backlog #37)".

---

### E2E-MOB007-025 — Job-title labels + per-script masks (owner request)

```gherkin
Feature: Job-title fields are language-labelled and script-masked
Scenario: The two job-title fields carry a language marker
  Given a visitor on the complete-profile form
  Then the English job-title field is labelled "المسمى الوظيفي (بالإنجليزية)"
  And the Arabic job-title field is labelled "المسمى الوظيفي (بالعربية)"

Scenario: The English job title accepts Latin letters only
  When the visitor types "Marine مهندس 12" into the English job title
  Then only "Marine " is kept (Arabic letters and digits filtered at the keystroke)

Scenario: The Arabic job title accepts Arabic letters only
  When the visitor types "مهندس Marine" into the Arabic job title
  Then only "مهندس " is kept (Latin letters filtered at the keystroke)
```

**Evidence:** golden `sign_up_visitor_168-2972` renders both language labels; the
formatters are the same verified per-script filters as the name fields
(E2E-MOB007-021).

### E2E-MOB007-026 — The organisation list always matches what is in the box

```gherkin
Scenario: A slow earlier search never overwrites the latest results
  Given the visitor is on the profile form
  When they type "min" into جهة العمل and the app issues GET /app/organisations?search=min
  And they keep typing to "ministry" and the app issues a second search
  And the "ministry" response arrives FIRST and lists "Ministry"
  And the "min" response arrives AFTERWARDS listing "Minority"
  Then the field still lists "Ministry"
  And "Minority" is not shown
  And selecting a row still links that organisation's id
```

**Why it matters:** the debounce cancels a pending timer, not a request already in
flight, so two searches are routinely outstanding and the network decides which
lands last — on congested venue WiFi that is often the older one. Organisation is
**required** (D-221 / E2E-MOB007-008b), so the user could not simply skip past a
list showing matches for text they had already replaced: they either picked the
wrong employer or were told there was no match for a query that had one.

**Evidence:** `organisation_typeahead_race_test` — "a slow earlier search does not
overwrite the latest results". The test resolves the two futures out of order on
purpose and fails without the generation guard in the field's `_run`.

### E2E-MOB007-027 — An out-of-range stored date of birth opens the picker clamped

```gherkin
Scenario Outline: The picker opens whatever the stored date of birth is
  Given the Saudi clock reads 2026-08-20
  And the eligible window is 1906-01-01 (120 years back) .. 2008-08-20 (the 18th birthday)
  And the stored profile holds the date of birth <stored>
  When the visitor taps تاريخ الميلاد
  Then the date picker dialog opens
  And its initial date is <opens on>

  Examples:
    | stored     | opens on   | why                              |
    | 2020-05-04 | 2008-08-20 | under 18, clamped to lastDate    |
    | 1890-05-04 | 1906-01-01 | over 120, clamped to firstDate   |
    | 1990-05-04 | 1990-05-04 | eligible, honoured verbatim      |
    | (none)     | 2008-08-20 | nothing stored yet               |

Scenario: The age boundary is the Saudi date, not the device's
  Given the eligible window is derived from saudiNow(), never DateTime.now()
  Then a device clock a day behind Riyadh cannot move the 18th-birthday boundary
```

> The two clamped rows are also driven through the real dialog, because the unit
> assertions prove the clamp and only opening the picker proves it is wired to
> `initialDate`. The Saudi-clock scenario is asserted **by source inspection**, not
> behaviour: every other case injects `now`, so a device clock reinstated as the
> parameter default would walk straight past them, and on a +03:00 box (every SIMF
> dev box and CI agent) the two clocks agree, so no in-process behavioural test can
> tell them apart.

**Why it matters:** `showDatePicker` **asserts** its `initialDate` lies within
`firstDate..lastDate`. The seed is the stored profile's date of birth, and the
server never range-checks that value against the app's 18-to-120 rule — so an
out-of-window date tripped the assert in debug and seeded an out-of-range picker in
release. The user hitting this is exactly the one who most needs to correct the
field. The Saudi-clock rule is the same one the rest of the app follows for
user-facing dates (D-219 / D-770): an 18th birthday read off a traveller's phone
lands on the wrong day for them.

**Evidence:** `sign_up_visitor_pickers_test` — "the newest eligible date of birth is
exactly the 18th birthday", "the oldest eligible date of birth is 120 years back",
"a date of birth younger than 18 seeds at the newest eligible date", "a date of
birth older than 120 seeds at the oldest eligible date", "an eligible date of birth
is seeded verbatim", "nothing stored yet seeds at the newest eligible date", group
"the picker opens on an out-of-range stored date", and "the default clock is the
Saudi one, not the device clock".

---

_Last reviewed:_ `2026-08-20` by `SIMF Team` — the organisation type-ahead's
out-of-order search guard (026) and the clamped, Saudi-clock date-of-birth picker
(027); the screen's three async paths now clear their busy flag in a `finally` (see
the page doc, section 5). Earlier: `2026-07-25` — job-title fields language-labelled (بالإنجليزية / بالعربية) and given the same per-script keystroke masks as the name fields (025); goldens `sign_up_visitor_168-2972` + `staff_register_visitor_1467-12357` regenerated. Earlier: `2026-06-20` — D-471 the birth-region + the 3 plate-letter fields now use the SAME beige searchable picker as the country picker (one shared `_LookupSearchSheet`) (016/022). D-469 Saudi birth-location region picker (code-keyed, cross-locale, non-region values preserved) (022). Earlier: D-459 name 2–4 parts + the 17-letter Saudi plate dropdowns (canonical code + AR/EN renderings) (016/021); D-437 two-photo split (Upload ID gallery + Face photo camera), Arabic-only/English-only name rules, top-avatar swap, and the self-service id-image face-gate removal (017/018/020/021); D-332 data-screen rework; D-371 C4 phone standards.
