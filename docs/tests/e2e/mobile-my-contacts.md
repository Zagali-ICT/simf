# E2E test catalogue — `My Contacts` / `Share my contact` (mobile, app API)

> **Authority:** SIMF-FDS-014 §5.4–5.7 (D-286 / Slice E). Visitor-to-visitor
> contact sharing. The **app API** has shipped + is tested
> (`tests/SIMF.Api.Tests/VisitorContactSharingTests.cs`, 9 cases); the **Flutter
> screens** (Share my contact / Scan / My Contacts) are built later in the app
> page-by-page workflow and bind to these endpoints.

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
| E2E-MMC-001 | Share my contact — token minted + shown as QR | happy | P0 | authored |
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

## Scenarios

### E2E-MMC-001 — Share my contact

```gherkin
Feature: Visitor-to-visitor contact sharing (Slice E)
  As an approved visitor
  I want to share my contact by showing a QR
  So that another visitor can save me without exposing my gate QrId

Scenario: The visitor opens "Share my contact"
  Given an approved visitor with a completed profile
  When the app calls GET /app/account/share-token
  Then a stable Crockford-base32 token is returned (minted on first call)
  And calling it again returns the SAME token (idempotent)
  And the app renders the token as a QR + offers an OS share-intent vCard
  And the token is SEPARATE from the visitor's entry QrId (scanning at the gate never harvests the card)
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

_Last reviewed:_ 2026-06-04 by SIMF Team.
