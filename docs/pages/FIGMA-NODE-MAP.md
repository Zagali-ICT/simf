# SIMF App — Figma node-id map (KSA-Project)

Figma file: `PSXHhY0UVTAPSaIOf9uNKd` (KSA-Project).
Every app screen below is bound to its authoritative Figma node. Open a node with
`https://www.figma.com/design/PSXHhY0UVTAPSaIOf9uNKd/KSA-Project?node-id=<NODE>`.

This map is the source of truth for the 2026-06-29 pixel-parity + responsive batch.
Pixel-parity is proven per screen with a golden render at the Figma frame size
(see the golden recipe in the team memory) plus an on-device render.

| App screen | File | Figma node | Notes |
|---|---|---|---|
| Standard top-nav (spec source) | `lib/app/widgets/ksa_shell.dart` (`KsaPage._defaultHeader`) | `758-1469` (badge screen — see Badge QR row below), `922-2824` (sponsors) | Back btn (42×42 navy `#192b41` rounded) + centred 18px SemiBold title + bottom hairline; fixed height; reusable across all non-home pages |
| Bottom nav component | `lib/app/widgets/ksa_shell.dart` | `206-1732` | Tab bar shell component |
| Home — signed-in (responsive) | `lib/features/home/home_screen.dart` | `758-1134` | Greeting header + action cluster + highlights carousel |
| Home — guest | `lib/features/home/home_screen.dart` | `758-2910` | Guest variant of Home; same screen, signed-in content hidden |
| Home — highlights (ابرز الاحداث) | `lib/features/home/home_screen.dart` (`_HighlightsCarousel`) | `758-1239` (title) / `758-1238` (container) | **Deliberate deviation** — see the note below: multi-slide image+text carousel, animated, CP-managed (supersedes the static single-card Figma frame) |
| My Area (منطقتي) | — | `758-1283` | Profile / attendee dashboard |
| Venue Map (الخريطة) | — | `758-1358` | Google Map demo (Phase 1) |
| Badge QR (بطاقة الدخول) | — | `758-1469` | Entry badge + QR code. Same node as top-nav spec source — the top-nav spec was derived by inspecting this frame |
| Sponsors (الرعاة) | `lib/features/sponsors/sponsors_screen.dart` | `922-2824` | Strategic/Premium/Gold tiers; responsive grid; standard top-nav |
| Exhibition / Booths (المعرض) | `lib/features/booths/booths_screen.dart` | `922-2458` | Booth cards: flag + company + code (A-12) + HALL badge + "أرشدني إلى الجناح" CTA |
| Speakers (المتحدثون) | `lib/features/speakers/speakers_screen.dart` | `908-1744` | Sort + search row; speaker cards (photo, name, title, gold chevron, verified badge) |
| Speaker profile | `lib/features/speakers/speaker_profile_screen.dart` | `908-2110` | "About Speaker": two-line header (white name over beige rank) + circled back, 125px white avatar ringed gold (anchor placeholder), 4 CV pills (active نبذة عنه gold on the right, the rest border-only/no-fill), navy `#192B41` CV card with right-aligned white body, **text-only** gold طلب مقابلة CTA. Header shows the nationality flag (🇸🇦) leading the name (D-542 — from the existing `countryId`→flag helper `lib/core/country_flag.dart`, the same one the speaker list uses; no backend change) |
| Programme schedule (برنامج الملتقى) | `lib/features/sessions/sessions_screen.dart` | `883-2308` | Search + day strip + "تفاصيل اليوم" banner + filter chips + المواعيد timeline; tap a session → `889-2450` |
| Session detail (تفاصيل الجلسة) | `lib/features/sessions/session_detail_screen.dart` | `889-2450` | Index badge + date/time + summary/link btns + description + speakers + ask-host + my-seat + reminder/add-to-calendar |
| Archive + Archive detail (الأرشيف) | — | `925-3079` | Single combined frame covering archive list (#24) and edition detail (#24-01) |
| Live broadcast (البث المباشر) | — | `934-3450` | Session live feed screen |
| Send question (إرسال سؤال) | — | `934-3636` | Q&A send-question sheet (during live session) |
| My sessions / session presentations (عروض الجلسات) | `lib/features/myarea/my_sessions_screen.dart` | `1388-7621` | Day filter chips + cards with تحميل / قريبا; reached from My-Area dashboard session count |
| Notifications (الاشعارات) | `lib/features/notifications/notifications_screen.dart` | `758-2491` | Search + chips (الكل/جلسات/VIP) + day groups (اليوم/أمس) + **per-kind** circular category icons (colour+glyph) + unread dot. Palette: green `#13C296`, coral `#FF6347`, gold `#C9A84C`. One deviation: the VIP card uses a **star** (mockup shows a ✕ close-circle on a positive VIP invite — reads as an error); unknown/future kinds fall back to the severity colour |
| Delegations (الوفود) | `lib/features/delegations/delegations_screen.dart` | `1426-10771` | Country-level aggregate; data added via CP (mark Country invited + register delegates) |
| Scan contact (مسح QR — FDS-014) | — | `758-4380` · `758-4735` | Two states: scan view + result/preview |

## TBD — no KSA frame assigned yet

| App screen | Notes |
|---|---|
| Registration status (#11) | Pending Figma frame |
| Share my contact (FDS-014) | Pending Figma frame |
| My contacts (FDS-014) | Pending Figma frame |

## Additional bindings (merged from SIMF-App-Pages-Figma-NodeIDs.docx, 2026-06-27)

The companion `docs/SIMF-App-Pages-Figma-NodeIDs.docx` carries the per-screen node
 id read from each screen's class doc-comment (file `PSXHhY0UVTAPSaIOf9uNKd`). It
is the comprehensive per-screen source; the bindings below fill screens this
curated map omitted. For any screen not here, consult that docx before guessing.

| App screen | File | Figma node | Source |
|---|---|---|---|
| FAQ (الأسئلة الشائعة) | `lib/features/faq/faq_screen.dart` | `1388-7567` | docx #28 (shipped D-517 parity) |
| Sign-up visitor / profile data | `lib/features/profile/sign_up_visitor_screen.dart` | `168-2972` (profile) · `168-3454` (form) · `758-2616` (email-OTP) | docx #48 (Page 007) |
| Gate-operator console (staff) | `lib/features/gates/gate_scan_screen.dart` | `758-4651` | docx #32 |
| Moderator Q&A desk | `lib/features/moderation/session_moderate_screen.dart` | `1461-12227` | docx #38 (D-405/D-509) |
| Staff register visitor on-site | `lib/features/staff/register_visitor_screen.dart` | `1467-12357` | docx #64 (D-509) |
| About (عن الملتقى) | `lib/features/about/about_screen.dart` | `1116-16448` | docx #1 |
| AI assistant / chatbot | `lib/features/chatbot/chatbot_screen.dart` | `1064-13066` | docx #18 |
| Contact us | `lib/features/contact_us/contact_us_screen.dart` | `1388-7711` | docx #20 (shipped D-516) |
| Exhibitor detail | `lib/features/booths/exhibitor_detail_screen.dart` | `1439-11881` | docx #17 |

Still genuinely unbound in the docx too (truly ASK / pending): badge-activation,
badge-sign-in, biometric-step-up, forgot/reset-password (auth flow — frozen
chrome), audience-comments (removed), my-contacts / scan-contact / share-my-contact
(FDS-014), registration-status (#11).

## Removed / dissolved screens

| App screen | Decision | Notes |
|---|---|---|
| Media gallery (#30) | Dissolved — content embedded in Home page | No standalone screen in V1 |
| Audience comments (#28) | Removed from app | — |
| Guest mode (#12) | Not a separate screen — guest users see Home (`758-2910`) directly | — |

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
| Notifications | `test/golden/goldens/notifications_758-2491.png` | `758-2491` | proves the D-531 **per-kind** category palette (gold ticket / green check + card / coral star + busy-mark) + unread dots + الكل·جلسات·VIP chips; the fake repo throws from `markAllRead` so the screen's open-time auto-mark-read doesn't strip the unread dots before the shot |
| Sessions (programme) | `test/golden/goldens/sessions_883-2308.png` | `883-2308` | day strip (selected day navy / sessions-day white), day title + banner (anchor-box fallback, no HTTP), الكل-active type tabs, featured first row (time chip + banner + 3-line desc) + collapsed row; fake `SessionsRepository.getDays()`; fixed dates (no now-filtering) so it is deterministic |
| My-Sessions | `test/golden/goldens/my_sessions_1388-9067.png` | `1388-9067` | 4 equal-width tabs (القادمة selected), count header, cards with time·category + speaker·rank + hall and the مفضلة heart; `mySessionsProvider` overridden + `sessionFavouritesProvider` (an **AsyncNotifier** → notifier-factory override) seeded so 2 of 3 hearts are filled; far-future `startUtc` keeps the upcoming tab populated forever while the card shows only time-of-day |
| Session-detail | `test/golden/goldens/session_detail_889-2450.png` | `889-2450` | richest state (signed-in + held assigned-seat + live feed): header card (title + gold code badge + meta), ملخص الجلسة + رابط الجلسة buttons, description, speaker, ask-host (enabled), booking card (الصف B · مقعد 12 · بانتظار الموافقة · إلغاء الحجز) + CTAs; wiring + fakes mirror `session_detail_screen_test.dart` (detail + seat-map + calendar + signed-in auth). **Extra env-artifact:** the Material CTA labels render as tofu (button font lacks Arabic + no headless fallback) — strings are asserted correct by the widget test |
| Speaker-profile | `test/golden/goldens/speaker_profile_908-2110.png` | `908-2110` | header (name over rank + circled back), 125px gold-ring avatar (anchor SVG placeholder — `Image.network` falls back, no HTTP), 4 CV pills (نبذة عنه active gold on the **right**, the other three border-only over the navySurface scaffold), navy CV card (px-8/py-16) with right-aligned white body, **text-only** gold طلب مقابلة CTA. The FilledButton label + the nationality flag (🇸🇦, leading the name) render as tofu (Arabic-glyph / colour-emoji env artifacts) but their positions are verifiable; flag from `countryId` via `lib/core/country_flag.dart` (D-542) |
| Delegations | `test/golden/goldens/delegations_1426-10771.png` | `1426-10771` | stats strip (8 دولة مشاركة left / 54 إجمالي المشاركين right + scattered flags + faint gold grid), search field (search glyph at inline-start = right, filter cell walled at inline-end = left), per-country card: flag box + bilingual name on the right, head box (gold avatar + gold name on the right, رئيس الوفد chip on the left), bottom row — member chip on the **right** with the groups glyph leading it, date range on the **left** with the clock glyph leading it. Country flags render as tofu (colour-emoji env artifact); `delegationsProvider` overridden with 3 fixed countries |

Known golden limitations: `Image.network` always falls back (no HTTP in tests)
and colour-emoji glyphs (flags) render as tofu — both are render-environment
artifacts, not layout drift. The goldens prove **layout/structure/colour/RTL**
parity; image/flag *content* is data/asset-driven.

## Colour tokens (from 922-2824)
- BG `#192B41` · Primary text `#FFFFFF` · Secondary/gold `#C9A84C` · Primary/deep `#01132D` · Paragraph `#C2B8A2`.
