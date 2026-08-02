# E2E test catalogue — Account accessibility preferences (`GET`/`PUT /api/v1/app/account/preferences`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md). This is the **server half** of
> `accessibility-server-sync`; the app half (the #38 screen, the write-through and
> the hydrate-at-sign-in) is catalogued in
> [`mobile-accessibility.md`](mobile-accessibility.md) (E2E-MOB038-007..012).

| | |
|--|--|
| **Page** | [`mobile/accessibility/`](../../pages/mobile/accessibility/README.md) (the app screen that drives it) · [`api/account-preferences.md`](../../pages/api/account-preferences.md) (the endpoint reference) |
| **Route** | `GET /api/v1/app/account/preferences` · `PUT /api/v1/app/account/preferences` |
| **Surface** | App API (signed-in app user; no Control-Panel surface) |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/AccountPreferencesTests.cs`) |
| **Auth setup** | An **Approved** visitor bearer token. The fixture signs up, verifies the email, sets `AccountState.Approved`, disables 2FA and signs in (`AuthFlow.Password`) — **no literal secrets**. |
| **Last reviewed** | 2026-07-31 |

## What this endpoint is, and why it exists

The five accessibility choices — text size, high contrast, reduce motion,
screen-reader assist and captions — used to live in **device prefs only**. They
therefore did not follow the user to a second device and did not survive a
reinstall, which is the whole point of an accessibility setting: the person who
needs `extraLarge` needs it on the phone they are holding today. Register item
`accessibility-server-sync`.

They are now **account** settings, stored on the caller's `UserProfile` row
(`SIMF_App`), which already carries the bare `UserId` — D-157, so nothing here
crosses into `SIMF_Identity` and no FK spans the two databases. The device prefs
stay as the app's offline cache and its only **read** path on first frame.

| Piece | File |
|---|---|
| Endpoints | `src/Backend/SIMF.Api/Endpoints/Account/AccountPreferencesEndpoints.cs` (`AccountPreferencesGetEndpoint` / `AccountPreferencesUpdateEndpoint`) |
| Contract | `src/Shared/SIMF.Contracts/Account/AccountPreferences.cs` (+ `UpdateAccountPreferencesRequest`) |
| Service | `IAccountPreferencesService` (Application) → `AccountPreferencesService` (Infrastructure) |
| Storage | `UserProfile.AccessibilityTextSize` / `…HighContrast` / `…ReduceMotion` / `…ScreenReaderAssist` / `…Captions` (additive columns, `UserProfileConfiguration`) |
| Tests | `tests/SIMF.Api.Tests/AccountPreferencesTests.cs` (8 facts) |

## The contract, in one place

```
GET  /api/v1/app/account/preferences        → 200 ApiResult<AccountPreferences>
PUT  /api/v1/app/account/preferences        → 200 ApiResult<AccountPreferences>
```

Both carry `Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))` and
resolve the subject from the caller's own `sub` claim. **No admin permission
code** — this is an app-user surface, so there is nothing for a
`PermissionCatalog` entry to gate; a caller can only ever read and write their
own row, and no route parameter names another account.

Body and payload, camelCase on the wire (FastEndpoints' web defaults):

| Field | Type | Default | Notes |
|---|---|---|---|
| `textSize` | string | `"normal"` | One of `small` · `normal` · `large` · `extraLarge`. The app's **stable enum name**, never an index, matched **case-sensitively**. |
| `highContrast` | bool | `false` | تباين عالٍ |
| `reduceMotion` | bool | `false` | تقليل الحركة |
| `screenReaderAssist` | bool | `false` | قارئ الشاشة |
| `captions` | bool | **`true`** | الترجمة النصية — the one choice that defaults **on**. |

The `PUT` is a **full replace**, so it is idempotent: the same body twice leaves
the same row. Every field is optional and falls back to its shipped default, so
a partial body from an older build stores a complete, well-defined set rather
than being rejected.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ACP-001 | Golden path — `PUT` all five away from their defaults, `GET` reads them back | happy | P0 | automated (`Saved_preferences_round_trip_through_a_second_read`) |
| E2E-ACP-002 | Empty state — an account that never saved reads the **defaults**, not a 404 | happy | P0 | automated (`Preferences_are_the_defaults_for_an_account_that_never_saved`) |
| E2E-ACP-003 | Repeating the same `PUT` is idempotent — one profile row, identical response | happy | P0 | automated (`Saving_the_same_preferences_twice_is_idempotent`) |
| E2E-ACP-004 | A `PUT` before the registration form seeds a **stub** profile that still reads as incomplete | edge | P0 | automated (`Saving_preferences_before_registration_leaves_the_profile_incomplete`) |
| E2E-ACP-005 | Unknown `textSize` → 400 `VALIDATION_FAILED`, bilingual, field `textSize`, **nothing written** | error | P0 | automated (`An_unknown_text_size_is_rejected_with_a_bilingual_error`) |
| E2E-ACP-006 | Auth gate — no bearer token → 401 on **both** verbs | auth | P0 | automated (`Preferences_without_a_token_return_401`) |
| E2E-ACP-007 | Auth gate — a verified but **not-yet-approved** account → 403 on both verbs | auth | P0 | automated (`Preferences_for_a_not_yet_approved_account_are_forbidden`) |
| E2E-ACP-008 | Wire contract — the five camelCase keys and the `textSize` **name** are frozen | happy | P0 | automated (raw-body assertions in `Preferences_are_the_defaults_…`) |
| E2E-ACP-009 | A stored blank / unknown text size degrades to `normal` on read, never to a value the app drops | edge | P1 | automated (`The_stored_default_text_size_matches_the_contract_default`) + service branch |
| E2E-ACP-010 | Cross-device — device B's first `GET` replays what device A saved | happy | P0 | _to author_ (driven manually; the app half is E2E-MOB038-009) |
| E2E-ACP-011 | Server 500 / unreachable — the envelope is still `ApiResult`, and the app keeps the local choice | resilience | P1 | _to author_ (client half automated as E2E-MOB038-008 / -010) |
| E2E-ACP-012 | RTL / bilingual — the Arabic error text is real Arabic and is **not** the English string | i18n | P1 | automated (`An_unknown_text_size_is_rejected_with_a_bilingual_error`) |
| E2E-ACP-013 | The `PUT` is rate-limited on the `auth` partition (20 / 60 s per IP); the `GET` is not | resilience | P2 | _to author_ |

## Scenarios

### E2E-ACP-001 — Golden path: save, then read back on a second call

```gherkin
Feature: Accessibility preferences follow the account
  As a user who has set the app up for my eyesight
  I want those choices stored against my account
  So that a new phone or a reinstall does not lose them

