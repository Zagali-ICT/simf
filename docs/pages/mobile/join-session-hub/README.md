# Join a Session hub (احجز مقعداً) — mobile `/sessions/join`

| Field | Value |
|---|---|
| Route | `/sessions/join` (`RouteNames.joinSessionHub`, route #110, D-485) · approved Visitor (login-gated) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/sessions/join_session_hub_screen.dart` (`JoinSessionHubScreen`, ~200 lines) |
| Figma node | none of its own — a plain session list into the join flow (the shared `HallSeatMapCard` is available "if required", owner 2026-07-03; the hub renders no grid) |
| Shell | `SimfPageShell` (`SimfTab.sessions`) |
| API | `GET /app/programme/sessions` (reused; **no new endpoint**) |
| Providers | `joinHubSessionsProvider` (`FutureProvider.autoDispose`) → `sessionsRepositoryProvider` |
| Tests | `test/features/sessions/join_session_hub_screen_test.dart`; render-lock golden `test/golden/join_session_hub_golden_test.dart` (`goldens/join_session_hub.png`); E2E [`mobile-join-hub.md`](../../../tests/e2e/mobile-join-hub.md) |
| Status | ✅ Real — D-485 (built) → **clean-code frozen (D-601)** |

## 1. Purpose
The standalone entry into the join flow (the other entry is the session page's
Join CTA — owner's "both" choice): a hint line + one tappable card per
programme session; tapping opens the session detail, where the Join/seat CTA
lives.

## 2. Audience & access
Approved Visitor (login-gated route).

## 3. UI & behaviour
- `AsyncValue.when`: loading spinner · error + retry · empty · list.
- **Pull-to-refresh on every state (ADDED D-601** — the screen was missing it,
  violating the repo-wide D-520/D-532 rule; short states wrapped in the shared
  `SimfPullableHost`).
- Cards: title over "time · hall", with the thin stroked forward chevron
  (`SimfSvgIcon ic_back.svg`) the sibling session cards use — **D-601 fixed a
  double-mirror bug** here: the old direction-aware `Icons.chevron_left`
  carries `matchTextDirection`, so Flutter flipped it back to pointing RIGHT
  under RTL; the SVG glyph never mirrors.

## 4. Button / action audit (Level F, 2026-07-03)
| Control | Handler | Backend |
|---|---|---|
| Back (circled) | `backOrHome` | — |
| Session card | push `/sessions/:id` #17 | — |
| Retry / pull-to-refresh | invalidate + re-await provider | `GET /app/programme/sessions` |

All data repo-backed; no missing API.

## 5. Clean-code freeze (D-601)
Already well-structured (163 lines, `.when` states, `.separated` list, tokens).
Two real fixes: pull-to-refresh added on all three states (owner rule), and the
RTL chevron double-mirror corrected to the shared stroked glyph. Render-lock
golden added (crop-verified left-pointing chevron).
