# SIMF App — Figma node-id map (KSA-Project)

Figma file: `PSXHhY0UVTAPSaIOf9uNKd` (KSA-Project).
Every app screen below is bound to its authoritative Figma node. Open a node with
`https://www.figma.com/design/PSXHhY0UVTAPSaIOf9uNKd/KSA-Project?node-id=<NODE>`.

This map is the source of truth for the 2026-06-28 pixel-parity + responsive batch.
Pixel-parity is proven per screen with a golden render at the Figma frame size
(see the golden recipe in the team memory) plus an on-device render.

| App screen | File | Figma node | Notes |
|---|---|---|---|
| Standard top-nav (spec source) | `lib/app/widgets/ksa_shell.dart` (`KsaPage._defaultHeader`) | `758-1469` (badge), `922-2824` (sponsors) | Back btn (42×42 navy `#192b41` rounded) + centred 18px SemiBold title + bottom hairline; fixed height; reusable across all non-home pages |
| Home (responsive) | `lib/features/home/home_screen.dart` | `758-1134` | Greeting header + action cluster + highlights carousel |
| Home — highlights (ابرز الاحداث) | `lib/features/home/home_screen.dart` (`_HighlightsCarousel`) | `758-1239` (title) / `758-1238` (container) | **Deliberate deviation** — see the note below: multi-slide image+text carousel, animated, CP-managed (supersedes the static single-card Figma frame) |
| Sponsors (الرعاة) | `lib/features/sponsors/sponsors_screen.dart` | `922-2824` | Strategic/Premium/Gold tiers; responsive grid; standard top-nav |
| Exhibition / Booths (المعرض) | `lib/features/booths/booths_screen.dart` | `922-2458` | Booth cards: flag + company + code (A-12) + HALL badge + "أرشدني إلى الجناح" CTA |
| Speakers (المتحدثون) | `lib/features/speakers/speakers_screen.dart` | `908-1744` | Sort + search row; speaker cards (photo, name, title, gold chevron, verified badge) |
| Speaker profile | `lib/features/speakers/speaker_profile_screen.dart` | `908-1744` (list) → detail | |
| Programme schedule (برنامج الملتقى) | `lib/features/sessions/sessions_screen.dart` | `883-2308` | Search + day strip + "تفاصيل اليوم" banner + filter chips + المواعيد timeline; tap a session → `889-2450` |
| Session detail (تفاصيل الجلسة) | `lib/features/sessions/session_detail_screen.dart` | `889-2450` | Index badge + date/time + summary/link btns + description + speakers + ask-host + my-seat + reminder/add-to-calendar |
| My sessions / session presentations (عروض الجلسات) | `lib/features/myarea/my_sessions_screen.dart` | `1388-7621` | Day filter chips + cards with تحميل / قريبا; reached from My-Area dashboard session count |
| Notifications (الاشعارات) | `lib/features/notifications/notifications_screen.dart` | `758-2491` | Search + chips (الكل/جلسات/VIP) + day groups (اليوم/أمس) + typed circular icons + unread dot |
| Delegations (الوفود) | `lib/features/delegations/delegations_screen.dart` | `1426-10771` | Country-level aggregate; data added via CP (mark Country invited + register delegates) |

## Standard top-nav spec (from 758-1469 / 922-2824)
- Header container: full width, bottom hairline border (`#c9a84c` ~0.1px / beige hairline).
- Back button: 42×42, bg `#192b41`, radius 22, gold/white chevron 24px, mirrors in RTL.
- Title: centred, Inter/FS-Albert SemiBold 18px, white, single line ellipsis.
- Fixed header height below the status bar (~56–66px).
- **Resolved (owner 2026-06-28):** sub-page nav matches Figma — back + title + line only, **no** bell/language/theme/menu cluster. The cluster lives on the Home greeting header (the guest home opts in). Implemented as `KsaPage.showHeaderActions` (default **false**); the 2026-06-18 every-page-cluster invariant is superseded for sub-pages.

## Highlights carousel — deliberate deviation from Figma 758-1238 (owner 2026-06-28)

The Figma frame `758-1238` ("ابرز الاحداث") shows the **old** design: a single
news card with a source row (`SIMF@ · قبل ساعة`), an avatar box, a separate image
container (`758-1250`), and an engagement-counts row (`58` repost / `340` comment /
`1.2k` heart — `758-1252`).

The owner explicitly redefined this section on 2026-06-28:

> "في الاول كانت صورة واحدة امنا الان فهو معرض صور" — *before it was a single
> image, now it is an image gallery* … "عرض شرايح متعددة / الصورة والنص فقط وه
> متحرك ومدخل عبر لوحة التحكم" — *multiple slides / image and text only / animated
> / entered via the Control Panel.*

So the shipped `_HighlightsCarousel` (D-527) **intentionally** drops the source
row, the avatar, and the engagement counts ("image and text only"), and replaces
the single card with an auto-advancing, swipeable PageView of image+title slides
plus position dots. It reuses the existing CP-managed news list (`/admin/news` →
`GET /app/news`, image via the anonymous D-357 `NewsImage` route) — **no new table
or API**. The engagement counts in the frame are admin-entered data deferred to
Phase 2 and are **not faked**. A Figma-pixel golden does **not** apply to this
section; behaviour is covered by `test/features/home/home_screen_test.dart`.

## Golden-render coverage (pixel-parity proof)

Committed golden tests render the screen at its exact Figma frame size with the
real brand fonts loaded (`test/golden/golden_fonts.dart`), so the PNG can be
diffed against the frame. Regenerate with
`flutter test --update-goldens test/golden/<screen>_golden_test.dart`.

| Screen | Golden | Figma frame | Notes |
|---|---|---|---|
| Speakers | `test/golden/goldens/speakers_908-1744.png` | `908-1744` | initials avatars (no network needed) |
| Sponsors | `test/golden/goldens/sponsors_922-2824.png` | `922-2824` | logo badges fall back to initials (real `SponsorLogo` loads over the network in production) |
| Booths | `test/golden/goldens/booths_922-2458.png` | `922-2458` | logo tile = initials fallback (real `CompanyLogo` in prod); corner flag is a tofu box in goldens (colour-emoji font not loaded); hall box shows the single localized name per D-432 (frame's "· HALL A" bilingual label simplified) |

Known golden limitations: `Image.network` always falls back (no HTTP in tests)
and colour-emoji glyphs (flags) render as tofu — both are render-environment
artifacts, not layout drift. The goldens prove **layout/structure/colour/RTL**
parity; image/flag *content* is data/asset-driven.

## Colour tokens (from 922-2824)
- BG `#192B41` · Primary text `#FFFFFF` · Secondary/gold `#C9A84C` · Primary/deep `#01132D` · Paragraph `#C2B8A2`.
