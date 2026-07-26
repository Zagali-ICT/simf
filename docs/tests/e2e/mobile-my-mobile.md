# E2E test catalogue — `My mobile number` (add / edit) (`myMobile`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Owner 2026-07-26 — *"Add /
> Edit phone number in my profile — NO VERIFY, ONLY VALIDATE."* Spec:
> [`mobile/my-mobile`](../../pages/mobile/my-mobile/README.md). Runner-agnostic Gherkin.

| | |
|--|--|
| **Page** | [`my-mobile`](../../pages/mobile/my-mobile/README.md) — `MyMobileScreen` |
| **Route** | app screen #703 `myMobile` → `/my-area/mobile` (**auth-gated**) |
| **APIs** | `GET /api/v1/app/account/user-profile` (load, for the pre-fill + a lossless re-save); **`POST /api/v1/app/account/user-profile`** (the existing full-profile upsert carrying the new mobile). Signed-in, no role/permission (D7). **No OTP endpoint is involved.** |
| **Surface** | Mobile (Flutter) — any signed-in account, opened from My-Area → "رقم الجوال / Mobile number" |
| **Auth setup** | A signed-in token (own `sub`). Obtain via the standard app sign-in; never a literal secret. |
| **Last reviewed** | 2026-07-26 (created for the owner phone request) |

> **What this is.** A self-service add/edit of the profile's mobile number.
> `UserProfile.SaudiMobile` / `.InternationalMobile` already existed and the
> server validator already checked both shapes, so this is **UI only** — no
> schema change, no new endpoint. The profile's `isSaudi` picks which of the two
> fields is edited. **There is deliberately no verification step**: a valid
> shape is saved as typed (normalised).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MYMOB-001 | Golden — open from My-Area → stored number pre-filled → change → Save → one full-profile `POST` → toast → pop back | happy | P0 | authored ✓ (widget) |
| E2E-MYMOB-002 | Add — a profile with no number takes one on a later edit | happy | P0 | authored ✓ (API) |
| E2E-MYMOB-003 | Round-trip fidelity — the edit re-POSTs every other field unchanged; NO field is wiped | happy | P0 | authored ✓ (widget + API) |
| E2E-MYMOB-004 | Validation — Saudi shape, international E.164 shape, required-when-empty; no API call on a reject | validation | P0 | authored ✓ (widget + API) |
| E2E-MYMOB-005 | Normalisation — Arabic-Indic digits, separators, leading `00` → `+` before submit | validation | P1 | authored ✓ (widget + unit) |
| E2E-MYMOB-006 | Server 500 / ApiFailure → message on screen, no navigation, nothing lost | resilience | P1 | authored ✓ (widget) |
| E2E-MYMOB-007 | Load failure → error state + Retry; no write fired | resilience | P1 | authored ✓ (widget) |
| E2E-MYMOB-008 | Auth gate — anonymous open of `/my-area/mobile` redirects to sign-in | auth | P0 | authored ✓ (router-gate matrix) |
| E2E-MYMOB-009 | RTL render (Arabic) — labels mirror, the NUMBER stays LTR | i18n | P1 | spec |

## Scenarios

### E2E-MYMOB-001 — Golden path: edit the mobile from My-Area

```gherkin
Feature: Add or edit my mobile number
Scenario: A signed-in Saudi user corrects their mobile number
  Given a signed-in user whose profile stores saudiMobile "0501234567"
  When they open My-Area and tap the "رقم الجوال / Mobile number" row
  Then the screen loads their profile and shows "0501234567" as the current number
  And the input is pre-filled with "0501234567"
  When they replace it with "0559876543" and tap "Save"
  Then the app POSTs ONE UpsertUserProfileRequest to /app/account/user-profile
  And the body carries saudiMobile "0559876543"
  And NO OTP screen is shown at any point
  And on ApiResult.Ok a "Your mobile number was updated" toast shows and the screen pops back to My-Area
```

### E2E-MYMOB-002 — Add a number that was never set

```gherkin
Scenario: A profile saved without a mobile takes one later
  Given a signed-in user whose profile has no mobile number
  When they open /my-area/mobile
  Then the current-number line reads "لم يُضف بعد / Not added yet" and the input is empty
  When they enter "+966501234567" and tap "Save"
  Then the upsert succeeds and GET /app/account/user-profile returns saudiMobile "+966501234567"
```

### E2E-MYMOB-003 — Round-trip fidelity (no other field is wiped)

```gherkin
Scenario: A mobile-only edit does not null anything else
  Given the loaded profile has organisationId "org-3", regionId "region-7",
        jobTitleArabic "مهندس", nationalId "1000000008" and interestIds ["i1"]
  When the user changes only the mobile and saves
  Then the single full-profile POST still carries organisationId "org-3",
       regionId "region-7", jobTitleArabic "مهندس", nationalId "1000000008" and interestIds ["i1"]
  # The upsert is the only write path and the service sets every field
  # unconditionally, so the screen MUST re-send the whole loaded profile.
```

### E2E-MYMOB-004 — Validation only (the single gate)

```gherkin
Scenario Outline: The mobile shape is validated client-side and server-side
  Given a signed-in <nationality> user on /my-area/mobile
  When they enter "<value>" and tap "Save"
  Then the result is <outcome>

  Examples:
    | nationality | value          | outcome                                            |
    | Saudi       | 0501234567     | saved                                              |
    | Saudi       | +966501234567  | saved                                              |
    | Saudi       | 050 123-4567   | saved (separators stripped)                        |
    | Saudi       | 12345          | rejected with the 05XXXXXXXX / +9665XXXXXXXX message, NO API call |
    | Saudi       | 0401234567     | rejected — not the 05 mobile plan                  |
    | Saudi       | (empty)        | rejected with "Mobile number is required"          |
    | non-Saudi   | +12025550123   | saved                                              |
    | non-Saudi   | +0447700900123 | rejected — leading zero after "+"                  |
    | non-Saudi   | +44            | rejected — too short for E.164                     |
```

### E2E-MYMOB-005 — Normalisation before submit

```gherkin
Scenario: The submitted value is always the canonical form
  Given a signed-in non-Saudi user on /my-area/mobile
  When they enter "00201000000000" and tap "Save"
  Then the POST body carries internationalMobile "+201000000000"
  And saudiMobile is null (the nationality picks exactly one field)
  # Arabic-Indic digits fold to Western and spaces / dashes are stripped the
  # same way (normalizePhone), client and server identically.
```

### E2E-MYMOB-006 — Server error

```gherkin
Scenario: A failed save keeps the user on the screen
  Given POST /app/account/user-profile returns an error
  When the user taps "Save" with a valid number
  Then the error message is shown under the field
  And the screen does NOT pop back to My-Area
  And the typed number is still in the input
```

### E2E-MYMOB-007 — Load failure

```gherkin
Scenario: A profile-load failure shows the error, no write
  Given GET /app/account/user-profile fails
  When the screen opens
  Then the error message + a Retry button are shown
  And no upsert is fired
```

### E2E-MYMOB-008 — Auth gate

```gherkin
Scenario: An anonymous open is impossible
  Given no session
  When /my-area/mobile is requested
  Then the router redirects to /sign-in (route #703 is in the auth gate)
```

### E2E-MYMOB-009 — RTL render (Arabic)

```gherkin
Scenario: The screen mirrors under Arabic but the number does not
  Given the app language is Arabic
  Then the header reads رقم الجوال and the helper + labels are right-aligned
  And the current-number read-out and the input both render the digits left-to-right
  And the "حفظ" button spans the full width at the bottom
```

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` — created for the owner's
"add / edit phone number, validate only, no verify" request.
