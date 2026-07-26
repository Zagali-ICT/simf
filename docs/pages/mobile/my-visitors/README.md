# My Booth Visitors — زوار جناحي (`myVisitors`, D-426)

- **Route:** `/exhibitor/visitors` (`RouteNames.myVisitors`). Access:
  **Exhibitor (approved, non-visitor)** — a visitor-tier caller gets 403 → the
  forbidden surface. Reached from the side drawer (Other-only), the exhibitor
  home's "Exhibitor tools" tile row, and after a successful visitor-badge scan.
- **API:** `GET /app/exhibitor/my-visitors` (`ExhibitorRepository.listMyVisitors`).
- **Figma:** none — this is a D-426 functional page, not a KSA design frame.
  **Clean-code freeze:** D-642 (2026-07-04).

## Purpose

The exhibitor's captured visitors — everyone they scanned at their booth,
newest first, each rendered as the shared `ContactCard` with the visitor's full
card resolved live (name / job / organisation / country / email / mobiles, or
the "no longer available" state when the visitor has hidden their card).

## Not "My Contacts" (BUG-025, 2026-07-26)

This list and **My Contacts** (`/contacts`, `myContacts`) are two different
features and are deliberately **not** merged:

| | My Booth Visitors (this page) | My Contacts |
|--|--|--|
| Filled by | the exhibitor scanning a visitor's **entry badge** at their booth | visitor-to-visitor **card sharing** (a share token) |
| Backend | `ExhibitorVisitorScan` → `GET /app/exhibitor/visitors` | `SavedContact` → `GET /app/contacts` |
| Who can use it | Exhibitor tier only (403 otherwise) | any approved visitor |
| Side effect | a new capture emails the lead to the exhibitor (BUG-024) | none |

Merging them needs an **owner ruling** (see `docs/decisions/DECISIONS_LOG.md`
D-771). Until then the distinction is made unmistakable in the UI: the title
names the booth (زوار جناحي / My Booth Visitors) and the first row of the list
is a shared `SimfPageNote` reading "بطاقات الزوار التي مسحتها في جناحك. قائمة
منفصلة عن «جهات اتصالي»." / "Badges you scanned at your booth. This list is
separate from My Contacts."

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
with full details, one unavailable) and **read it** — the زوار جناحي header (and,
since BUG-025, the explanatory note row beneath it), the two
`ContactCard`s (gold avatar + job/org/country/email/mobile with gold RTL icons;
the second showing هذه الجهة لم تعد متاحة), the bottom nav. RTL, no tofu. No
Figma frame is bound, so this is a structural render-lock, not a parity claim.

## Level-F

- **Note** — the BUG-025 `SimfPageNote` (first list row): what this list is, and
  that it is not My Contacts.
- **List** — each captured visitor's `ContactCard`.
- **Pull-to-refresh / Retry** — re-fetch `listMyVisitors`.
- **403** — the forbidden message (only exhibitor accounts may scan).
- **Empty** — the "scan a visitor badge at your booth to capture them here"
  message.
- **Back** — `backOrHome`.

## Tests

`test/golden/my_visitors_golden_test.dart` (render-lock, @375×812, ar; re-locked
for the BUG-025 title + note) +
`test/features/exhibitor/my_visitors_screen_test.dart` (empty / list / 403 /
booth title + note).
E2E: [`docs/tests/e2e/mobile-my-visitors.md`](../../../tests/e2e/mobile-my-visitors.md)
(E2E-MOBMYVIS-001..008; 001..006 authored D-648 — closed the earlier
pre-existing gap; 008 added for BUG-025).

## Related decisions

- **D-771** (BUG-024 lead email + BUG-025 keep the two lists separate, title +
  note).
- **D-642** (this clean-code freeze — `SimfPullToRefresh` swap + render-lock
  golden + first PAGE-INDEX row).
- **D-426** (exhibitor scan + my-visitors built).
