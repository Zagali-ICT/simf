# Guest mode — وضع الضيف (Page 012, `#12`)

- **Route:** `/guest` (`RouteNames.guestMode`). Access: **Guest (public)**.
- **API:** none — renders entirely client-side (offline-safe).
- **Figma:** styled to the Page 012 mockup (الرئيسية · ضيف); no pinned KSA node.
  **Clean-code freeze:** D-644 (2026-07-04).

## Purpose

An informational entry screen: an accent explore mark, a headline, an accent
"you are browsing as a guest" callout explaining what a guest can browse
(sessions, speakers, the venue map, media) and what needs an account (the smart
badge, notifications, booking), then a primary **Continue as guest** action
(→ home) and a secondary **Sign in** action (→ sign-in).

## Structure

| File | Holds |
|------|-------|
| `guest/guest_mode_screen.dart` (124) | `GuestModeScreen` (`StatelessWidget`) — the AppBar shell (shared `SimfBackButton`) over the explore ring + headline + guest callout + the two actions. |

## Clean-code freeze (D-644)

The screen was **already clean** — a 124-line static `StatelessWidget`, fully
tokenised (no raw `Color(0x..)`; accent alphas via `.withValues`), using the
shared `SimfBackButton`, single-responsibility. So this freeze is the
**render-lock golden only** (no code change), completing the per-page DoD.

## L4 render-lock (no pinned node)

Captured `guest_mode.png` (@375×812, ar) and **read it** — the وضع الضيف AppBar,
the gold explore ring, the التصفح كضيف headline, the accent callout (browse body
+ sign-in body), the المتابعة كضيف (gold) continue action and the تسجيل الدخول
outlined action. RTL, no tofu. No pinned KSA node, so this is a structural
render-lock, not a parity claim.

## Level-F

- **Continue as guest** — `context.go('/')`.
- **Sign in** — `context.pushNamed(signIn)`.
- **Back** — shared `SimfBackButton`.

No API.

## Tests

`test/golden/guest_mode_golden_test.dart` (render-lock, @375×812, ar) +
`test/features/guest/guest_mode_screen_test.dart`. E2E:
`docs/tests/e2e/mobile-guest-mode.md`.

## Related decisions

- **D-644** (this clean-code freeze — render-lock golden, no code change).
- **D-316** (guest-mode entry screen built).
