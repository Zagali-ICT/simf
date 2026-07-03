# My Area (منطقتي) — mobile `/my-area`

| Field | Value |
|---|---|
| Route | `/my-area` (`RouteNames.myArea`, page #14) · Visitor (signed-in) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/myarea/my_area_screen.dart` (`MyAreaScreen`, 269 lines — state + build branches) |
| Widgets | `lib/features/myarea/widgets/` — `my_area_dashboard_body` (`MyAreaDashboardBody`, the Approved dashboard) · `my_area_identity_card` (`MyAreaIdentityCard` + tappable avatar) · `my_area_rows` (`MyAreaScheduleRow`/`MyAreaScheduleGroupHeader`/`MyAreaMoreRow`/`MyAreaShareTile`) |
| Figma node | `213:963` / `758:1283` |
| Shell | `SimfPageShell` (`SimfTab.profile`, sweep on) |
| API | `GET /app/account/dashboard` (Approved only) · `.vcf` contact export · avatar/ID uploads |
| Providers | `myAreaRepositoryProvider` · `authControllerProvider` · `referenceNumberProvider` · `sessionFavouritesProvider` · `avatarBustProvider` |
| Tests | `test/features/myarea/my_area_screen_test.dart`; golden `test/golden/my_area_golden_test.dart` (`goldens/my_area_213-963.png`); E2E [`mobile-my-area.md`](../../../tests/e2e/mobile-my-area.md) |
| Legacy detail | `docs/App/Page_014/` — retained as the historical spec |
| Status | ✅ Real — D-396 (213:963 parity) → D-584 saved counter → **clean-code frozen (D-607)** |

## 1. Purpose
The attendee's home hub: identity card (avatar/name/tier/reference + share),
the two share pills, the الإحصائيات stat tiles (meetings / saved), the جدولي
اليوم schedule (session + meeting groups), and the المزيد settings rows.

## 2. Audience & access
Signed-in Visitor. An **Approved** account loads the dashboard; a
pending/rejected account gets the limited cached-identity view (no dashboard
call — it would 403, L-5).

## 3. Button / action audit (Level F, 2026-07-04)
| Control | Handler | Backend |
|---|---|---|
| Back | `backOrHome` | — |
| Avatar (camera) | identity-verification flow → upload | `POST` avatar |
| مشاركة / مشاركة جهة اتصال | `.vcf` native share | `GET …/contact.vcf` |
| مشاركة ملفي | push `shareMyContact` | — |
| مقابلات stat | push `myMeetings` | — |
| جلسات محفوظة stat | push `savedSessions` | favourites count |
| Schedule (session) row | push `sessionDetail` #17 | — |
| بطاقتي / الطلبات / احجز مقعداً / المزيد | push respective routes | — |
| تحديث صورة الهوية | gallery pick → upload | `POST` ID image |
| Face-ID toggle | enable/disable (self-hides w/o biometric) | — |
| Retry / pull-to-refresh | `_load()` | `GET …/dashboard` |

All data repo-backed; no missing API.

## 4. Clean-code freeze (D-607)
**790 → 269-line screen** + 3 widget files (all <400; the Approved dashboard
body extracted to a `ConsumerWidget` that owns its navigation + favourites
watch, taking the 3 async account callbacks). Replaced a local
LayoutBuilder+ConstrainedBox error-state wrapper with the shared
`SimfPullableHost`. Golden captured at 213:963 (initials avatar — the
authenticated photo yields no HTTP in tests) and overlay-checked against the
frame mapping; the D-396 parity holds (render-preserving decomposition, 12
module tests green).
