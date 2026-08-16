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
> The server validator already checked both shapes, so the screen itself is
> **UI only** — no new endpoint. The profile's `isSaudi` picks which of the two
> wire fields is edited. **There is deliberately no verification step**: a valid
> shape is saved as typed (canonicalised).
>
> **Storage since the mobile-number collapse.** The number is stored **once**, in
> canonical E.164, on `UserProfile.MobileNumber`. A Saudi mobile IS an
> international mobile with `+966`, so the old `SaudiMobile` / `International‑
> Mobile` pair was one attribute in two columns — a row could hold two different
> numbers with nothing saying which to ring. The pair is still written, in
> lockstep, because every reader still projects it, and **both wire keys
> (`saudiMobile`, `internationalMobile`) are still emitted and accepted** — the
> shipped app decodes them by name. What changed for this screen: the Saudi local
> `05XXXXXXXX` spelling is **stored folded** to `+9665XXXXXXXX`, so a number saved
> as `0501234567` reads back as `+966501234567`. Acceptance is unchanged — both
> spellings are still accepted, and a Saudi local number is still rejected by the
> *international* field.

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
| E2E-MYMOB-010 | **DEF-PHN-003** — the SERVER canonicalises on write: separators stripped, a leading `00` rewritten to `+`, and the Saudi local `05…` spelling folded onto `+966…`, so however it was typed it lands in the column as **one** form | validation | P0 | authored ✓ (`UserProfileTests.POST_stores_the_Saudi_mobile_canonicalised` theory + `..._international_mobile_canonicalised`; `AdminAccountMobileTests.Admin_edit_stores_the_mobile_canonicalised`) |
| E2E-MYMOB-011 | **DEF-PHN-004** — the mobile is required **server-side** too (at least one, Saudi or international), so a save can no longer clear the number the app then refuses to submit without | validation | P0 | authored ✓ (`UserProfileTests.POST_rejects_a_profile_with_no_mobile_at_all`, `..._rejects_a_save_that_blanks_an_existing_mobile`, `..._accepts_an_international_only_mobile_for_a_Saudi`) |
| E2E-MYMOB-012 | **Mobile-number collapse** — the number is stored ONCE in canonical E.164 on `UserProfile.MobileNumber`; the Saudi local spelling folds onto `+966…`; both shipped wire keys still round-trip, Saudi first when both arrive | validation | P0 | authored ✓ (`UserProfileTests.Upsert_of_a_Saudi_number_alone_...`, `..._of_an_international_number_alone_...`, `..._folds_the_Saudi_local_spelling_...`, `Both_mobile_wire_keys_are_still_emitted_over_the_real_HTTP_surface`; `MobileNumberTests.The_saudi_local_spelling_folds_onto_the_international_form`) |
| E2E-MYMOB-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MYMOB-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MYMOB-001 — Golden path: edit the mobile from My-Area

```gherkin
Feature: Add or edit my mobile number
Scenario: A signed-in Saudi user corrects their mobile number
  Given a signed-in user whose profile stores saudiMobile "+966501234567"
  When they open My-Area and tap the "رقم الجوال / Mobile number" row
  Then the screen loads their profile and shows "+966501234567" as the current number
  And the input is pre-filled with "+966501234567"
  When they replace it with "0559876543" and tap "Save"
  Then the app POSTs ONE UpsertUserProfileRequest to /app/account/user-profile
  And the body carries saudiMobile "0559876543"
  # …which the server stores, and reads back, folded as "+966559876543"
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

### E2E-MYMOB-010 — The SERVER canonicalises on write (DEF-PHN-003)

Until this shipped the shape rules stripped separators **only to match** — the
value was persisted exactly as typed. So one column held `+966501234567` (the
app, which canonicalises client-side) and `+966-555987654` (the Control-Panel /
Website `SimfPhoneInput`, which emits `+dial-local`). Two spellings of one
number defeat search, export and de-duplication. Every write path now stores
`MobileNumber.Canonicalize`'s output.

`Canonicalize` is `Normalize` — the *same* normaliser the validator matches
against, not a second copy — plus the Saudi local fold. The fold lives on the
**storage** path and nowhere else, deliberately: `Normalize` stays the match
form, so folding it there would make `0501234567` satisfy the E.164 test and a
Saudi local number would start being accepted into the **international** field.
Widening what is stored must not widen what is accepted.

```gherkin
Scenario Outline: A number is stored in exactly one form, whoever typed it
  Given a signed-in user saving their profile
  When the submitted mobile is "<typed>"
  Then the stored column holds "<stored>"

  Examples:
    | typed             | stored         |
    | +966-501234567    | +966501234567  |   # the CP / Website dash form
    | 050 123-4567      | +966501234567  |   # spaces + dash, then folded
    | 00966501234567    | +966501234567  |   # the 00 international prefix
    | 0044-7700 900123  | +447700900123  |   # international, both rules
    | 05012345          | 05012345       |   # NOT the Saudi mobile shape: not folded
