# FAQ (الأسئلة الشائعة) — mobile `/faq`

| Field | Value |
|---|---|
| Route | `/faq` (`RouteNames.faq`) · public / Guest+ |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/faq/faq_screen.dart` (`FaqScreen`) |
| Figma node | `1388:7567` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`) |
| Shell | `SimfPageShell` (back + centred title + hairline; no header cluster) |
| API | `GET /app/faq` (anonymous; reads the D-211 `FaqGroup`/`FaqEntry` tables) |
| Providers | `faqProvider` (`FutureProvider.autoDispose<List<FaqGroup>>`) → `FaqRepository` |
| Tests | `test/features/faq/faq_screen_test.dart` + `faq_models_test.dart`; golden `test/golden/faq_golden_test.dart` (`goldens/faq_1388-7567.png`); E2E [`mobile-faq.md`](../../../tests/e2e/mobile-faq.md) |
| Status | ✅ Real — D-464 (built from ComingSoon) → D-517 (Figma parity) → clean-code 0g |

## 1. Purpose
The public FAQ: a navy accordion of question/answer cards over the shared shell.
Anonymous (no auth) — reached from المزيد → الأسئلة الشائعة.

## 2. Audience & access
Guest+ (everyone). The endpoint is anonymous; no permission gate.

## 3. UI & behaviour
- `SimfPageShell` — back chevron + centred title الأسئلة الشائعة + bottom hairline.
- Accordion of `SimfCard` rows; tapping a card toggles its answer; the trailing
  gold chevron flips up/down.
- Expanded card reveals a hairline divider then the answer.
- More than one group → a `SimfSectionHeader` per group; a single group renders
  the flat accordion the design shows.
- **Pull-to-refresh** (`SimfPullToRefresh`; empty/error states wrapped in
  `SimfPullableHost` so the gesture fires on short content).
- Typography (clean-code 0g): question = `SimfTokens.labelBeigeMedium` (beige
  Medium 14), answer = `SimfTokens.bodyBeige` (beige 14, line-height 1.5). No
  raw colours / inline `TextStyle`.

## 4. Data / API
- `GET /app/faq` → `[{ id, name, nameArabic, entries: [{ id, question,
  questionArabic, answer, answerArabic }] }]` — active groups + active entries,
  ordered server-side. Wire field names are the frozen contract (D-219).
- Async states via `AsyncValue`: loading (spinner) · data (accordion) · empty
  (`SimfEmptyState`) · error (`SimfErrorState` + retry).

## 5. i18n / RTL
Bilingual; the model's localized getters fall back to the other language when one
side is blank. RTL-correct (start-aligned text, directional insets).

## 6. Testing
- **Widget** (`faq_screen_test.dart`): loading→data→error, pull-to-refresh
  re-fetch (calls 1→2), empty state.
- **Unit** (`faq_models_test.dart`): bilingual fallback getters.
- **Golden** (`faq_golden_test.dart`): `goldens/faq_1388-7567.png` @375×900 RTL,
  first card expanded — locks the frame parity.
- **E2E**: [`docs/tests/e2e/mobile-faq.md`](../../../tests/e2e/mobile-faq.md).

## 7. Clean-code DoD (Phase 0g pilot — 2026-06-29)
- [x] ≤400 lines (158); one public widget per file; shell/cards reused (no copy)
- [x] flexible width (0 fixed content widths); pull-to-refresh; RTL-correct
- [x] 0 raw `Color(0x…)`; 0 inline `TextStyle` (→ named `SimfTokens` styles)
- [x] Figma node `1388:7567` bound; golden locks parity
- [x] widget + unit + golden tests + E2E catalogue + this doc, same changeset
- [x] `flutter analyze` 0 errors; faq tests green

## 8. Changelog
- **2026-06-29 (clean-code 0g):** extracted the 2 inline `TextStyle`s to
  `SimfTokens.labelBeigeMedium` / `bodyBeige`; added the golden
  `faq_1388-7567.png`; added this per-page doc. Behaviour/pixels unchanged.
- **D-517:** exact Figma parity (frame 1388:7567).
- **D-464:** built from the ComingSoon placeholder over the D-211 FAQ tables.
