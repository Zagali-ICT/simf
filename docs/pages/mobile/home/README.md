# Home (الرئيسية) — mobile `/`

| Field | Value |
|---|---|
| Route | `/` (`RouteNames.home`, page #13) — the landing screen · Guest+ |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/home/home_screen.dart` (`HomeScreen`, 111 lines — role router only) |
| Helpers | `lib/features/home/home_greeting.dart` (`homeGreeting` / `homePostTime`, re-exported from the screen) |
| Widgets | `lib/features/home/widgets/` — `guest_home` · `operational_homes` (staff/moderator) · `visitor_home` · `greeting_header` · `home_banners` (live + discover hero) · `highlights_carousel` · `follow_us_section` · `discover_saudi_row` · `home_icons` |
| Figma nodes | signed-in **758:1134** · guest **758:2910** · highlights carousel 758:1239 (documented multi-slide deviation) |
| Shell | `SimfPageShell` (`SimfTab.home`); signed-in uses the `GreetingHeader`, guest/staff/moderator use the standard header |
| API | `GET /app/notifications/unread-count` (bell badge, signed-in) · `GET /app/news` (highlights, reused) · `GET /app/me/dashboard` (best-effort greeting name); all best-effort — Home never blocks on them |
| Providers | `homeProfileProvider` · `unreadNotificationCountProvider` · `newsListProvider` · `orgProfileProvider` |
| Tests | `test/features/home/home_screen_test.dart` (29); goldens `test/golden/home_golden_test.dart` (`goldens/home_signed_in_758-1134.png` + `home_guest_758-2910.png`); E2E [`mobile-home.md`](../../../tests/e2e/mobile-home.md) |
| Legacy detail | `docs/App/Page_013/` — retained as the historical spec |
| Status | ✅ Real — built → 758:1134/2910 parity → **clean-code frozen (D-602)** |

## 1. Purpose
The landing screen; one route with four role layouts off the cached auth
privilege: **guest** (also shown to a signed-in-but-unapproved account), the
focused **staff** and **moderator** operational homes (D-519), and the
**visitor/exhibitor** signed-in home.

## 2. Audience & access
Guest+ (public). Signed-in state and role select the layout; a pending/rejected
account sees the guest layout with an awaiting-approval note.

## 3. UI & behaviour
- **Guest** (758:2910): browse banner, 2×2 public tiles, locked بطاقتي card,
  the open-info FAQ + روح السعودية rows, and the sign-in CTA (or the pending
  note).
- **Visitor** (758:1134): `GreetingHeader` (avatar → My Area, the static
  "مرحبًا" welcome + the user's **first name** only (owner 2026-07-21),
  bell-with-unread-badge + language/theme/menu cluster), discover hero →
  News, LIVE banner → live, the عن الملتقى bar + 4-up about tiles + اسأل المحاور
  tile, the news tiles, the الميزات الذكية smart tiles, the الرعاة + الأخبار
  bars, the **highlights carousel** (auto-advancing image+title slides — a
  documented multi-slide deviation from the single-card frame; hidden until a
  post exists), the discover row, and the self-hiding follow-us row.
- **Exhibitor**: the visitor home + a lead-capture tools section (scan visitor /
  my visitors).
- **Staff / Moderator**: single-purpose rows into their own tools only.

## 4. Button / action audit (Level F, 2026-07-03)
Every tile/row is a real navigation to its screen (sessions, speakers,
venue-map, booths, faq, about, delegations, session-presentations,
send-question, requests, archive, meet-people, chatbot, session-summaries,
badge, sponsors, news, more, gate-scanner, staff-register-visitor,
scan-visitor, my-visitors, sign-in). The bell → notifications; the avatar → My
Area; the follow-us + روح السعودية rows → external links via the
confirm-then-launch gate; the LIVE banner is static config (D10, L-6). The
locked guest بطاقتي card is intentionally inert. No hardcoded data on the page
beyond the static LIVE banner copy; all dynamic content is repo-backed and
best-effort. No missing API.

## 5. Clean-code freeze (D-602)
**1,271 → 111-line screen** (role router only) + a greeting helper + 9 widget
files, every file <400, all stateless except the carousel (its own timer).
Shared tile/row/section widgets (`SimfNavTile`/`SimfTileRow`/`SimfListRow`/
`SimfLinkRow`/`SimfSectionHeader`/`SimfAvatar`/`SimfHeaderActions`) already live
in the shell — **reused, not recreated** (the DRY target was already met).
`_HomeIcons` and `_DiscoverSaudiRow` became the public shared `HomeIcons` /
`DiscoverSaudiRow` (used by both guest + visitor). (The former `now` seam on
`GreetingHeader`/`VisitorHome`, added for a deterministic time-of-day greeting,
was removed on 2026-07-21 when the greeting became the static "مرحبًا".) Goldens
captured for **both** states
and overlay-verified against 758:1134 / 758:2910. Behaviour byte-identical (29
tests green).
