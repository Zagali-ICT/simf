# More — المزيد (Page 041, `#41`)

- **Route:** `/more` (`RouteNames.more`). Access: **Guest+ (public)**. No API of its own (the profile card best-effort reads the dashboard).
- **Figma:** **1129:17224** (grouped re-skin, D-465). **Clean-code freeze:** D-635 (2026-07-04).

## Purpose

The app's "more" hub: a منطقتي profile header card (signed-in → My Area), three
grouped sections of bordered nav rows — **معلومات الملتقى** (about / forum-guide /
FAQ / VisitSaudi), **الإعدادات** (language toggle / accessibility / notifications),
**قانوني** (terms / contact-us / rate) — the تسجيل الخروج link (signed-in) and the
version line. Rows are **role-filtered** (D-519) so a focused Staff/Moderator never
sees a dead attendee-only link.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `more_screen.dart` (181) | `MoreScreen` (`ConsumerWidget`) — the `_moreProfileProvider` (a tiny best-effort dashboard wrapper, kept private in the screen), and the `build` that lays out the profile card + the three sections (with role-filtered rows) + sign-out + version. |
| `widgets/more_profile_card.dart` (`MoreProfileCard`) | The منطقتي header card — avatar, "منطقتي" title + `{name} · {tier}` sub-line, gold caret. |
| `widgets/more_list.dart` (`MoreSection` + `MoreRow`) | A titled group of nav rows + one nav row (title / optional trailing value / gold caret). |

`_moreProfileProvider` is a 7-line private best-effort wrapper (kept in the screen
— moving a trivial private provider to its own file would be over-engineering; the
test drives it via the `myAreaRepositoryProvider` override). Screen was already
fully tokenised (no raw `Color(0x..)`). Every file ≤400 lines.

## L4 Figma parity (frame 1129:17224)

Captured `more_1129-17224.png` (@375×1000, ar, signed-in VIP) as the **baseline
before** the refactor, then **held it WITHOUT `--update`** after — proving the
3-widget extraction byte-identical. Golden read: المزيد header, the منطقتي card
(رائد السالم · VIP), the three sections (all rows; اللغة shows العربية), تسجيل
الخروج, RTL, no tofu.

## Level-F

Wired: profile card → My Area; each row navigates (about / forum-guide / faq /
accessibility / notifications / terms / contact-us / rate) or acts (language
toggle, VisitSaudi confirm-launch); sign-out (confirm); rate/attendee rows
role-filtered. No API of its own.

## Tests

`test/golden/more_golden_test.dart` (frame 1129:17224, @375×1000, ar, signed-in) +
`test/features/more/more_screen_test.dart` (guest hides profile/sign-out, signed-in
shows them, About navigation). E2E: `docs/tests/e2e/mobile-more.md`.

## Related decisions

- **D-635** (this clean-code freeze — profile card + list widgets + first golden).
- **D-315** (screen built), **D-465** (1129:17224 grouped re-skin), **D-519** (role-filtered rows), **D-609** (my-sessions row removed).
