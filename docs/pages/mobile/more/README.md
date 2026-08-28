# More — المزيد (Page 041, `#41`)

- **Route:** `/more` (`RouteNames.more`). Access: **Guest+ (public)**. No API of its own (the profile card best-effort reads the dashboard).
- **Figma:** **1129:17224** (grouped re-skin, D-465). **Clean-code freeze:** D-635 (2026-07-04).

## Purpose

The app's "more" hub: a منطقتي profile header card (signed-in → My Area), three
grouped sections of bordered nav rows — **معلومات الملتقى** (about / forum-guide /
FAQ / my-sessions / VisitSaudi), **الإعدادات** (language toggle / accessibility /
notifications / reset-password),
**قانوني** (terms / privacy-policy / contact-us / rate) — the تسجيل الخروج link (signed-in) and the
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
| `widgets/more_profile_card.dart` (`MoreProfileCard`) | The منطقتي header card — avatar, "منطقتي" title + `{name} · {tier}` sub-line, gold caret. The caret renders through the shared `SimfForwardChevron`, as every `MoreRow` below it already did (see the 2026-08-20 note). |
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

**2026-08-20 (app deep-clean audit — the منطقتي caret pointed the wrong way in
English):** the profile card drew its gold caret as a bare `SimfSvgIcon`, which
never mirrors. The bundled `ic_caret_left.svg` glyph points **left**, which is
forward at the inline end in Arabic — but every `MoreRow` beneath it has gone
through the shared `SimfForwardChevron` since 2026-07-22, so under
**English** the same screen showed the card's caret pointing left while every
row's caret pointed right. The card now uses `SimfForwardChevron` too. It is a
`Transform.flip` that fires in **LTR only**, so **the Arabic render is
unchanged** — `more_1129-17224.png` is captured in Arabic and held **without**
`--update-goldens`; only the English render moves. The same bare-icon defect was
fixed in four other rows in the same pass (forum-guide step, sessions hub row,
seat reservation card, speaker session row), all covered together by
`test/features/forward_navigation_chevron_test.dart` (5 rows, one case each).

## L4 Figma parity (frame 1129:17224)

Captured `more_1129-17224.png` (@375×1000, ar, signed-in VIP) as the **baseline
before** the refactor, then **held it WITHOUT `--update`** after — proving the
3-widget extraction byte-identical. Golden read: المزيد header, the منطقتي card
(رائد السالم · VIP), the three sections (all rows; اللغة shows العربية), تسجيل
الخروج, RTL, no tofu.

## Level-F

Wired: profile card → My Area; each row navigates (about / forum-guide / faq /
accessibility / notifications / terms / contact-us / rate) or acts (language
toggle, VisitSaudi confirm-launch, privacy-policy confirm-launch); sign-out
(confirm); rate/attendee rows role-filtered. No API of its own.

**سياسة الخصوصية opens the published web policy** (`BuildConfig.privacyPolicyUrl`,
default `https://web.simrsnf.com/privacy`) through the shared leave-the-app
confirmation, rather than an in-app copy: it is a legal document that is
updated without a store release, so a second copy here would go stale
silently. Google Play requires the policy to be reachable from inside an app
that handles sensitive data — this one takes identity documents, photos, the
camera and biometrics — and not from the store listing alone. The same entry
is in the side drawer, which is why `MoreMenuEntry` now carries either a
`routeName` or an `externalUrl`.

## Tests

`test/golden/more_golden_test.dart` (frame 1129:17224, @375×1000, ar, signed-in) +
`test/features/more/more_screen_test.dart` (8 cases — the grouped sections and
their rows, the version line, guest hides the profile card + sign-out, signed-in
shows them, About navigation, reset-password → the forgot flow, and the D-710
عروض الجلسات row shown to a signed-in attendee / hidden from a guest) +
`test/features/forward_navigation_chevron_test.dart` (the منطقتي card's caret goes
through the shared chevron). E2E: `docs/tests/e2e/mobile-more.md`.

## Related decisions

- **D-635** (this clean-code freeze — profile card + list widgets + first golden).
- **D-315** (screen built), **D-465** (1129:17224 grouped re-skin), **D-519** (role-filtered rows), **D-609** (my-sessions row removed) and **D-710** (the owner reversed that removal 2026-07-09 — عروض الجلسات is back in معلومات الملتقى, attendee-gated, and opens `/my-area/sessions`).
- **D-736** (footer version line sourced from the real installed version via `package_info_plus`).
