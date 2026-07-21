# E2E test catalogue — `My Contacts` / `Share my contact` (mobile, app API)

> **Authority:** SIMF-FDS-014 §5.4–5.7 (D-286 / Slice E; Flutter UI D-324).
> Visitor-to-visitor contact sharing. The **app API** has shipped + is tested
> (`tests/SIMF.Api.Tests/VisitorContactSharingTests.cs`, 9 cases); the **Flutter
> screens** (Share my contact / Scan / My Contacts) are **built** (D-324) under
> `lib/features/contacts/` and bind to these endpoints — see
> [`docs/App/FDS-014-Contact-UI/README.md`](../../App/FDS-014-Contact-UI/README.md).
>
> **D-737 (dual-purpose contact QR + unified scanner):** "Share my contact" now
> encodes a vCard with the share token embedded as a private `X-SIMF-TOKEN`
> property (`lib/features/contacts/data/share_qr_payload.dart`, RFC 6350 §6.10) —
> so a native phone camera still offers "add contact" AND the in-app scanner can
> resolve it (it used to always 404). The scan screen uses the shared
> `SimfScannerBody` (via `QrScanView`) with the single `ScanGate` dedupe and a
> camera-permission-denied error card. A foreign/old contact vCard that carries no
> token can't resolve to a live SIMF card, so it is offered straight to the
> phone's own address book via the OS "add contact" flow (D-744).

| | |
|--|--|
| **Surface** | Mobile app (Flutter) + app API (`/api/v1/app/*`) |
| **Endpoints** | `GET /app/account/share-token`, `POST /app/account/share-token/rotate`, `POST /app/contacts/resolve`, `POST /app/contacts/save`, `GET /app/contacts`, `DELETE /app/contacts/{id}`, `GET /app/contacts/{id}/vcard` |
| **Auth** | `RequireApprovedAccount` + app token (no CP / no permission code — self-service, matching Connection / SessionComment) |
| **Test runner** | xUnit + WebApplicationFactory (API today); Flutter integration test when the screens land |
| **Last reviewed** | 2026-06-04 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MMC-001 | Share my contact — the QR encodes the user's vCard (Arabic name + phones), readable by any phone camera; the gate QrId is never in it (D-470) | happy | P0 | authored ✓ (D-470) |
| E2E-MMC-002 | Rotate the share token — old code stops resolving | happy | P0 | authored |
| E2E-MMC-003 | Scan / resolve a token → live card preview | happy | P0 | authored |
| E2E-MMC-004 | Save a scanned contact (idempotent per subject) | happy | P0 | authored |
| E2E-MMC-005 | My Contacts list — cards resolved on read | happy | P1 | authored |
| E2E-MMC-006 | Remove a saved contact (soft-delete) | happy | P1 | authored |
| E2E-MMC-007 | Export a saved contact as a vCard (.vcf) | happy | P1 | authored |
| E2E-MMC-008 | Resolve an unknown / revoked token → 404 | error | P0 | authored |
| E2E-MMC-009 | Save your own token → 400 | error | P1 | authored |
| E2E-MMC-010 | Unauthenticated → 401 | auth | P0 | authored |
| E2E-MMC-011 | Subject deactivated after save → limited card | resilience | P2 | authored |
| E2E-MMC-012 | **D-737:** scan the app's OWN share QR (vCard + `X-SIMF-TOKEN`) → resolves + saves | happy | P0 | authored ✓ (`share_qr_payload_test` round-trip) |
| E2E-MMC-013 | Scan a foreign / old contact vCard (no token) → offered to save to the phone's contacts (D-744) | edge | P1 | authored ✓ (`scan_contact_screen_test` save-to-phone) |
| E2E-MMC-014 | Camera-permission-denied on the scanner → error card + manual entry still works | resilience | P1 | source-verified (`simf_scanner_body` error card; manual path in `simf_scanner_body_test`) |
| E2E-MMC-015 | **Bilingual job title (2026-07-20):** a resolved / saved contact whose subject has an Arabic job title shows it under the Arabic toggle (English fallback when absent, null when neither is set); `VisitorCard.jobTitleArabic` / `SavedContactRow.jobTitleArabic` localize like name + organisation | i18n | P1 | authored ✓ (`VisitorContactSharingTests` bilingual assert + `contact_models_test.localizedJobTitle`) |

## Scenarios

### E2E-MMC-001 — Share my contact

