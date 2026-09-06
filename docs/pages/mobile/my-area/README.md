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

A **true guest** (no account at all) also reaches this screen: the bottom nav
switches tabs **inside** `SimfAppShell`'s IndexedStack, so no go_router
navigation happens and the router's auth gate on route 14 never runs. It used to
fall through to the limited view and show `myAreaPendingNote` ("your account is
under review") — describing a registration the guest never submitted, with no way
out (**BUG-013**, 2026-07-26). A signed-out user now gets the shared
`SimfGuestPrompt` — `myAreaGuestNote` plus **Sign in** / **Create account**
actions; the pending copy is unchanged for a genuinely pending/rejected account.

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
| حذف حسابي / Delete my account | `DeleteAccountTile` → confirm → code screen | `POST …/delete/send-code`, then `DELETE /app/account` |
| Retry / pull-to-refresh | `_load()` | `GET …/dashboard` |

All data repo-backed; no missing API.

## 3.1 Account deletion (App Store 5.1.1(v) / Google Play)

The control is a **full-width outlined destructive button** with a trash icon
(`lib/features/myarea/widgets/delete_account_tile.dart`), mounted here twice —
`my_area_screen.dart` and `my_area_more_section.dart`. It was a quiet red line of
text until 2026-09-05, and Apple rejected the app under 5.1.1(v) for it.

**This screen is not the only place it lives, and that is the whole point.** The
bottom navigation that reaches My Area only appears once an account is approved,
and `post_auth_route.dart` sends any account with `profileComplete == false` back
to the ID-capture form on every launch — so for a half-registered account that
form *is* the whole app, and a reviewer standing there could not find deletion at
all. `AccountDeletionFooter`
(`lib/core/widgets/account_deletion_footer.dart`) therefore mounts the same
control on the sign-up visitor form, the interests step and the approval-status
screen; the two here mount `DeleteAccountTile` directly.
`test/repo/account_deletion_reachable_test.dart` pins all five sites with a
reason each and fails the build if one is dropped.

**Confirming is not the point of no return.** The dialog opens
`DeleteAccountCodeScreen`, which asks the server to email a six-digit code and
submits the deletion itself — it does not hand a verified code back, because
there is no verify-only endpoint and re-entering would send a fresh code (five
sends an hour, so four typos would lock deletion out for an hour). Refusals
answer **403, never 401**: on a 401 the API client refreshes and replays, which
would burn two attempts, and a failed refresh signs the holder out mid-deletion.
Deletion also revokes the biometric device key before signing out, so Face-ID
cannot resume a session for an account that no longer exists.

Catalogue: [`mobile-delete-account-code.md`](../../../tests/e2e/mobile-delete-account-code.md)
(E2E-MOBDEL) and E2E-MOB014-020 in this page's own file.

## 4. Clean-code freeze (D-607)
**790 → 269-line screen** + 3 widget files (all <400; the Approved dashboard
body extracted to a `ConsumerWidget` that owns its navigation + favourites
watch, taking the 3 async account callbacks). Replaced a local
LayoutBuilder+ConstrainedBox error-state wrapper with the shared
`SimfPullableHost`. Golden captured at 213:963 (initials avatar — the
authenticated photo yields no HTTP in tests) and overlay-checked against the
frame mapping; the D-396 parity holds (render-preserving decomposition, 12
module tests green).