```

**Covered (lower layer):** `UserProfileTests.POST_stores_the_Saudi_mobile_canonicalised`
(theory, 3 cases) + `POST_stores_the_international_mobile_canonicalised`; the
admin path is `AdminAccountMobileTests.Admin_edit_stores_the_mobile_canonicalised`;
the fold itself is `MobileNumberTests`.

### E2E-MYMOB-012 — The mobile-number collapse: one column, both wire keys

`SaudiMobile` and `InternationalMobile` were one attribute in two columns. Two
columns let a row hold two DIFFERENT numbers with nothing on it saying which to
ring, made every reader coalesce, and de-duplicated against nothing. The number
now lives once on `UserProfile.MobileNumber` in canonical E.164; the pair is
written in lockstep as exact complements (Saudi set / international NULL, or the
reverse) because every reader still projects it.

```gherkin
Scenario: A Saudi-only registrant round-trips both wire keys
  Given a signed-in user saving saudiMobile "+966501234567" and no international number
  Then UserProfile.MobileNumber holds "+966501234567"
  And GET /app/account/user-profile returns saudiMobile "+966501234567"
  And it returns internationalMobile null
  And BOTH keys are present in the JSON body

Scenario: An international-only registrant round-trips both wire keys
  Given a signed-in user saving internationalMobile "+447700900123" and no Saudi number
  Then UserProfile.MobileNumber holds "+447700900123"
  And GET /app/account/user-profile returns internationalMobile "+447700900123"
  And it returns saudiMobile null

Scenario: Saudi wins when both arrive
  Given a save carrying BOTH a Saudi and an international number
  Then the canonical column holds the Saudi one
  # the precedence VipRosterService already displays with: SaudiMobile ?? InternationalMobile

Scenario: An admin correction replaces the number rather than adding a second one
  Given a stored Saudi mobile and an admin PUT carrying only an international number
  Then the canonical column holds the international number
  And SaudiMobile is NULL
  # blanking is forbidden, so coalescing would make moving an attendee onto a
  # foreign number impossible
```

**Covered (lower layer):** `UserProfileTests.Upsert_of_a_Saudi_number_alone_fills_the_canonical_column_and_both_wire_keys`,
`..._of_an_international_number_alone_...`, `..._folds_the_Saudi_local_spelling_onto_the_canonical_international_form`,
`Both_mobile_wire_keys_are_still_emitted_over_the_real_HTTP_surface`;
`AdminAccountMobileTests.Admin_edit_sets_the_international_mobile`.

### E2E-MYMOB-011 — The mobile is REQUIRED on the server too (DEF-PHN-004)

The number was mandatory on the app form (D-723) and on the walk-in desk, but
**optional on the server**, so a save could still clear it — and the app would
then refuse to let the user submit the form next time they opened it, with no
way out. The server rule now matches the product rule: **at least one mobile,
Saudi or international**. Resolved toward *required* (not toward making the app
optional) because the number is the event's only non-email contact channel and
the two other write paths already demanded it; requiring "at least one" rather
than "the one matching IsSaudi" keeps a Saudi national reachable on a foreign
number.

```gherkin
Scenario: A save with no mobile at all is rejected
  Given a signed-in user whose profile save carries neither mobile
  When the profile is submitted
  Then the response is 400
       ("A mobile number is required (Saudi or international)." /
        "رقم الجوال مطلوب (سعودي أو دولي).")

Scenario: A later save cannot blank a stored number
  Given a stored Saudi mobile "+966501234567"
  When a save submits a blank mobile
  Then the response is 400
  And the stored number survives the rejected save

Scenario: An international-only number satisfies the rule for a Saudi national
  Given a Saudi user with no Saudi mobile but "+447700900123" international
  Then the save succeeds
```

**Covered (lower layer):** `UserProfileTests.POST_rejects_a_profile_with_no_mobile_at_all`,
`POST_rejects_a_save_that_blanks_an_existing_mobile`,
`POST_accepts_an_international_only_mobile_for_a_Saudi`.

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
