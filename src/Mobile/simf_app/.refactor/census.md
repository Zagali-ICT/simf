# Module census + Figma parity ledger — clean-code refactor (Phase 0f)

Captured 2026-06-29 on `refactor/clean-code`. 41 feature folders; shared shell
`lib/app/widgets/simf_page_shell.dart` = 1,113 lines (most cross-cutting file).
Driver: the per-module order in `D:\SIMF\System\CleanCode\REFACTOR_PLAN.md`.

## Module order (worst/most-used first) + size + parity status

Legend — **G** = golden-locked (Level 4 = verify the golden, don't re-audit);
**N?** = Figma node not in `docs/pages/FIGMA-NODE-MAP.md` → **ASK the owner before Level 4**.

| # | Module / screen | lines | node (FIGMA-NODE-MAP) | golden |
|---|---|---|---|---|
| 1 | profile/sign_up_visitor_screen | 2245 | N? (registration/profile frames not bound) | — |
| 2 | sessions/session_detail_screen | 1373 | 889-2450 | G |
|   | sessions/sessions_screen | 797 | 883-2308 | G |
| 3 | home/home_screen | 1263 | 758-1134 / 758-2910 (guest) | — |
| 4 | live/live_broadcast_screen | 1217 | 934-3450 | — |
| 5 | staff/register_visitor_screen | 1007 | N? | — |
|   | gates/gate_scan_screen | 922 | N? | — |
|   | archive/archive_screen | 893 | 925-3079 | G |
|   | speakers/speaker_profile_screen | 799 | 908-2110 | G |
|   | myarea/my_area_screen | 786 | 758-1283 | — |
|   | booths/booths_screen | 777 | 922-2458 | G |
|   | venuemap/venue_map_screen | 775 | 758-1358 | — |
|   | ai_summary/session_summary_screen | 715 | (summary 1072-13518) | G |
|   | notifications/notifications_screen | 687 | 758-2491 | G |
|   | delegations/delegations_screen | 678 | 1426-10771 | G |
| 6 | auth/sign_in_screen | 733 | 168-2800 (frozen chrome) | — |
|   | moderation/session_moderate_screen | 698 | N? | — |
|   | requests/requests_screen | 643 | 1408-9726 | — |
|   | sessions/my_seat_screen | 641 | 898-2873 | — |
|   | ai_summary/session_summary_list_screen | 592 | 1388-8392 | G |
| 7 | comments/audience_comments_screen | 544 | removed-from-app (per node map) | — |
|   | gallery/gallery_screen | 541 | dissolved-into-home | — |
|   | contact_us/contact_us_screen | 523 | (D-516 parity) | — |
|   | feedback/rate_screen | 514 | (D-463/D-465) | — |
|   | sessions/session_presentations_screen | — | 1388-7621 | G |
|   | + long tail (about 390, faq 165, more, news, sponsors, media_partners, meet, forum_guide, onboarding, etc.) | <500 | mixed / verify per-screen | — |

Golden-locked (14): archive, booths, delegations, my_sessions, notifications,
presentations, send_question, session_detail, session_summary,
session_summary_list, sessions, speaker_profile, speakers, sponsors.

## Contention points (do in Phase 0 / serial, one owner at a time)
`lib/app/widgets/simf_page_shell.dart` (1,113) · `core/responsive/` · `core/widgets/`
· `app/theme/tokens.dart` · `lib/app/localization/app_l10n.dart` (2,008).

## Node ids — RESOLVED via SIMF-App-Pages-Figma-NodeIDs.docx (2026-06-29)
Merged into FIGMA-NODE-MAP.md: faq `1388-7567`, profile/sign_up_visitor `168-2972`,
gates `758-4651`, moderation `1461-12227`, staff `1467-12357`, about `1116-16448`,
chatbot `1064-13066`, contact_us `1388-7711`, exhibitor_detail `1439-11881`.

Still genuinely unbound (ASK only if their layout is touched): auth-secondary
(badge-activation/sign-in, biometric-step-up, forgot/reset-password — frozen
chrome), audience-comments (removed), contacts FDS-014 (my-contacts/scan/share),
registration-status (#11). Typography: text-style tokens are built **incrementally
per screen** (owner 2026-06-29) — no upfront type ramp.