Background:
  Given an Approved visitor "prefs-visitor@simf.test" holding a bearer token
  And the account has never saved a preference

Scenario: Every value is stored and read back
  When the client PUTs /api/v1/app/account/preferences with
    | textSize           | extraLarge |
    | highContrast       | true       |
    | reduceMotion       | true       |
    | screenReaderAssist | true       |
    | captions           | false      |
  Then the response is 200 with success = true
  And data.textSize is "extraLarge"
  And data.captions is false
  When the client GETs /api/v1/app/account/preferences
  Then the response is 200
  And data is exactly { extraLarge, true, true, true, false }
  And the UserProfile row for that account stores AccessibilityTextSize = "extraLarge"
  And AccessibilityCaptions = false
```

**Why `captions: false` is the load-bearing value here.** Its column carries a
`HasDefaultValue(true)`, so an explicit **false** on the very first save is the
one write EF could silently drop — the property value would equal the CLR
default sentinel and be omitted from the `INSERT`, and the column default would
put it back ON. `UserProfileConfiguration` pins `HasSentinel(true)` for exactly
that reason, and this scenario is what proves it.

**Evidence captured:**
`AccountPreferencesTests.Saved_preferences_round_trip_through_a_second_read` —
including the direct `SimfAppDbContext` read that asserts the **name**
`"extraLarge"` reached the column, not an index.

### E2E-ACP-002 — Empty state: the defaults, never a 404

```gherkin
Scenario: A fresh account reads the shipped defaults
  Given an Approved visitor who has never saved a preference
  And who has not yet filled the registration form (so there is no UserProfile row)
  When the client GETs /api/v1/app/account/preferences
  Then the response is 200 — NOT 404
  And data.textSize is "normal"
  And data.highContrast, data.reduceMotion and data.screenReaderAssist are all false
  And data.captions is true
