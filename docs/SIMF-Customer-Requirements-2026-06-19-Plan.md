# SIMF — Customer Requirements Intake & Wave Plan (2026-06-19)

Source: owner requirements batch for the **App + Control Panel**, captured 2026-06-19.
This plan is grounded in a verified codebase survey (see file:line references). It is a
plan of record, not a substitute for the per-wave §11 change plan.

## Two upfront owner decisions (BLOCKING for waves F & G)

1. **Delegations + session meeting-requests were PERMANENTLY deleted by owner directive.**
   - **D-277** (2026-06-04) removed the entire `Delegations` stack (`Country`-linked
     entity, CRUD, CP page, perms, ~70 resx, Flutter route). Directive: *"for delegation,
     remove permanently."* DB table dropped.
   - **D-278** (2026-06-04) removed the session "request interview" meeting-request
     (mockup screen 27). Directive: *"for session MeetingRequest, remove permanently."*
   - Requirements **#10 (delegations/isWafd)** and **#11 (meetings)** rebuild these →
     need a fresh DECISIONS_LOG entry reversing D-277/D-278, else they get re-deleted.
2. **Schema freeze (D-110)** is wave-lifted but must be re-instated before handover.
   Items #4, #9, #10, #11 need additive migrations + a decision entry; the shipped
   **mobile wire contract stays append-only**.

## Requirements → status → approach

### Group A — Validation fixes (small, no schema) — **IN PROGRESS (2026-06-19)**
- **#1 Name = 2 to 4 parts.** Current rule enforces **≥4 parts** (`HaveAtLeastFourParts`)
  in app (`sign_up_visitor_screen.dart:511,526`) + self-service API
  (`UpsertUserProfileRequestValidator.cs:92`). New rule loosens min 4 → **min 2, max 4**.
  CP walk-in validator (`AdminWalkInRegistrationRequestValidator.cs:45-59`) enforces
  neither part-count nor letters-only → bring to parity.
- **#2/#3 Saudi National ID.** App + self-service already use `^1\d{9}$` (citizen) /
  `^2\d{9}$` (Iqama) **with Luhn**. Only gap: CP walk-in skips Luhn
  (`AdminWalkInRegistrationRequestValidator.cs:89,106`) → add the existing
  `IsValidLuhn`. (The reported "10xxxx" is not in live code — already `1xxxx`.)
- **#5 Plate.** `PlateNumber` exists, nullable, 3 letters (AR or Latin) + 1–4 digits
  (`plate_validation.dart`, `UpsertUserProfileRequestValidator.cs:256`). Gaps: walk-in
  validator is length-only; restrict letters to the **17 official Saudi plate letters**;
  **owner add-on (2026-06-19): plate must carry both AR and EN letter forms.** Approach:
  shared 17-letter AR↔EN bijection map (C# + Dart), validate against it, store one
  canonical form and **derive the other on read** (no schema change, respects freeze &
  "no duplicated data"). Persisted-second-column variant deferred (needs migration +
  owner approval).

### Group B — Birth location dropdown (medium, +schema)
- **#4** `PlaceOfBirth` is free text today; no city lookup exists. New `SaudiCity` lookup
  (mirror `CountryConfiguration`) + public read endpoint (mirror
  `ProfileCountriesEndpoint`); Saudi → dropdown, non-Saudi → free text "as in passport".
  Needs the controlled city/region list from the client.

### Group C — CP-configurable content (medium; one shared read-path)
- **#6 Registration success message** + **#7 social links**. `SystemSettings` store +
  CRUD + `/admin/configuration` page exist (D-229) but ship empty with **no read-path**.
  Build `GetByKeyAsync` + a public settings endpoint once, then drive the registration
  congrats text (today hardcoded `app_l10n.dart:352`) and social links (today
  `index.html:2319` placeholders / `build_config.dart:49` env vars) from settings.

### Group D — Shareable contact QR (medium)
- **#8** Share QR encodes an opaque token today (`share_my_contact_screen.dart:171`);
  scanner does no parsing (`qr_scan_view.dart`). Encode a **vCard/MeCard** (Arabic name +
  phones) so any phone camera reads it; add a scanner branch to import vCard locally; add
  TEL + Arabic `FN` to the My-Area self-card builder (`MyAreaEndpoints.BuildVCard:150`).
  Trade-off: a self-readable vCard loses the revocable token.

### Group E — Session AI/subtitle approval workflow (large)
- **#9** AI seam (`IAiService`/`EchoAiProvider`/`OpenAiProvider`) + prompt catalogue +
  the `SessionSummary` draft→edit→publish pattern + the Q&A approve→moderator pipeline all
  exist. New: a status state machine for the subtitle/data content (AiDrafted → InReview →
  Approved → ReadyForModerator), AI prompt, CP review desk (mirror
  `SessionSummariesList.razor`), and موderator consumption of approved content.

### Group F — Delegations / Wafd (large, reverses D-277)
- **#10** Add `Country.IsInvited` (24 countries), `isWafd` flag (mirror
  `UserProfileType.IsForVisitor`), extend XLSX bulk import. "Profile-only, no account"
  is new behaviour (only `Speaker.UserProfileId=null` precedent) — must respect D-157.

### Group G — Meeting requests with availability + approval + email (XL, reverses D-278 concept)
- **#11** `SpeakerMeetingRequest` (attendee→speaker, audit-only, no email) + `BusinessMeeting`
  + the SeatReservation approve/reject + bilingual-notification pattern exist. New: an
  availability/slot scheduler (none exists), VIP-only gate, delegation↔delegation type
  (depends on #10), and email-on-approval (`SendEmail=true`).

## Recommended sequencing
1. **Wave 1:** Group A. 2. Group C (#6+#7) → Group D (#8). 3. Group B (#4).
4. Group E (#9). 5. Group F (#10) → Group G (#11).

## Open clarifications
Name min-2 confirm; Saudi-ID "10xxxx" source; #4 city list; 24 invited countries;
delegations real-accounts vs profile-only; QR public-readable trade-off OK; final
registration message text (AR+EN); plate Absher-style dropdown UI.
