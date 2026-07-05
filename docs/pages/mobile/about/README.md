# About the forum — عن الملتقى (Page 037, `#37`)

- **Route:** `/about` (`RouteNames.aboutForum`). Access: **Guest+ (public)**.
- **Figma:** **1116:16448** (restructured, D-465 — per the screen doc + PAGE-INDEX). **Clean-code freeze:** D-634 (2026-07-04).
- **Node inconsistency FLAGGED (not resolved):** the class doc + PAGE-INDEX cite `1116:16448`, while `about_screen_test.dart`'s group name cites `1082:15307`. Surfaced for the owner (like badge D-633 / session_moderate D-622), not guessed.

## Purpose

The public about page: an anchor-mark header (forum name + title + optional edition
status badge), the الرسالة (mission) + الرؤية (vision) text cards, the تفاصيل
الملتقى details card (year / date / location) and the المحاور الرئيسية themes card
(four fixed forum themes). It **data-drives** from the app-lifetime
`orgProfileProvider` (D-495: name / title / status / about-items / details /
contact / version) and hydrates the vision paragraph from the CMS
(`GET /app/content/about`, D-173) — each with a **static bilingual fallback** so
the page always renders (first run / offline / unseeded key).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `about_screen.dart` (160) | `AboutScreen` + State — the best-effort CMS load, and the `build` that **assembles** the data (forum name/title/status, the about cards, the detail / contact / version rows, the themes) from `orgProfileProvider` + the CMS block + l10n fallbacks, then lays out the header + cards. |
| `widgets/about_header.dart` (`AboutHeader`) | The anchor-mark header — gold anchor + forum name, optional title, optional gold status badge. |
| `widgets/about_cards.dart` (`AboutTextCard`, `AboutDetailsCard`, `AboutThemesCard` + the shared `_Card`/`_CardHeading` chrome) | The three About card types + their shared navy-deep card chrome (colocated as file-privates — kept **local**, not the contact_us `ContactCard`: different padding [`all(16)` vs `h16/v8`] + heading [14/w700 vs 16/w500]). |

The screen defines no provider inline (it consumes shared content/org providers),
so this freeze is widget extraction only; already fully tokenised (no raw
`Color(0x..)`). Every file ≤400 lines.

## L4 Figma parity (frame 1116:16448)

Captured `about_1116-16448.png` (@375×1400, ar, static-fallback path — no profile,
CMS fails) as the **baseline before** the refactor, then **held it WITHOUT
`--update`** after — proving the header + 3-card extraction byte-identical. Golden
read: عن الملتقى header, anchor + forum name, الرسالة / الرؤية cards, the تفاصيل
الملتقى details (السنة / الزمن / المكان) and the المحاور الرئيسية themes (01–04),
RTL, no tofu.

## Level-F

Read-only content page — profile-driven with static fallback; the CMS vision
hydrate is best-effort (failure → static). No actions beyond back. Reads
`GET /app/content/about` + `orgProfileProvider`.

## Tests

`test/golden/about_golden_test.dart` (frame 1116:16448, @375×1400, ar, static path)
+ `test/features/about/about_screen_test.dart` (static fallback, server-error
degrade, Arabic theme-number position, D-495 profile-driven name/status/contact/
version). E2E: `docs/tests/e2e/mobile-about.md`.

## Related decisions

- **D-634** (this clean-code freeze — header + 3-card extraction + first golden; node inconsistency flagged).
- **D-311** (screen built), **D-465** (1116:16448 restructure), **D-495** (org-profile data-drive), **D-173** (CMS content block).