```

A 404 here would be wrong twice over: "I have not chosen yet" is a value, not a
missing resource, and the app's **first** read on a fresh device happens before
any write — a 404 would make the very first launch look like a failure.

**Evidence captured:**
`AccountPreferencesTests.Preferences_are_the_defaults_for_an_account_that_never_saved`.

### E2E-ACP-003 — Idempotent full replace (the conflict case)

```gherkin
Scenario: Sending the same body twice changes nothing
  Given an Approved visitor with no saved preferences
  And the body { textSize: "large", highContrast: true, reduceMotion: false,
                 screenReaderAssist: false, captions: false }
  When the client PUTs that body
  And PUTs the identical body a second time
  Then both responses are 200
  And the two response payloads are equal
  And the account has exactly ONE UserProfile row
  # The second PUT must UPDATE the row the first one seeded, never add a sibling.
```

There is no `409` on this endpoint by design: a preference set has no version
and no concurrent-editor problem — the last write from the user's own device
wins, which is what the app's write-through-on-every-toggle behaviour needs.

**Evidence captured:**
`AccountPreferencesTests.Saving_the_same_preferences_twice_is_idempotent`,
including the `UserProfiles.Count(...) == 1` assertion.

### E2E-ACP-004 — Saving before registration must not fake a completed registration

```gherkin
Scenario: A stub profile stays incomplete
  Given an Approved visitor who has NOT filled the registration form
  When the client PUTs { textSize: "small", captions: true }
  Then the response is 200
  And a UserProfile row now exists for that account
  And its Name is "" and its NameArabic is ""
  And its ProfileTypeId is null
  # so IsProfileCompleteAsync still reports "not registered" — picking a text
  # size must never flip a half-registered account to "registered".
```

Same stub contract as the ID-document upload
(`UserProfileService.UploadIdImageAsync`): the preferences need a row to live on,
and the row that gets seeded is deliberately empty-named.

**Evidence captured:**
`AccountPreferencesTests.Saving_preferences_before_registration_leaves_the_profile_incomplete`.

### E2E-ACP-005 — Validation: an unknown text size is rejected, not coerced

```gherkin
Scenario: The right choice in the wrong case is still wrong
  Given an Approved visitor with no saved preferences
  When the client PUTs { textSize: "ExtraLarge", highContrast: false,
                         reduceMotion: false, screenReaderAssist: false,
                         captions: true }
  Then the response is 400
  And error.code is "VALIDATION_FAILED"
  And error.message and error.messageArabic are both present and DIFFERENT
  And error.details has exactly one entry
  And that entry's field is "textSize"
  And its message names the four accepted values, including "extraLarge"
  And its messageArabic is present
  And NO UserProfile row was created for that account
```

The comparison is `StringComparer.Ordinal` on purpose. The app matches the name
byte for byte, so accepting `"ExtraLarge"` would store a value the client cannot
decode — it would silently read back as the `normal` fallback and the user's
pick would appear to have been ignored. Rejecting is the honest answer; coercing
would hide the client bug that sent it.

The rejection also happens **before** the profile lookup, so a bad payload
cannot seed a stub row as a side effect.

**Evidence captured:**
`AccountPreferencesTests.An_unknown_text_size_is_rejected_with_a_bilingual_error`
— including the `Assert.Empty(app.UserProfiles.Where(...))` that proves nothing
was written.

### E2E-ACP-006 / E2E-ACP-007 — The auth gate on both verbs

```gherkin
Scenario: No token is 401 on both verbs
  When an anonymous client GETs /api/v1/app/account/preferences
  Then the response is 401
  When an anonymous client PUTs a valid body to the same route
  Then the response is 401

