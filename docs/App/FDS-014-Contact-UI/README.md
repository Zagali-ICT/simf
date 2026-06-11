# App — FDS-014 Visitor Contact Sharing (Flutter UI)

| Field | Value |
|-------|-------|
| Feature | Visitor-to-visitor contact sharing (Track 2) |
| Authority | [`SIMF-FDS-014`](../../SIMF-FDS-014-Contacts-and-Sharing.md) §5.4–5.7 |
| Backend | Shipped D-286 (`VisitorShareService` + `/app/*` endpoints + vCard); 9 xUnit cases |
| Flutter UI | **Built D-324** — `src/Mobile/simf_app/lib/features/contacts/` |
| Audience | App — **Visitor (Approved)**; no permission code (self-service, like `Connection`) |
| Wire | `ApiResult<T>` envelope; fields camelCase, **enum-free**; append-only (D-219) |
| E2E | [`docs/tests/e2e/mobile-my-contacts.md`](../../tests/e2e/mobile-my-contacts.md) (E2E-MMC-001..011) |

This feature is **additive** to the 41-screen mockup spine (it is not a numbered
mockup screen). It is reached from **More** (`/more`) and gates behind an
Approved account (router sentinel numbers 100–102, all in `_authenticatedRoutes`).
The UI is interim; final visuals come from SIMF-VID-001.

## Screens

| Route | Name | Endpoint(s) | Function |
|-------|------|-------------|----------|
| `/contacts/share` | `shareMyContact` | `GET /app/account/share-token`, `POST …/rotate`, `GET /app/account/contact-card.vcf` | Mint + show the caller's dedicated share token as a QR; rotate it (old code stops resolving); hand off an OS share-intent vCard (reuses the My-Area export — one vCard source). |
| `/contacts/scan` | `scanContact` | `POST /app/contacts/resolve`, `POST /app/contacts/save` | Camera scan (`mobile_scanner`) **or** manual code entry → resolve to a live card → preview → save to My Contacts (idempotent per subject). |
| `/contacts` | `myContacts` | `GET /app/contacts`, `DELETE /app/contacts/{id}`, `GET /app/contacts/{id}/vcard` | List saved cards (resolved on read — no PII snapshot); detail sheet exports the vCard or removes (soft-delete); app-bar action opens the scanner. |

## Files

- `data/contact_models.dart` — `VisitorShareToken`, `VisitorCard`, `SavedContactRow` (tolerant decode + localized name/org/country helpers).
- `data/contacts_repository.dart` (+`contactsRepositoryProvider`) — wraps the 7 endpoints; reuses `SimfApiClient.getText` for the vCard export.
- `widgets/contact_card.dart` — shared read-only card (initials avatar, name, job title, channel rows for org / country / email / Saudi + international mobile, optional note; an unavailable subject shows only the unavailable note).
- `share_my_contact_screen.dart`, `scan_contact_screen.dart`, `my_contacts_screen.dart`.

## Logic notes

- **L-1 (share token ≠ entry QR).** The card QR encodes the **dedicated** share
  token, never the entry `QrId` (FDS-014 Q3), so a gate scan never harvests the card.
- **L-2 (live projection).** A resolved/saved card is projected **live** from the
  subject's `UserProfile` (+ Organisation/Country + a permitted email round-trip);
  nothing of the subject's PII is stored on the saved row (D-157). No photo in V1.
- **L-3 (unavailable subject).** When `available` / `subjectAvailable` is false
  (subject deactivated / no profile) the card shows the unavailable note and hides
  the channels and the save/export actions (E2E-MMC-011).
- **L-4 (errors).** `resolve` 404 → "code not found or no longer valid";
  `save` 400 → "you can't save your own card"; other wire errors → a generic
  bilingual toast. List/share-token failures show error + retry.
- **L-5 (scan capture).** Real camera scan via `mobile_scanner` **plus** a manual
  code-entry fallback (works when the camera is denied and is the path the widget
  tests drive). Camera permission lands in the generated `android/ios` at simf-run
  (same pattern as `local_auth` / `image_picker` / `video_player`).

## Tests

`test/features/contacts/` — `share_my_contact_screen_test.dart`,
`scan_contact_screen_test.dart`, `my_contacts_screen_test.dart`,
`contact_card_test.dart`, and the shared `_fake_contacts_repo.dart`. The scanner
screen is driven with `enableCamera: false` so the resolve→preview→save path is
exercised without the native plugin. API-layer coverage is
`tests/SIMF.Api.Tests/VisitorContactSharingTests.cs` (D-286).
