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
| Home — highlights (ابرز الاحداث) | `lib/features/home/home_screen.dart` (`_FollowUsSection`/featured) | `922-2824` (referenced) | Multi-slide carousel of image+text, animated, CP-managed (was single image) |
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
- **Open design question (flagged to owner):** the Figma standard nav shows back+title only — no bell/language/theme/menu cluster. The current app puts the cluster on every page (owner invariant 2026-06-18). Pending owner confirmation whether the cluster stays on inner pages or moves to home only.

## Colour tokens (from 922-2824)
- BG `#192B41` · Primary text `#FFFFFF` · Secondary/gold `#C9A84C` · Primary/deep `#01132D` · Paragraph `#C2B8A2`.
