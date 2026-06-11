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

### E2E-MOB007-010 — Empty lookup is not an error

```gherkin
Scenario: A lookup that returns no rows shows an empty state
  Given the profile-types (or organisation) lookup returns []
  Then that picker shows its empty state (not a blocking error)
```

### E2E-MOB007-011 — RTL render (Arabic)

```gherkin
Scenario: The form mirrors under Arabic
  Given the app language is Arabic
  Then the type chips, sections, fields and toggles mirror right-to-left
  And each lookup row's Arabic label is shown; switching to English flips labels without a re-fetch
```

---

_Last reviewed:_ `2026-06-11` by `SIMF Team` — reworked under D-332 (data screen; interests + save → Page 007‑01).
