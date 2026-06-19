# Page 038 — إمكانية الوصول · Accessibility

Per-page documentation folder (App screen 38).

## Identity
| | |
|---|---|
| Mockup page | **38** (`Mockup.html`) |
| Route | `RouteNames.accessibility` → `/settings/accessibility` (**public, anonymous**) |
| Titles | AR **إمكانية الوصول** · EN **Accessibility** |
| Section | 5 — Settings |
| Nature | **Client-local accessibility settings** — font size, high contrast, reduce motion, screen-reader assist, captions |
| App privilege | **Public (anonymous).** No API; nothing is read or written server-side. |
| Status | **No API** (client-local only); **Flutter screen BUILT** — choices **persisted to prefs + applied app-wide**, D-327; Figma `1116:16630` re-skin + screen-reader/captions wired (D-465) |

## API (authoritative contract)
**None.** This screen has no backend contract — it is a client-local settings
panel. The selections live only in widget state.

## Behaviour
On the navy `KsaPage` shell (Figma `1116:16630`), two grouped sections:
- **العرض** — حجم الخط (four chips: صغير / متوسط / كبير / **أكبر**), a high-contrast
  switch and a reduce-motion switch.
- **الصوت والقراءة** — a **screen-reader** assist switch (default off) and a
  **captions** switch (default on).

**Persisted + applied (D-327 + D-465).** Each control reads/writes the
prefs-backed `AccessibilityController`, so the choice survives a restart, and the
settings are applied **app-wide**: `app/app.dart` injects
`MediaQuery.textScaler` (0.85 / 1.0 / 1.15 / **1.3**) + `disableAnimations`, and
high-contrast swaps the theme; the **screen-reader** assist makes `KsaPage`
announce each titled screen via `SemanticsService.sendAnnouncement` (best-effort,
guarded); the **captions** toggle gates the live-broadcast caption strip. No API,
nothing server-side.

## Tests
- Widget: `src/Mobile/simf_app/test/features/accessibility/accessibility_screen_test.dart`
  (renders both sections + 4 chips + 4 switches; high-contrast flips+persists;
  picking أكبر persists extraLarge; screen-reader off→on persists; captions
  on→off persists).
- Unit: `src/Mobile/simf_app/test/features/accessibility/accessibility_controller_test.dart`
  (defaults; reads persisted values; invalid-index fallback; each setter persists;
  scaleFactor map).
- Integration: `src/Mobile/simf_app/integration_test/app_flows_test.dart`
  (changing text size + high-contrast rebuilds the assembled app cleanly).
- API: none (no backend contract).
- E2E: [`docs/tests/e2e/mobile-accessibility.md`](../../tests/e2e/mobile-accessibility.md).