```gherkin
Feature: Visitor-to-visitor contact sharing (Slice E)
  As an approved visitor
  I want to share my contact by showing a QR
  So that another visitor can save me without exposing my gate QrId

Scenario: The visitor opens "Share my contact" (D-470 — vCard QR)
  Given an approved visitor with a completed profile
  When the screen loads
  Then the QR encodes the visitor's vCard from GET /app/account/contact-card.vcf
       (FN = Arabic name, TEL = Saudi + international mobile)
  And any phone's native camera can scan it and offer "Add to contacts" — no SIMF app needed
  And the gate QrId is NOT emitted into the vCard (a camera-readable QR must never leak the badge/lead key)
  And the screen still offers the OS share-intent .vcf and the rotate-token control
       (the share token continues to gate the in-app resolve/save flow in the scenarios below)
```

### E2E-MMC-002 — Rotate the share token

```gherkin
Scenario: Rotating invalidates the previously shared code
  Given the visitor has an active share token "OLD"
  When the app calls POST /app/account/share-token/rotate
  Then a new token "NEW" is returned (NEW != OLD)
  And resolving "OLD" now returns 404
  And resolving "NEW" returns the card
```

### E2E-MMC-003 — Scan / resolve a token

```gherkin
Scenario: A second visitor scans the QR and previews the card
  Given visitor B scanned visitor A's share token
  When the app calls POST /app/contacts/resolve { token }
  Then a card projected LIVE from A's UserProfile is returned:
       name (Ar/En), job title, organisation (from the Organisation lookup),
       Saudi + international mobile, country (from Country), and email
       (resolved cross-DB from Identity — OI-2)
  And NO photo is included (V1 — the encrypted ID document is never exposed)
```

### E2E-MMC-004 — Save a scanned contact

```gherkin
Scenario: Saving is idempotent per (owner, subject)
  Given visitor B resolved visitor A's token
  When B calls POST /app/contacts/save { token, note: "met at booth" }
  Then a SavedContact row is created and returned
  When B saves the same token again with note "updated"
  Then the SAME row id is returned and the note is refreshed (no duplicate)
  And no notification is sent to A (quiet by design; consent-by-action)
```

### E2E-MMC-005 — My Contacts list

```gherkin
Scenario: The list resolves each card on read (no stored PII snapshot)
  Given visitor B saved one or more contacts
  When B calls GET /app/contacts
  Then each row carries the subject's live name / job title / organisation + the note + savedAt
  And nothing of the subject's PII was persisted on the SavedContact row (D-157)
```

### E2E-MMC-006 — Remove a saved contact

```gherkin
Scenario: Removing soft-deletes and is idempotent
  Given visitor B has a saved contact with id X
  When B calls DELETE /app/contacts/{X}
  Then it returns 200 and X no longer appears in GET /app/contacts
  And calling DELETE /app/contacts/{X} again still returns 200 (idempotent)
  And another visitor cannot delete B's saved contact (404 — owner-scoped)
```

### E2E-MMC-007 — Export a saved contact as a vCard

```gherkin
Scenario: vCard 3.0 export
  Given visitor B has a saved contact with id X
  When B calls GET /app/contacts/{X}/vcard
  Then a "text/vcard" body is returned starting "BEGIN:VCARD" / "VERSION:3.0"
  And it carries FN / TITLE / ORG / EMAIL / TEL (Saudi + international mobile)
  And the client may equally build the vCard from the card DTO itself
```

### E2E-MMC-008 — Resolve an unknown / revoked token

```gherkin
Scenario: A bad code fails cleanly
  When the app calls POST /app/contacts/resolve with a non-existent or rotated-away token
  Then it returns 404 with a bilingual "share code not found or no longer valid" error
```

### E2E-MMC-009 — Save your own token

```gherkin
Scenario: You cannot save yourself
  When a visitor resolves + saves their OWN share token
  Then POST /app/contacts/save returns 400 VALIDATION_FAILED
```

### E2E-MMC-010 — Auth gate

```gherkin
Scenario: Unauthenticated access is denied
  When GET /app/account/share-token is called with no token
  Then it returns 401
```

### E2E-MMC-011 — Subject deactivated after save

```gherkin
Scenario: A saved contact whose subject is gone shows a limited card
  Given visitor B saved visitor A, then A's profile is removed / A is deactivated
  When B opens My Contacts (GET /app/contacts) or resolves the saved card
  Then the row resolves with Available = false and blank name fields
  And the app shows a limited / unavailable card (no crash)
```

### E2E-MMC-012 — Scan the app's own share QR → resolve + save (D-737)

