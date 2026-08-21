# Rate — تقييم الملتقى (Page 040, `#40`)

- **Route:** `/rate` (`RouteNames.rate`, optional `code` / `ratingTypeId` / `targetId`). Access: **Visitor (login-only)**.
- **Figma:** **1116:16894** (D-463 re-skin). **Clean-code freeze:** D-628 (2026-07-04).

## Purpose

A dynamic, **config-driven** rating screen. It fetches the form for a rating type
(resolved by `code` — e.g. "App" / "Session" — or `ratingTypeId`) plus an optional
`targetId` (a session id for a per-session type), then renders: the optional
overall star row, the server-defined grouped + flat questions (each a 1–5 star
bar), and the optional comment box — prefilled from any existing submission.
`GET /app/feedback/form` → `POST /app/feedback/submit`.

## Attendance gate (owner 2026-07-19)

A rating may only be submitted for something the user **attended**. The server hard-gates
`POST /app/feedback/submit` with **403 `RATING_NOT_ATTENDED`**, and `GET /app/feedback/form`
returns an **`isEligible`** flag (append-only, defaults `true`) so the screen keeps the form
visible but disables submit and shows an "attend to rate" note rather than letting the user
fill it and be rejected. Attendance proof is **blended** per scope: **Session** = an in-hall
`HallAttendance` for that session; **Day** = an in-hall check-in that event-local day **or** a
venue-gate Check-In scan that day; **App / Event / Exhibition** (global) = any in-hall check-in
**or** any venue-gate Check-In scan (so the same audience the rating prompts target can rate).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `rate_screen.dart` (366) | `RateScreen` + `_RateScreenState` — the load / submit logic (overall-stars + required-question client validation), and `_buildForm` (the config-driven form assembly: kicker + lead + overall `StarRow`, the grouped / flat question sections, the comment box, the submit button). |
| `widgets/star_row.dart` (`StarRow`) | The tappable 1–5 star bar (ambient-direction fill), shared by the overall block + each category row. |
| `widgets/rate_category_row.dart` (`RateCategoryRow`) | One per-element row — the beige-hairline box, name at inline-start, an 18px `StarRow` at inline-end. |
| `widgets/rate_gold_button.dart` (`RateGoldButton`) | The full-width gold submit button (stays gold while loading, white spinner). |
| `widgets/rate_load_error.dart` (`RateLoadError`) | The form-load failure + retry (kept custom — an `OutlinedButton`, distinct from `SimfErrorState`'s FilledButton). |
| `widgets/rate_navy_note_chip.dart` (`RateNavyNoteChip`) | The navy chip + accent glyph + beige message above the form — the D-713 "watched at" line and the "attend to rate" note when `isEligible` is false. |

The data layer already lived in `data/` (feedback_repository + rating_models).
The screen was already fully tokenised (no raw `Color(0x..)`). Every file ≤400
lines; `_buildForm` stays in the State (it drives `_overall`/`_answers`/`_comment`
+ `setState`) — the leaf widgets are the clean seams.

## DRY (this freeze)

The local `_SectionTitle` (bare white/textLg/w500 `Text`) → the shared
**`SimfSectionHeader`** (title-only) — used for group names, "قيّم العناصر", and
the comment label. Same swap proven pixel-identical in gallery/archive/sponsors;
the golden held here too.

## L4 Figma parity (frame 1116:16894)

Captured `rate_1116-16894.png` (@375×1100, ar, a form with 3 ungrouped questions +
comment) as the **baseline before** the refactor, then **held it WITHOUT
`--update`** after — proving the 5-widget extraction + the `SimfSectionHeader` swap
byte-identical. Golden read: تقييم الملتقى header, kicker شارك تجربتك + lead + the
30px overall stars, قيّم العناصر over the 3 category rows (name right / 18px stars
left), the ملاحظاتك comment box, the gold إرسال التقييم button, RTL, no tofu.

## Level-F

Wired: overall stars + per-element stars set state; submit runs the client guards
(overall-stars-required, every required question scored) → `POST
/app/feedback/submit` → thanks / error toast; retry on load failure; the form is
prefilled from any existing submission. Reads `GET /app/feedback/form`.

### The submit button can no longer strand (fixed 2026-08-20)

`_submitting` was cleared on the success path and again inside `on ApiFailure`,
with no `finally`. Anything thrown that is **not** an `ApiFailure` therefore left
إرسال التقييم disabled for good — no toast, no retry, no way out but leaving the
screen. That escape is real, not theoretical: `SimfApiClient` converts only the
**first** call's errors to `ApiFailure`, and the 401 token-refresh branch sits
outside that guard, so a keystore/keychain `PlatformException` (an OS keystore
reset, a restored backup) surfaces raw mid-submit. The flag now clears in a
`finally`; the thanks / error toasts still fire only on their own paths.

## Tests

`test/golden/rate_golden_test.dart` (frame 1116:16894, @375×1100, ar) +
`test/features/feedback/rate_screen_test.dart` (8 — no-stars prompt, overall
submit, ineligible attend-note + disabled submit, per-question score, per-session
watched-at header, global rating without it, submit failure, session
required-question block). E2E: `docs/tests/e2e/mobile-rate.md`.

## Related decisions

- **D-628** (this clean-code freeze — 5-widget extraction + `SimfSectionHeader` + first golden).
- **D-310** (screen built), **D-463** (per-element scores + the 1116:16894 re-skin), **D-496** (session deep-link).
