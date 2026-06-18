# SIMF App Redesign Programme — KSA-Project Figma

Last updated: 2026-06-12 · Owner directive: replace the current Flutter entry/auth
screens with the delivered KSA-Project Figma designs, one page per changeset.
This document is the **continuity anchor** (same pattern as the D-356 programme
doc): the binding decisions, the per-page status board, and the per-page gate
checklist live here. Decisions log entries: D-358 (login preview), D-359
(programme + Phase 0), D-370 (Wave 2 start).

Figma file: `PSXHhY0UVTAPSaIOf9uNKd` ("KSA Project"). Wave 1 covered the 10
frames delivered first — the **entry/auth flow** (8 app screens; the 3
onboarding frames are one carousel screen). The designer has since delivered
**11 further frames** — Wave 2 (owner-approved 2026-06-12, starting with
Page 005); see the Wave 2 board below.

## Binding programme decisions (owner-approved, 2026-06-11)

1. **Replace in place; old screens parked in a temp directory.** Each redesigned
   screen lands at the **real route and real file path**; the previous screen
   file + its widget test move to `lib/features/_legacy_mockup/` /
   `test/_legacy_mockup/` (kept compiling, never routed). Deletion of the legacy
   directory is an owner decision at programme close (§6 freeze rules).
2. **Design system first (Phase 0).** The Figma palette/typography live in
   `SimfTokens` + `SimfTheme`; screens consume tokens, never literals.
3. **Login v2 is done and gets promoted** to the official `/sign-in` (Phase 1);
   no further redesign work on it.
4. **Full Definition of Done per page** (D-246): implement → widget tests →
   analyze + full suite → live browser check vs the Figma frame → E2E catalogue
   + PAGE-INDEX + per-page doc → decisions-log entry → commit.
5. **Behaviour changes are never silent.** A design that changes a flow or
   contract (fields moved between pages, buttons added/removed) is flagged to
   the owner before implementation — design fidelity never silently changes an
   API/flow contract.

## Design system (Phase 0 as-built)

Figma variables → `SimfTokens`:

| Figma variable | Value | Token |
|---|---|---|
| Primary- Color | `#01132D` | `navy` (dark scaffold) |
| BG | `#192B41` | `navyDeep` (boxes/cards on navy) |
| Secondary- Color | `#C9A84C` | `accent` (gold) |
| Pragraph Color | `#C2B8A2` | `beigeBorder` |
| — (login frame) | `#102238` | `navySurface` (elevated navy surface) |
| — (login card) | `#F1ECE4` | `cardBeige` |
| — | `#D0AC77` | `goldSoft` |
| — | `#111827` / `#6C7278` / `#00245E` | `headlineInk` / `greyText` / `linkNavy` |