```gherkin
Scenario: The in-app scanner reads the app's dual-purpose contact QR
  Given visitor A opened "Share my contact" — its QR is a vCard with the share
        token embedded as an X-SIMF-TOKEN property
  When visitor B scans that QR on the in-app contact scanner
  Then the scanner recognises the vCard, extracts the X-SIMF-TOKEN value,
       and calls POST /app/contacts/resolve with the extracted token (no 404)
  And the live contact card preview sheet is shown
  When B taps "save"
  Then POST /app/contacts/save stores the contact and a "saved" toast shows
```

**Evidence:** `share_qr_payload_test` — "reads the token back from a built payload
(round-trip)" + "injects the token just before END:VCARD"; `scan_contact_screen`
routes a vCard through `extractShareToken` → `resolve`. API resolve/save covered
by `VisitorContactSharingTests`.

### E2E-MMC-013 — Foreign / old vCard has no token → save to phone contacts

```gherkin
Scenario: A contact QR minted outside SIMF (or before D-737) carries no token
  Given a vCard QR with NO X-SIMF-TOKEN property (a native phone's contact, or an old QR)
  When visitor B scans it on the in-app contact scanner
  Then no resolve call is made and a bilingual confirm dialog offers to save it
       to the phone's own contacts ("حفظ في جهات اتصال الهاتف؟" / "Save to phone contacts?")
  And confirming hands the raw vCard to the OS "add contact" flow (the phone's
       Contacts app imports it and the user confirms — no WRITE_CONTACTS permission)
  And cancelling keeps the scanner open so a valid SIMF QR can be scanned instead
```

**Evidence:** `share_qr_payload_test` — "null for a foreign vCard with no token";
`scan_contact_screen_test` — "a plain vCard (no SIMF token) offers save-to-phone"
(no `resolve` call; the `Save to phone contacts?` dialog appears). The confirmed
path reuses the shared `shareTextContent` vCard export (D-744).

### E2E-MMC-014 — Camera-permission-denied → error card + manual entry

```gherkin
Scenario: A denied camera never traps the contact scanner
  Given the contact scanner opens with the camera enabled
  When the OS denies the camera permission (or the device has no camera)
  Then the shared error card shows
       "تعذّر تشغيل الكاميرا. فعّل إذن الكاميرا من إعدادات النظام، أو أدخل الرمز يدويًا بالأسفل." /
       "Camera unavailable. Enable camera permission in system settings, or type the code below."
  And a "إعادة المحاولة / Try again" retry control is offered
  And the always-visible manual field still drives resolve → preview → save
```

**Evidence:** source-verified — `simf_scanner_body.dart` `_CameraErrorCard` on a
controller error / the 8 s watchdog (device-only render); `simf_scanner_body_test`
covers the always-mounted manual field with the camera off.

## Implementation notes

- API coverage: `tests/SIMF.Api.Tests/VisitorContactSharingTests.cs` (9 xUnit cases
  covering MMC-001..010 at the API layer; MMC-011 is the `Available=false` path of
  the card projection).
- Data: `VisitorShareToken` + `SavedContact` on `SimfAppDbContext` (migration
  `D286_AddVisitorContactSharing`); both hold bare-Guid logical FKs to
  `SimfUser.Id` — no DB FK, no cross-DB join, no PII snapshot (D-157).
- The Flutter screens (Share my contact / Scan QR → preview → save / My Contacts)
  bind to these endpoints when built in the app page-by-page workflow.

---

_Last reviewed:_ 2026-07-20 by Claude — bilingual job title: `VisitorCard` /
`SavedContactRow` gained `jobTitleArabic`; the app contact + exhibitor cards
localize the title (Arabic primary in ar, English fallback), mirroring the
delegation head-title fix; E2E-MMC-015. Earlier:
_Last reviewed:_ 2026-07-11 by SIMF Team — D-744: a token-less foreign vCard
(MMC-013) is now offered straight to the phone's own contacts via the OS "add
contact" flow (reuses the shared `shareTextContent` vCard export) instead of
dead-ending. D-737: the Share-my-contact QR is a dual-purpose vCard carrying the
share token as an `X-SIMF-TOKEN` property, so the in-app scanner resolves the
app's own QR (MMC-012); the scan screen uses the shared `SimfScannerBody`
with a camera-denied error card (MMC-014). App coverage: `share_qr_payload_test`,
`simf_scanner_body_test`, `scan_gate_test`. Earlier: 2026-06-20 (D-470: the QR
encodes the user's vCard — Arabic name + phones — so any phone camera can add the
contact; the gate QrId is no longer emitted into the vCard; backend
`MyAreaDashboardTests.Contact_card_vcf_has_the_arabic_name_and_phones_and_omits_the_qr_id`,
app `share_my_contact_screen_test`); 2026-06-04 (D-286 API + D-324 screens).
