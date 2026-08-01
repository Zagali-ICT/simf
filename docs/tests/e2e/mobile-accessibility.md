# E2E test catalogue — `Accessibility` (`accessibility`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> **Re-skinned to Figma `1116:16630` + two new controls wired to real behaviour
> (D-465).** Tested in
> `src/Mobile/simf_app/test/features/accessibility/accessibility_screen_test.dart`
> + `accessibility_controller_test.dart` + `accessibility_server_sync_test.dart`.
> Choices are **persisted** (prefs) and **applied app-wide**: the text scaler +
> reduce-motion ride the root MediaQuery, high-contrast swaps the theme (D-327),
> the **screen-reader** assist announces each titled screen, and the **captions**
> toggle gates the live-broadcast caption strip.

> **Server sync (`accessibility-server-sync`, 2026-07-30).** The five flags used
> to be **device prefs only**, so they did not follow the user to a second device
> and did not survive a reinstall. They are now **account** settings: every change
> is written through to `PUT /api/v1/app/account/preferences` and the account copy
> is replayed at sign-in (`GET …/preferences`, via the single post-auth seam
> `routeAfterAuth`). The device prefs stay the **offline cache and the only read
> path**, so the app still renders the right scale on the first frame, offline,
> before any network call — and a sync failure never disturbs the local choice
> nor fails a sign-in.
>
> **The server half shipped 2026-07-31.** When this file was written the endpoint
> did not exist, so the app was writing into the void and every sync scenario
> below was a *target* spec that passed only because both directions swallow
> their failures by contract. `GET`/`PUT /api/v1/app/account/preferences` is now
> live, backed by five additive `UserProfile` columns, and is catalogued on its
> own at [`api-account-preferences.md`](api-account-preferences.md)
> (E2E-ACP-001..013). E2E-MOB038-007..011 are consequently real end-to-end for
> the first time, and E2E-MOB038-012 is the round trip they could not previously
> assert.

| | |
|--|--|
| **Page** | [`Page_038`](../../App/Page_038/README.md) |
| **Route** | app screen #38 `/settings/accessibility` · `GET`/`PUT /api/v1/app/account/preferences` (approved account only) |
| **Surface** | Mobile (Flutter) + App API |
| **Figma** | `1116:16630` |
| **API catalogue** | [`api-account-preferences.md`](api-account-preferences.md) — the server half (E2E-ACP-001..013) |
| **Auth setup** | The screen itself is reachable anonymously (local prefs). The **sync** half needs an **approved** visitor token — the endpoint is `RequireApprovedAccount`, so a merely verified account is answered 403 and the app falls back to local-only. **No literal secrets.** |
| **Last reviewed** | 2026-07-31 |

## Layout (D-465)

