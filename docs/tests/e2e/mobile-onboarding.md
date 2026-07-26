# E2E test catalogue — `Onboarding` (`onboarding`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #2, the
> first-run intro. Spec: [`Page_002`](../../App/Page_002/README.md). **No SIMF
> API** — client-side only (Page_002_API.md). Runner-agnostic Gherkin. The flag +
> route-out are widget-tested in
> `src/Mobile/simf_app/test/features/onboarding/onboarding_screen_test.dart`; the
> first-run gate is covered by the splash catalogue (E2E-MOB001-001).

| | |
|--|--|
| **Page** | [`Page_002`](../../App/Page_002/README.md) (App page docs) |
| **Route** | app screen #2 `/onboarding` · **no API** |
| **Surface** | Mobile (Flutter) — first-run intro carousel (three static panels — KSA design, D-362) |
| **Test runner** | Flutter widget test (flag + navigation) · device/emulator drive for the visual + RTL |
| **Auth setup** | None — runs at **Guest**, before sign-in. State driven by the local `onboardingCompleted` flag. **No token, no SIMF call.** |
| **Last reviewed** | 2026-06-11 |

> **KSA-Project redesign (D-362, Figma 148:22 / 159:942 / 159:1052):** the
> intro **videos are dropped** (owner decision) for the design's three static
> panels — world-map photo + 90% navy overlay behind step 1, plain navy behind
> steps 2–3, `SimfLogo`, one shared welcome title, per-step body, pill dots,
> the gold **التالي** on every step (no "ابدأ" variant), **تخطي** hidden on
> the last step, a back chevron on steps 2–3. The flag + routing contract is
> unchanged; the old placeholder screen is parked in
> `lib/features/_legacy_mockup/`. Widget tests rewritten:
> `onboarding_screen_test.dart` (skip / third-Next completes / skip hidden on
> last step / back chevron).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB002-001 | First run (flag unset) → onboarding is shown | happy | P0 | authored ✓ (splash gate, E2E-MOB001-001) |
| E2E-MOB002-002 | Next advances through the 3 slides; last shows "Get started" | happy | P0 | authored ✓ (widget test) |
| E2E-MOB002-003 | Finishing the last slide sets the flag + routes to sign-in | happy | P0 | authored ✓ (widget test) |
| E2E-MOB002-004 | Skip (any slide) sets the flag + routes to sign-in | happy | P0 | authored ✓ (widget test) |
| E2E-MOB002-005 | A returning user never sees onboarding (splash skips it) | edge | P0 | authored ✓ (splash test: onboarded → sign-in) |
| E2E-MOB002-006 | The screen makes no SIMF API call | resilience | P1 | authored (no client) |
| E2E-MOB002-007 | RTL render (Arabic) — progress + controls mirror | i18n | P1 | authored (screen) |
| E2E-MOB002-008 | App killed mid-sequence → replays next launch (flag only set on completion) | edge | P2 | authored (flag semantics) |
| E2E-MOB002-009 | Background media — the world-map poster backs EVERY step, so a step is never blank navy | happy | P0 | authored ✓ (widget test) |
| E2E-MOB002-010 | Background media — a PLAYING video sits under the 60% scrim (visible motion); the still poster keeps the design 90% | happy | P1 | authored ✓ (widget test) |
| E2E-MOB002-011 | Background media — a device that refuses the clip degrades to the poster and logs the reason in debug (no visitor-facing error) | resilience | P1 | authored ✓ (widget test) |

## Scenarios

### E2E-MOB002-001 — First run shows onboarding

```gherkin
Feature: First-run onboarding
  As a first-time user
  I want a short intro before signing in
  So that I understand what the app offers

Scenario: A first launch lands on onboarding
  Given the onboardingCompleted flag is not set
  And no session is stored
  When the app cold-starts
  Then the splash routes to Onboarding (#2 /onboarding)
```

**Evidence:** `splash_controller_test` — "a signed-out first run routes to onboarding".

### E2E-MOB002-002 — Next advances the slides

