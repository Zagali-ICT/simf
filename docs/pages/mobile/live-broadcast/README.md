# Live broadcast (البث المباشر) — mobile `/live?sessionId=`

| Field | Value |
|---|---|
| Route | `/live?sessionId=` (`RouteNames.liveBroadcast`, page #25) · **login-only** (in-screen gate, D-577) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/live/live_broadcast_screen.dart` (`LiveBroadcastScreen`, 348 lines — state + `_content` composition) |
| Widgets | `lib/features/live/widgets/` — `live_player_surface` (badge row + player + caption strip) · `live_video_player` (the YouTube/`video_player` controller engine) · `live_badges` (LiveBadge + LanguageChip) · `live_message_surfaces` (recording / not-live black bands) · `live_content` (need-login, feed toggle, gold bullets, ask-question, sign-language note, upcoming cards, **`LiveNoticeBanner`** — the FR-702 informational notice) |
| Figma node | `934:3450` |
| Shell | `SimfPageShell` (`SimfTab.sessions`) |
| API | `GET /app/programme/sessions/{id}` (broadcast slice, `AllowAnonymous`) + the agenda list for the upcoming strip |
| Providers | `liveRepositoryProvider` · `orgProfileProvider` (global main-live URL) · `authControllerProvider` · `accessibilityControllerProvider` (captions toggle) · `localeControllerProvider` (language chip) |
| Tests | `test/features/live/live_broadcast_screen_test.dart` (**31 cases, counted on this branch** — incl. the A15 + A20 regression cases and the 3 new FR-702 notice cases; the "36" this row previously claimed was stale, not a deletion) + `live_repository_test.dart` (notice decode + fallback) + `youtube_url_test.dart`; golden `test/golden/live_broadcast_golden_test.dart` (`goldens/live_broadcast_934-3450.png`); E2E [`mobile-live.md`](../../../tests/e2e/mobile-live.md) (E2E-MOB025-026..028) |
| Legacy detail | `docs/App/Page_025/` — retained as the historical spec |
| Status | ✅ Real — D-199 → D-349 (YouTube POC) → D-433/439/495/577 → **clean-code frozen (D-603)** → D-815 (FR-702 live notice, informational) |

## 1. Purpose
The live video feed for a session (or the forum's global main-live): the black
player band (LIVE badge, language chip, organiser caption strip), the يُبث الآن
now-broadcasting block, the ask-a-question entry, and the upcoming-sessions
strip.

## 2. Audience & access
**Login-only** — a signed-out guest sees an in-screen need-login prompt (not a
router redirect); the reads themselves stay `AllowAnonymous`.

## 3. UI & behaviour
- Branches (Page_025 L-3): live feed (player) · recording-available note ·
  not-live note · 404 · error+retry · need-login · no-session picker (or the
  global main-live when the org profile carries a URL, D-495).
- Player (D-349): YouTube link → IFrame player; else HLS/MP4 → `video_player`.
  Both-feeds session → a main/sign-language toggle keyed by the active URL.
  The caption strip shows the admin-typed `Session.LiveCaptions` note or a
  placeholder; hidden when the user turns captions off (accessibility toggle).
- **A15 (2026-07-26) — no false AI claim on the caption strip.** The strip used
  to carry a gold "AI" chip over copy promising live translation of the spoken
  word, but it renders a STATIC admin-typed string that never changes during the
  broadcast. Both are removed; the placeholder now names the organiser as the
  author. Real speech-to-text + streaming translation is a feature decision for
  the owner, not something this screen fakes (the `/app/ai/live-translation/chunk`
  endpoint exists but has no caller anywhere in the repo).
- **A20 (2026-07-26) — no geographic-restriction notice.** The gold "the
  broadcast is available only inside the Riyadh region per the organising
  regulations" card (frame 934:3619) was shown to every viewer while nothing in
  the app, API, CP or Website ever read the viewer's location. The claim is
  removed.
- **FR-702 live notice (owner decision 2026-07-31 — D-815).** The product
  decision A20 left open is taken, and it is **no restriction**: owner, verbatim,
  *"No restriction, this is only notification and be added to session."* So there
  is no geo-fence, no location read and no gate — instead the session carries
  optional bilingual free text (`liveNotice` / `liveNoticeArabic`, ≤512 each)
  written per session at `/admin/sessions`, and `LiveNoticeBanner`
  (`widgets/live_content.dart`) renders it as a calm informational banner
  **above** the player: `SimfPageNote` on a plain `SimfCard`, deliberately not an
  alert register. `LiveSession.localizedNotice(isArabic)` picks the active
  locale, falls back to the other side when one is blank, and returns null when
  both are — in which case nothing is rendered at all (no empty card, no reserved
  space). The banner never gates, delays or replaces the feed; the widget tests
  assert `LivePlayerSurface` is mounted alongside it precisely so a regression
  back to "notice instead of stream" fails the build. SIMF-FDS-007 §5.1 carries
  the superseded restriction wording.
- Language chip toggles the app locale; the ask-question button opens #26.
- **Watch keep-alive (item 13 / D-726):** while a signed-in user is on this
  screen, a 60s timer pings the shared `SessionActivity` clock so the app-wide
  `SessionGuard` treats watching (no touch) as activity and silently refreshes —
  no idle sign-out mid-stream. Bounded by the server 24h cap (D-443); cancelled
  on leave (dispose).

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back | `backOrHome` | — |
| Language chip | `localeController.toggle()` | — |
| Feed toggle (both-feeds) | swap active URL (setState) | — |
| Player play/pause (HLS) / retry | controller / re-bind | — |
| اطرح سؤالاً (session only) | push `sendQuestion` #26 | — |
| Upcoming card | (display; frame has no tap) | — |
| Sign-in (guest) / retry | route / re-fetch | `GET …/sessions/{id}` |

All dynamic content repo-backed; the LIVE-banner copy is the recorded static
config (D10/L-6); no missing API. The region-restriction notice was removed by
A20 (it claimed a restriction nothing enforced) and replaced by the FR-702 live
notice — display-only text with no control on it, so the table above is
unchanged: the banner wires no handler and reaches no backend.

## 5. Clean-code freeze (D-603)
**1,286 → 348-line screen** + 5 widget files (all <400). The media engine
(player surface, controller, badges) and the info column separated cleanly.
**Bug caught by the golden + fixed:** the ask-a-question button set its Arabic
label size/weight via `FilledButton.styleFrom(textStyle:)`, which drops the
brand `fontFamily` and renders the Arabic label as tofu / system-fallback on
device (the recurring D-546/D-549 button-font bug) — moved the style onto the
label `Text` so it inherits the theme font. Golden captured at frame 934:3450;
the player box is env-limited (no headless video platform → error surface) so
the parity claim is the chrome + info column, which overlay-match the frame.
Behaviour byte-identical (34 tests green).