- **العرض**: حجم الخط — four chips صغير / متوسط / كبير / **أكبر** (`extraLarge`, ×1.3); تباين عالٍ switch; تقليل الحركة switch.
- **الصوت والقراءة**: قارئ الشاشة switch (default off); الترجمة النصية (للجلسات) switch (default **on**).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB038-001 | Both sections render: 4 font chips + 4 switches | happy | P0 | authored ✓ (screen `renders the display + sound sections and their controls`) |
| E2E-MOB038-002 | Toggling high-contrast flips it and persists | happy | P0 | authored ✓ (screen `toggling high-contrast flips it and persists`) |
| E2E-MOB038-003 | Picking a text size (أكبر) persists `extraLarge` | happy | P1 | authored ✓ (screen `picking a text size persists the choice`) |
| E2E-MOB038-004 | Screen-reader assist defaults off, persists on | happy | P1 | authored ✓ (screen `screen-reader assist defaults off and persists on`) |
| E2E-MOB038-005 | Captions default on, persist off → live strip hidden | happy | P1 | authored ✓ (screen `captions default on and persist off`) + live-broadcast strip gating |
| E2E-MOB038-006 | Choices persisted to prefs + applied app-wide (scale / contrast / motion) | happy | P1 | covered (controller test persists; app applies via root MediaQuery + theme) |
| E2E-MOB038-007 | **Write-through (`accessibility-server-sync`):** every one of the five setters pushes the WHOLE settings object to `PUT /app/account/preferences` | happy | P1 | authored ✓ (`accessibility_server_sync_test` — `each change is pushed to the account`) |
| E2E-MOB038-008 | **A failed push never disturbs the local choice** — offline / signed-out / 5xx leaves both the state and the prefs on the value the user just picked | resilience | P0 | authored ✓ (`accessibility_server_sync_test` — `a failed push never disturbs the local choice`) |
| E2E-MOB038-009 | **Hydrate at sign-in** — the account copy replaces the local one on the device *and* is written to prefs, so the next cold start reads it instantly and offline | happy | P1 | authored ✓ (`accessibility_server_sync_test` — `the account copy replaces the local one, prefs included`) |
| E2E-MOB038-010 | **An unreachable server at sign-in leaves the cache untouched** and never blocks or fails the sign-in | resilience | P0 | authored ✓ (`accessibility_server_sync_test` — `an unreachable server leaves the local cache untouched`) |
| E2E-MOB038-011 | **Wire shape** — `textSize` travels as the stable enum NAME (`small`/`normal`/`large`/`extraLarge`), never an index; an absent / unknown payload falls back to the shipped defaults (captions ON) | edge | P1 | authored ✓ (`accessibility_server_sync_test` — `AccessibilityPreferencesRepository.decode` ×2) |
| E2E-MOB038-012 | **End-to-end round trip against the LIVE endpoint (2026-07-31)** — device A saves أكبر + تباين عالٍ + captions off, device B (fresh install, local cache "متوسط") signs in and renders exactly that. Previously unassertable: the endpoint did not exist, so a green sync test only proved the app tolerated its absence | happy | P0 | _to author_ (manual; server half automated as E2E-ACP-001 / -010) |
| E2E-MOB038-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB038-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

```gherkin
Feature: Accessibility settings (client-local, Figma 1116:16630)

Scenario: The two sections render their controls
  When the user opens /settings/accessibility
  Then the العرض section shows the four size chips (Small/Medium/Large/Extra large)
  And the تباين عالٍ and تقليل الحركة switches are shown
  And the الصوت والقراءة section shows the قارئ الشاشة and الترجمة النصية switches

Scenario: Toggling high-contrast persists
  Given the high-contrast switch is off
  When the user taps it
  Then it flips on and accessibility_high_contrast is written to prefs

Scenario: Picking the largest text size persists extraLarge
  When the user taps "أكبر"
  Then accessibility_text_size = "extraLarge" is written and the app text scaler becomes 1.3

Scenario: The screen-reader assist persists and announces
  Given the screen-reader switch is off
  When the user turns it on
  Then accessibility_screen_reader = true is written
  And subsequently opening a titled screen announces its name via the platform a11y channel

Scenario: Turning captions off hides the live caption strip
  Given captions default on
  When the user turns captions off
  Then accessibility_captions = false is written
  And the live-broadcast AI caption strip is no longer rendered
```

**Evidence:** screen tests (5) + controller test (read-on-boot + each setter persists);
live caption gating in `live_broadcast_screen.dart` (`_CaptionStrip`).

### E2E-MOB038-007..011 — Server sync (`accessibility-server-sync`)