```gherkin
Scenario: Stepping through the three slides
  Given the onboarding screen is shown on slide 1
  When the user taps Next
  Then slide 2 is shown and the second progress segment is active
  When the user taps Next again
  Then slide 3 is shown
  And the primary button now reads "Get started" (ابدأ)
```

**Evidence:** `onboarding_screen_test` — "finishing the last slide completes onboarding".

### E2E-MOB002-003 — Finish completes onboarding

```gherkin
Scenario: Get started finishes the sequence
  Given the onboarding screen is on the last slide
  When the user taps "Get started"
  Then onboardingCompleted is set to true in local storage
  And the app replace-navigates to Sign-in (#3 /sign-in)
  And Back does not return to onboarding
```

**Evidence:** `onboarding_screen_test` — asserts the flag is set + the sign-in screen renders.

### E2E-MOB002-004 — Skip completes onboarding

```gherkin
Scenario: Skip from any slide
  Given the onboarding screen is shown
  When the user taps Skip
  Then onboardingCompleted is set to true
  And the app routes to Sign-in (#3)
```

**Evidence:** `onboarding_screen_test` — "Skip sets the first-run flag and routes to sign-in".

### E2E-MOB002-005 — Returning user skips onboarding

```gherkin
Scenario: A returning user never sees onboarding
  Given onboardingCompleted is true
  And no session is stored
  When the app cold-starts
  Then the splash routes straight to Sign-in (#3), not onboarding
```

**Evidence:** `splash_controller_test` — "a signed-out returning user routes to sign-in".

### E2E-MOB002-006 — No SIMF API call

```gherkin
Scenario: Onboarding is fully client-side
  When onboarding is shown and the user finishes or skips
  Then no request is made to any /api/v1/app/* endpoint
  And no Authorization header is sent
```

> The interim build issues no network call at all; the eventual video player's
> only network dependency is the external YouTube player (Page_002_Logic L-4),
> never a SIMF endpoint.

### E2E-MOB002-007 — RTL render

```gherkin
Scenario: Arabic onboarding mirrors
  Given the device locale is Arabic
  When the onboarding screen renders
  Then the slide title/body read in Arabic and centered
  And the progress segments + Skip/Next controls mirror right-to-left
```

### E2E-MOB002-008 — Killed mid-sequence replays

```gherkin
Scenario: The flag is only set on completion
  Given a first launch reaches onboarding slide 2
  And the user force-kills the app without finishing or skipping
  When the app is relaunched
  Then onboarding is shown again (onboardingCompleted was never set)
```

> Acceptable per Page_002_Logic L-5 — the flag is set only on finish/skip.

### E2E-MOB002-009 — The background is never blank

```gherkin
Scenario Outline: Every step paints the world-map poster
  Given the onboarding background renders step <step> with no playable video
  Then the assets/images/onboarding_world_map.jpg poster fills the frame (BoxFit.cover)
  And the step is NOT plain navy

  Examples:
    | step |
    | 1    |
    | 2    |
    | 3    |
# Owner 2026-07-26 — the poster used to be gated on step 1, so a failed decode
# on steps 2/3 left the copy floating on empty navy ("the video does not exist").
```

### E2E-MOB002-010 — The video is actually visible

```gherkin
Scenario: The scrim over a playing video is lighter than over the poster
  Given a step whose background video is initialised and playing
  Then the navy scrim over it is SimfTokens.navyFill60 (60%)
  And the white title + beige body stay legible over the moving footage
  When no video is playing
  Then the scrim is SimfTokens.navyFill90 (the Figma 148:22 photo overlay)
```

### E2E-MOB002-011 — A refused codec degrades gracefully

```gherkin
Scenario: A device that cannot decode the clip still shows a background
  Given the platform video decoder rejects assets/videos/onboard_02.mp4
  When the user pages to step 2
  Then the world-map poster + the 90% scrim are shown
  And NO error is surfaced to the visitor
  And a debug build prints the asset path and the decoder error
# The Huawei/HiSilicon AVC decoder case is handled by the vendored
# third_party/video_player_android decoder-fallback patch (D-768).
```

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` — added E2E-MOB002-009..011 for the
owner's "background video not working" fix (poster on every step, 60% video scrim,
debug-visible decode failure).