Scenario: A verified but unapproved account is 403 on both verbs
  Given a visitor who has verified their email but whose AccountState is Pending
  And who therefore holds a VALID bearer token
  When they GET /api/v1/app/account/preferences
  Then the response is 403 (RequireApprovedAccount)
  When they PUT a valid body
  Then the response is 403
  # 403, not 401: the token is genuine, the ACCOUNT is not yet approved.
```

Both are worth pinning because it would be tempting to treat "settings" as a
harmless surface. It is not: an ungated `PUT` keyed off a route parameter, or a
`GET` that trusted a caller-supplied id, would let one account read and rewrite
another's profile row. The subject comes from `sub` and from nowhere else, and a
`sub` that is not a Guid is answered `401` rather than being defaulted.

**Evidence captured:**
`AccountPreferencesTests.Preferences_without_a_token_return_401` and
`…Preferences_for_a_not_yet_approved_account_are_forbidden` (the latter drives a
real `AuthFlow.SignInVisitorWithoutTwoFactorAsync` token).

### E2E-ACP-008 — The wire shape is a frozen contract

```gherkin
Scenario: The five camelCase keys are present verbatim
  When any successful GET or PUT response is read as raw text
  Then it contains "textSize", "highContrast", "reduceMotion",
       "screenReaderAssist" and "captions"
  And textSize is a STRING enum name, never a number
```

The shipped Flutter app decodes this shape field by field and falls back to the
default on anything it does not recognise (`AccessibilityPreferencesRepository.decode`).
That tolerance is deliberate — it is what let the app ship before the endpoint
did — but it also means a renamed field would **silently** reset every user to
the defaults instead of failing loudly. Hence the raw-body assertion rather than
a typed round-trip: a typed test would pass through a rename.

**Evidence captured:** the `Assert.Contains("\"textSize\"", raw, …)` block in
`AccountPreferencesTests.Preferences_are_the_defaults_for_an_account_that_never_saved`.

### E2E-ACP-009 — A legacy row with no text size still reads as something the app can use

```gherkin
Scenario: A pre-feature row degrades to the default, not to an undecodable value
  Given a UserProfile row written before this feature — or by the walk-in desk
    or the ID-document stub path — whose AccessibilityTextSize is empty
  When the client GETs /api/v1/app/account/preferences
  Then the response is 200
  And data.textSize is "normal"
  And the other four flags are returned as stored
```

`SIMF.Domain` does not reference `SIMF.Contracts`, so the column default
(`UserProfile.DefaultAccessibilityTextSize`) and the wire default
(`AccountPreferences.DefaultTextSize`) are two separate constants that the
compiler cannot keep in step. They are pinned together by a test instead.

**Evidence captured:**
`AccountPreferencesTests.The_stored_default_text_size_matches_the_contract_default`
plus the `IsAllowedTextSize(...) ? stored : stored with { TextSize = Default }`
branch in `AccountPreferencesService.GetMineAsync`.

### E2E-ACP-010 — Cross-device replay (the reason the feature exists)

```gherkin
Scenario: A second device inherits the choices
  Given the user signed in on device A and set extraLarge + high contrast + captions off
  And device B is a fresh install whose local cache says "normal"
  When the user signs in on device B
  Then the app calls GET /api/v1/app/account/preferences exactly once
  And the response is { extraLarge, true, false, false, false }
  And device B renders extraLarge with high contrast and no caption strip
  And device B writes those values to its own prefs
  # so B's NEXT cold start reads them instantly, offline, before any call
