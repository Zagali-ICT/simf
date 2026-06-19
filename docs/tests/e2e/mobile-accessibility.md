# E2E test catalogue — `Accessibility` (`accessibility`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> this screen has **no API** (client-local settings only). **Re-skinned to Figma
> `1116:16630` + two new controls wired to real behaviour (D-465).** Tested in
> `src/Mobile/simf_app/test/features/accessibility/accessibility_screen_test.dart`
> + `accessibility_controller_test.dart`. Choices are **persisted** (prefs) and
> **applied app-wide**: the text scaler + reduce-motion ride the root MediaQuery,
> high-contrast swaps the theme (D-327), the **screen-reader** assist announces
> each titled screen, and the **captions** toggle gates the live-broadcast caption
> strip.

| | |
|--|--|
| **Page** | [`Page_038`](../../App/Page_038/README.md) |
| **Route** | app screen #38 `/settings/accessibility` (no API) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1116:16630` |
| **Auth setup** | **None** — the screen is public (anonymous). |
| **Last reviewed** | 2026-06-20 |

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

---

_Last reviewed:_ `2026-06-20` by `SIMF Team`.
