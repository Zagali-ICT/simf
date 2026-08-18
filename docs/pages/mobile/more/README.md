# More — المزيد (Page 041, `#41`)

- **Route:** `/more` (`RouteNames.more`). Access: **Guest+ (public)**. No API of its own (the profile card best-effort reads the dashboard).
- **Figma:** **1129:17224** (grouped re-skin, D-465). **Clean-code freeze:** D-635 (2026-07-04).

## Purpose

The app's "more" hub: a منطقتي profile header card (signed-in → My Area), three
grouped sections of bordered nav rows — **معلومات الملتقى** (about / forum-guide /
FAQ / VisitSaudi), **الإعدادات** (language toggle / accessibility / notifications),
**قانوني** (terms / contact-us / rate) — the تسجيل الخروج link (signed-in) and the
version line. The footer version line reads the REAL installed version
(`installedAppVersionProvider`, from `package_info_plus` — D-736) rendered as
`SIMF 2026 · الإصدار {v}` / `SIMF 2026 · v{v}` (edition alone when the version
is unresolved), no longer a hardcoded literal. Rows are **role-filtered**
(D-519) so a focused Staff/Moderator never sees a dead attendee-only link.

**Not the same menu as the side drawer (BUG-017, 2026-07-26).** `MoreDrawer`
(the shell's ☰ side menu — a flat list of every destination) used to be titled
`l10n.moreTitle` too, so the app showed two different menus both called
"المزيد" / "More", and only this one carries the **language** row. The drawer
now uses its own `l10n.menuTitle` ("القائمة" / "Menu"); this screen keeps
"المزيد". Separately, the shared `SimfLanguageToggle` was added to the signed-in
Home greeting header, because the header toggle was on every screen except Home
— leaving Home with no route to the language switch at all.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `more_screen.dart` (96) | `MoreScreen` (`ConsumerWidget`) — the `_moreProfileProvider` (a tiny best-effort dashboard wrapper, kept private in the screen) and the `build` that stacks the profile card, the three sections, sign-out and the footer. |
| `more_menu_items.dart` (`MoreMenuEntry` + `moreMenuEntries(l10n)`) | A feature-local pure helper — the destination list the shell's slide-in `MoreDrawer` renders. It lives here because the entries are this feature's, but **this screen does not read it**: the drawer is a flat list of every destination, this page is three curated groups (see the BUG-017 note above). |
| `widgets/more_profile_card.dart` (`MoreProfileCard`) | The منطقتي header card — avatar, "منطقتي" title + `{name} · {tier}` sub-line, gold caret. |
| `widgets/more_list.dart` (`MoreSection` + `MoreRow`) | A titled group of nav rows + one nav row (title / optional trailing value / gold caret). |
| `widgets/more_forum_info_section.dart` · `widgets/more_settings_section.dart` · `widgets/more_legal_section.dart` | One widget per group — معلومات الملتقى, الإعدادات, قانوني. The forum-info and legal sections take the effective `AppRole` and apply the D-519 row filter themselves; the settings section takes the account email instead (its rows are role-independent) and is the `ConsumerWidget` that owns the language row. |
| `widgets/more_footer.dart` (`MoreFooter`) | The sign-out link + the version line off `installedAppVersionProvider` (D-736). |

`_moreProfileProvider` is a 7-line private best-effort wrapper (kept in the screen
— a private provider used by one screen is the documented exception to
"providers live in `data/`", and the test drives it via the
`myAreaRepositoryProvider` override). Screen was already fully tokenised (no raw
`Color(0x..)`). Every file ≤400 lines.

**2026-08-18 (delivery clean-code programme, structure only):** the three groups
and the footer came out of the screen's `build`, taking `more_screen.dart` from
181 to **96** lines; the table above is updated to match. Behaviour-preserving —
the `more_1129-17224` golden held **without** `--update-goldens` and the screen
tests passed unchanged.

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
- **D-736** (footer version line sourced from the real installed version via `package_info_plus`).