Type: Title 24 SemiBold / Sub-title 18 / Paragraph 16 — rendered in the bundled
IBM Plex Sans Arabic (D-329; the design's Inter/Plus Jakarta are not bundled).
Theme: gold `FilledButton` = white bold text, radius 4, height 48; dark
`OutlinedButton` = beige border, radius 4; selected chips = solid-gold pills.
Shared widgets: `lib/app/widgets/simf_logo.dart` (`SimfLogo`, 4x asset
`assets/images/simf_logo.png`, Figma node 159:580).

## Status board

| # | Screen | Route | Figma node(s) | Status | Commit |
|---|--------|-------|---------------|--------|--------|
| P0 | Design system (tokens/theme/logo) | — | variables + 159:580 | ✅ shipped 2026-06-11 | `e7f8c7b` |
| P1 | Promote Login v2 → official sign-in | `/sign-in` | 168:2800 | ✅ shipped 2026-06-11 (D-360) | preview `be81082`; promotion — see git log |
| 1 | Splash (Page 001) | `/splash` | 159:573 | ✅ shipped 2026-06-11 (D-361) | — |
| 2 | Onboarding carousel (Page 002) | `/onboarding` | 148:22, 159:942, 159:1052 | ✅ shipped 2026-06-11 (D-362 — intro videos dropped for static panels, owner decision; D-373 later added looping muted *background* videos per step, bundled assets) | — |
| 3 | Profile-data form (Page 007) | `/sign-up/visitor` | 168:2972 | ✅ shipped 2026-06-11 (D-368 — frame's "رقم اللوحة" skipped: no backend field; DOB/place-of-birth/national-ID path kept: API-required; live check N/A: auth-gated). ~~Page 005 register keeps its current UI (no frame delivered)~~ — superseded: the register frame arrived in Wave 2 and shipped as W2-1 (D-370). | — |
| 4 | Email-OTP verify (Page 006) | `/sign-up/otp` | 505:837 | ✅ shipped 2026-06-11 (D-364). ~~The 2FA OTP screen (`/auth/verify-otp`) keeps its old look — restyling it is a tracked follow-up~~ — delivered by D-369 (shared `OtpCodeBoxes`/`OtpMark`). | — |
| 5 | Interests picker (Page 007-01) | `/sign-up/interests` | 505:1083 | ✅ shipped 2026-06-11 (D-365 — live check N/A: auth+draft-gated; widget tests stand in) | — |
| 6 | Registration success (Page 010) | `/registration/success` | 505:1451 | ✅ shipped 2026-06-11 (D-366; superseded in part — D-369 wired the contact tiles via `SIMF_SUPPORT_PHONE`/`SIMF_SUPPORT_EMAIL` config, D-373 put the real DB-issued `referenceNumber` on the card with the mask as offline fallback; live check N/A: auth-gated) | — |
| 7 | Terms & conditions | `/terms` | 505:1553 | ✅ shipped 2026-06-11 (D-367 — bullet cards; checkbox gate replaced by the design's single موافق consent button) | — |

## Wave 2 status board (11 new frames, owner-approved 2026-06-12 — D-370)

Build order owner-set: Page 005 first; the rest follow one page per changeset,
each with its own plan→approve step before code.

| # | Screen | Route | Figma node(s) | Status | Commit |
|---|--------|-------|---------------|--------|--------|
| W2-1 | Register (Page 005) | `/sign-up` | 168:3454 | ✅ shipped 2026-06-12 (D-370 — login chrome + beige card; logic byte-identical) | — |
| W2-0 | **Shared KSA shell** (bottom nav v2 + `ksa_shell.dart`: KsaPage/KsaCard/KsaNavTile/KsaStatTile/KsaListRow/KsaTileRow/KsaAvatar/KsaError-EmptyState) | all W2 pages | nav component 206:1669 | ✅ shipped 2026-06-13 (D-378 — News tab → Profile tab, owner-approved) | `174132c` (+ simplify `2fd7816`) |
| W2-2 | Home — **guest** (note: the original board had guest/signed-in swapped; frames corrected) | `/` | 512:1492 (owner-picked 2×2 option over 512:1335) | ✅ shipped 2026-06-13 (D-378 — FAQ row → About pending an app FAQ endpoint; latest-post card omitted) | `17bb238` |
| W2-3 | Home — **signed-in** | `/` (signed-in) | 203:1236 | ✅ shipped 2026-06-13 (D-378 — same changeset as W2-2; social + Visit-Saudi links config-driven) | `17bb238` |
| W2-4 | Profile | superseded — the owner picked 512:1780 for `/my-area` (213:963 not built; its "الأرشيف" stat has no API field) | 213:963 | ❌ not built (owner pick) | — |
| W2-5 | My Place (منطقتي) | `/my-area` | 512:1780 | ✅ shipped 2026-06-13 (D-378 — language tile wired, theme tile visible-but-disabled per owner) | `734b6a7` |
| W2-6 | Location (venue map) | `/map` | 215:562 | ✅ shipped 2026-06-13 (D-378 — Google map replaced by the venue 2D plane per owner; bottom info card + gold controls) | `cf7214e` |
| W2-7 | Calendar (sessions) | `/sessions` | 215:767 | ✅ shipped 2026-06-13 (D-378 — white day strip, re-tap clears; pills relabelled to the frame copy) | `8a0387f` |
| W2-8 | QR (badge) | `/badge` | 221:769 | ✅ shipped 2026-06-13 (D-378 — gold identity strip with masked id tail; امسح لإضافة شخص → `/contacts/scan`) | `f35ffe3` |
| W2-9 | Notifications | `/notifications` | 223:4264 | ✅ shipped 2026-06-17 (D-399 — filter chips, grouped list, search) | — |
| W2-10 | Visitor home state 512:1659 | `/` | 512:1659 | not reviewed yet — folded under the home follow-up | — |

## Per-page gate checklist (every row above must pass all of these)

1. Figma design context + screenshot fetched; visual target confirmed.
2. Old screen + test parked in the legacy directory; new screen at the real path
   consumes `SimfTokens`/shared widgets; existing behaviour contract preserved.
3. Widget tests ported + extended; `flutter analyze` clean; full suite green.
4. Live browser check vs the frame: screenshot, console clean, network clean,
   no horizontal overflow, no broken images.
5. Docs in the same changeset: E2E catalogue file, PAGE-INDEX row, per-page doc.
6. Decisions-log entry; commit with a descriptive message.

## W2 batch close-out evidence (2026-06-13, D-378)

Live browser drive of a release web build (`localhost:8080`) against the local
API (`localhost:5175`, Development): **all five pages rendered live** — the
signed-in home (real session via `/auth/refresh`, live unread badge, all
sections + the 5 brand glyphs: `docs/screenshots/app-w2-home-signedin-live.png`),
منطقتي (dashboard 200, disabled theme tile, live stat tiles, empty-schedule
placeholder: `app-w2-myarea-live.png`), the agenda (real seeded sessions, white
day strip WED/THU, two-line time chips: `app-w2-agenda-live.png`), the venue
map (real seeded node, gold controls, node tap → info card + selection ring:
`app-w2-map-live.png` / `app-w2-map-infocard-live.png`), the QR badge (this
test user is pre-approval → the pending state rendered; the issued-QR path is
widget-test covered: `app-w2-badge-live.png`), and the guest home reached via a
**live D-373 sign-out → الدخول كزائر → المتابعة كضيف** chain
(`app-w2-home-guest-live.png`). Console: zero errors across the drive (only
Chrome's standard form-autofill notices); network: **every request 200**
(refresh / me / dashboard / sessions / venue-map / booths / sign-out), no
broken assets; `scrollWidth == clientWidth` (no horizontal overflow). Suites:
**403/403**, analyze clean. Simplify pass applied as commit `2fd7816`.

Minor recorded deviation: the floating map controls sit at the directional
end (left in RTL) while the frame's static mock shows them right — directional
placement keeps the LTR locale correct; flag to the designer if exact RTL-right
is wanted.

## Known open items

- **App FAQ screen + endpoint (W2 batch follow-up, D-378):** the guest home's
  الأسئلة الشائعة row currently opens the About page — only **admin** FAQ CRUD
  exists (D-218); a public `GET /app/faq` + a dedicated screen is the proper
  destination. Owner to schedule.
- **Social/Visit-Saudi link values (D-378):** `SIMF_SOCIAL_X/_INSTAGRAM/
  _LINKEDIN/_YOUTUBE/_TIKTOK` are unset (buttons inert) until the owner
  supplies the official profile URLs; `SIMF_VISIT_SAUDI_URL` defaults to the
  public Visit-Saudi site. Wired into `publish-app-web.ps1`.
- **Legacy-deletion sweep additions (W2):** when `_legacy_mockup/` is deleted
  at programme close, also remove the l10n getters only it still references
  (`sessionsAllDays`, `legendHall/Zone/Booth/Poi`, `badgeShowAtEntry`,
  `homeDiscoverTitle/Subtitle`, `liveBannerTitle/Subtitle`, `guestPromptText`,
  `sessionsTitle`) and consolidate the 7 initials helpers onto `ksaInitials`.
- **Deferred efficiency notes (simplify pass, recorded not actioned):** a
  shared cached dashboard provider for `/my-area` + `/badge` (currently two
  identical `GET /app/account/dashboard` calls when navigating between them);
  per-item precomputed search haystacks in `filterSessions`.

- **Contact tiles (Page 010) — wired via config (D-369):** the tiles open the
  OS dialer/mail through `url_launcher` (owner-approved) gated on
  `BuildConfig.supportPhone` / `supportEmail` (`--dart-define
  SIMF_SUPPORT_PHONE / SIMF_SUPPORT_EMAIL`). **Open input:** the owner still
  needs to supply the official values — empty keeps a tile inert.
- **2FA OTP screen restyle — DONE (D-369):** `/auth/verify-otp` now uses the
  shared `OtpCodeBoxes`/`OtpMark` (extracted from D-364 when this second
  consumer appeared); verified live.
- **Regression pass (D-369):** suite 334/334 + analyze clean; live drive of
  sign-in / OTP screens / terms chrome; backend API started locally —
  `/health` 200 and `GET /app/content/terms` 200 with real bilingual content
  (the in-browser loaded render is CORS-blocked for the dev-web origin; native
  builds are unaffected — the widget tests cover the loaded rendering).

- **Sign-up flow question — RESOLVED (owner, 2026-06-11):** no flow change.
  Register (Page 005, user+pwd+confirm → its own API) keeps its current UI —
  no frame was delivered for it; then OTP; then ONE profile API across TWO
  pages (frame 168:2972 = the Page 007 data form; frame 505:1083 = the
  interests page); then the success/wait screen. Frame 168:2972 is therefore
  the **Page 007 redesign**, mapped onto the existing fields/logic only.
- The design's Arabic header on the login frame ("الملتقى الدولى البحرى")
  differs in spelling from the app's `appName` ("الملتقى البحري") — designer
  typo suspected, owner to confirm.
- Legacy directory deletion at programme close (freeze §6) — owner decision.
- **Shared entry-chrome extraction (flagged by the D-370 simplify pass) — owner
  decision.** The KSA entry chrome is now copy-pasted across the redesigned
  screens: the rotated sweep block exists in **8** screens, the back+globe top
  controls and `_toggleLanguage` in 3, `_Header`/`_FieldLabel` and the bordered
  field decoration in 2–3, the gold-button style (incl. the un-tokenised
  `Color(0x80C9A84C)` disabled colour) in 2, and the test `_FakePrefs` fake in
  6 test files. Per the D-369 second-consumer rule this is past the extraction
  threshold; the fix is a dedicated changeset (shared `lib/app/widgets/` /
  `features/auth/widgets/` chrome + a `SimfTokens.accentDisabled` token +
  a shared test fake) touching the already-shipped screens — too broad to
  bundle into a single page's changeset, so it is parked here for approval.