```

The app half of this — the single post-auth seam `routeAfterAuth` calling
`AccessibilitySync.hydrate()` — is E2E-MOB038-009 and is automated in
`accessibility_server_sync_test.dart`. What is **not** automated is the two-device
journey against a live API, so this row stays a manual driver until the mobile
surface is automatable (`mobile-manual-only`).

### E2E-ACP-011 — A server failure must not disturb the user's choice

```gherkin
Scenario: A 5xx is a well-formed envelope, and the app absorbs it
  Given the preferences store is unavailable
  When the client PUTs a valid body
  Then the response is 500 carrying the standard ApiResult error envelope
    (success = false, error.code, error.message, error.messageArabic)
  And the app still renders the choice the user just made
  And that choice is still written to device prefs
  And no error is surfaced on the accessibility screen

Scenario: An unreachable server at sign-in cannot break sign-in
  Given GET /api/v1/app/account/preferences is unreachable
  When the user signs in
  Then the sign-in completes and routes normally
  And the device's cached choices are unchanged
```

Both sync directions swallow their failures **by contract** — a settings sync
must never fail a sign-in and must never overwrite the choice the user is
looking at. The client half is automated (`accessibility_server_sync_test.dart`:
`a failed push never disturbs the local choice`, `an unreachable server leaves
the local cache untouched`); the server-side 500 injection is manual.

### E2E-ACP-012 — Bilingual / RTL

```gherkin
Scenario: The Arabic half of a rejection is real Arabic
  When a PUT is rejected for an unknown textSize
  Then error.message reads "One or more fields are invalid."
  And error.messageArabic reads "يوجد حقل أو أكثر غير صالح."
  And details[0].messageArabic names the four values in Arabic
    ("يجب أن يكون حجم الخط أحد القيم التالية: …")
  And messageArabic is NOT equal to message
  And the app renders the details entry against the textSize control, right-to-left
```

**Evidence captured:** the `Assert.NotEqual(body.Error.Message,
body.Error.MessageArabic)` and the `detail.MessageArabic` assertion in
`An_unknown_text_size_is_rejected_with_a_bilingual_error`.

### E2E-ACP-013 — The write path is rate-limited, the read path is not

```gherkin
Scenario: A runaway client cannot hammer the write path
  Given the "auth" rate-limit partition (20 requests / 60 s per IP by default)
  When more than the permitted number of PUTs arrive from one IP inside the window
  Then the excess requests are answered 429
  And a GET from the same IP is unaffected (it carries no policy)
```

The `PUT` carries `RequireRateLimiting("auth")` because the app writes through on
**every toggle** — a stuck switch or a retry loop would otherwise write once per
frame. The `GET` is left unlimited on purpose: it runs once per sign-in, and
throttling it would degrade accessibility on a shared-NAT venue Wi-Fi, which is
exactly the network this event runs on.

## Implementation notes

- **D-157 clean.** The service reads and writes `SimfAppDbContext` only. The
  `userId` is the bare `sub` Guid; no navigation, no FK and no cross-database
  join reaches `SIMF_Identity`.
- **Read is a five-column projection** with `AsNoTracking()`, so a preferences
  read never materialises the profile's PII columns and never enters the change
  tracker.
- **No new permission code.** Step 1–6 of the CP permission playbook do not
  apply: there is no CP page and no admin action here. The gate is
  `RequireApprovedAccount` plus own-`sub`.
- **Additive schema only.** Five columns on an existing table, all with store
  defaults that reproduce the shipped behaviour, so every pre-existing row reads
  back as "never chosen".

---

_Last reviewed:_ `2026-07-31` by `SIMF Team` — **first authoring. The server half
of `accessibility-server-sync` shipped (`GET`/`PUT /app/account/preferences`),
closing the register item whose app half landed on 2026-07-30 with nothing to
call. New namespace ACP, E2E-ACP-001..013.**
