# My visitors — زواري (`myVisitors`, D-426)

- **Route:** `/exhibitor/visitors` (`RouteNames.myVisitors`). Access:
  **Exhibitor (approved, non-visitor)** — a visitor-tier caller gets 403 → the
  forbidden surface. Reached from the side drawer (Other-only) and after a
  successful visitor-badge scan.
- **API:** `GET /app/exhibitor/my-visitors` (`ExhibitorRepository.listMyVisitors`).
- **Figma:** none — this is a D-426 functional page, not a KSA design frame.
  **Clean-code freeze:** D-642 (2026-07-04).

## Purpose

The exhibitor's captured visitors — everyone they scanned at their booth,
newest first, each rendered as the shared `ContactCard` with the visitor's full
card resolved live (name / job / organisation / country / email / mobiles, or
the "no longer available" state when the visitor has hidden their card).

## Structure

| File | Holds |
|------|-------|
| `exhibitor/my_visitors_screen.dart` (139) | `MyVisitorsScreen` (`ConsumerStatefulWidget`) — the load → loading/forbidden/error/empty/list dispatch inside `SimfPageShell`, the pull-to-refresh list of shared `ContactCard`s, and the small `_Centered` text-only message. |
| `exhibitor/data/exhibitor_models.dart` · `exhibitor_repository.dart` | `ExhibitorVisitor` + the repo (already split). |

## Clean-code freeze (D-642)

The screen was already small (139 lines) with its data layer in `data/` and its
error branch already on the shared `SimfErrorState`. The one deviation was a raw
`RefreshIndicator` — swapped for the app-wide branded **`SimfPullToRefresh`**
(the accent/navy-deep spinner, D-520/D-532); the resting render is unchanged
(the spinner colour only shows during an active pull). `_Centered` is **kept
local** — it is a text-only centred message (no icon), so neither `SimfEmptyState`
(icon) nor `SimfErrorState` (retry button) fits; it serves both the empty and
the forbidden surfaces. Already fully tokenised.

## L4 render-lock (no Figma frame)

Captured `my_visitors.png` (@375×812, ar, two captured visitors — one available
with full details, one unavailable) and **read it** — the زواري header, the two
`ContactCard`s (gold avatar + job/org/country/email/mobile with gold RTL icons;
the second showing هذه الجهة لم تعد متاحة), the bottom nav. RTL, no tofu. No
Figma frame is bound, so this is a structural render-lock, not a parity claim.

## Level-F

- **List** — each captured visitor's `ContactCard`.
- **Pull-to-refresh / Retry** — re-fetch `listMyVisitors`.
- **403** — the forbidden message (only exhibitor accounts may scan).
- **Empty** — the "scan a visitor badge to capture them here" message.
- **Back** — `backOrHome`.

## Tests

`test/golden/my_visitors_golden_test.dart` (render-lock, @375×812, ar) +
`test/features/exhibitor/my_visitors_screen_test.dart` (empty / list / 403).

> **Gap (flagged, not introduced here):** this D-426 screen shipped without an
> E2E catalogue file under `docs/tests/e2e/`. Authoring `mobile-my-visitors.md`
> is a pre-existing DoD gap, tracked for the owner — out of scope for this
> clean-code freeze (a one-line refresh-widget swap + a golden).

## Related decisions

- **D-642** (this clean-code freeze — `SimfPullToRefresh` swap + render-lock
  golden + first PAGE-INDEX row).
- **D-426** (exhibitor scan + my-visitors built).