```gherkin
Feature: Accessibility preferences follow the account
  As a user who has set up the app for my eyesight
  I want those choices on my new phone after a reinstall
  So that I do not have to rediscover the settings screen

Scenario: Each change is written through to the account
  Given a signed-in user on the accessibility screen
  When they turn high contrast on
  And pick the "أكبر" text size
  And turn reduce-motion on
  And turn screen-reader assist on
  And turn captions off
  Then each change is PUT to /api/v1/app/account/preferences
  And each push carries the WHOLE settings object, not a single flag
  And textSize travels as the name "extraLarge", never an index

Scenario: A failed push never disturbs the local choice
  Given the device is offline
  When the user turns high contrast on
  Then the app still renders high contrast
  And accessibility_high_contrast = true is written to device prefs
  And no error is surfaced on the settings screen

Scenario: The account copy is replayed at sign-in
  Given the account's stored preferences are extraLarge + contrast + captions off
  And this device's local cache says "small"
  When the user signs in
  Then GET /api/v1/app/account/preferences is called once
  And the app applies extraLarge + contrast + captions off
  And those values are written to device prefs
  # so the NEXT cold start reads them instantly, offline, before any call

Scenario: An unreachable server at sign-in cannot break sign-in
  Given the preferences endpoint is unreachable
  When the user signs in
  Then the sign-in completes and routes normally
  And the device's cached choices are unchanged
```

**Evidence:** `test/features/accessibility/accessibility_server_sync_test.dart`
— two write-through cases, two hydrate cases and two wire-decode cases.

**Contract this depends on — SHIPPED 2026-07-31.** `GET`/`PUT
/api/v1/app/account/preferences`, `ApiResult<AccountPreferences>`,
`Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))`, body
`{ textSize: string, highContrast: bool, reduceMotion: bool,
screenReaderAssist: bool, captions: bool }` — exactly the shape this file
predicted, stored on five additive `UserProfile` columns. Full server-side
coverage: [`api-account-preferences.md`](api-account-preferences.md). The old
"until it exists the app degrades to local prefs only" caveat no longer applies
to a deployed build; it still describes what happens against an **older** API
(and against a non-approved account, which the endpoint answers 403), because
both sync paths swallow their failures by contract.

### E2E-MOB038-012 — Round trip against the live endpoint

```gherkin
Feature: The choices are on the account, not on the handset

Scenario: A second handset inherits the first one's accessibility setup
  Given "khalid@simf.test" is an APPROVED visitor
  And on device A they set حجم الخط = أكبر, تباين عالٍ = on, الترجمة النصية = off
  And each of those three changes was PUT to /api/v1/app/account/preferences
  When they sign in on device B, a fresh install whose local cache says متوسط
  Then GET /api/v1/app/account/preferences is called exactly once
  And it returns { extraLarge, true, false, false, false }
  And device B renders text at ×1.3 with the high-contrast theme
  And the live-broadcast caption strip is not rendered on device B
  And those five values are written to device B's own prefs
  # so B's next cold start applies them offline, before any call

Scenario: The same journey on a PENDING account stays local-only
  Given the account is verified but NOT yet approved
  When they sign in on device B
  Then the preferences GET is answered 403 (RequireApprovedAccount)
  And sign-in completes normally
  And device B keeps its own cached choices, showing no error
```

**Evidence:** server side automated by
`AccountPreferencesTests.Saved_preferences_round_trip_through_a_second_read` and
`…Preferences_for_a_not_yet_approved_account_are_forbidden`; the two-handset half
is a manual driver (`mobile-manual-only` — the Flutter UI is not agent-drivable).

---

_Last reviewed:_ `2026-07-31` by `SIMF Team` — **the server half of
`accessibility-server-sync` shipped.** `GET`/`PUT /app/account/preferences` now
exists (five additive `UserProfile` columns, `RequireApprovedAccount`, no admin
permission), so E2E-MOB038-007..011 stop being a target spec and become real
end-to-end coverage. Added E2E-MOB038-012 (live round trip) and the API
catalogue cross-link; corrected the "until it exists" caveat and the auth-setup
line (the endpoint needs an **approved** account, not merely a signed-in one).
_Prior:_ `2026-07-30` by `SIMF Team` — `accessibility-server-sync`: the
five flags are account settings now (write-through + hydrate at sign-in, prefs
as the offline cache); added E2E-MOB038-007..011.
_Prior:_ `2026-06-20` by `SIMF Team`.
