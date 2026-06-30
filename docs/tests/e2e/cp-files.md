# E2E test catalogue — `Centralized file store` (`/api/v1/files`)

> **Authority:** SIMF E2E test catalogue (D-133 slice 7) · feature **D-568**.
> The centralized file subsystem is **API-surface today** — one upload, one
> download-by-GUID, one delete. The CP `SimfFileUpload` component + a Media/Files
> management page land in slice 0.8; this catalogue grows a CP section then. The
> executable form of every scenario below already exists as xUnit integration
> tests (`tests/SIMF.Api.Tests/Files/`).

| | |
|--|--|
| **Page** | _(API only today; CP component in D-568 slice 0.8)_ |
| **Route** | `POST /api/v1/files` · `GET /api/v1/files/{id}` · `DELETE /api/v1/files/{id}` |
| **Surface** | Admin API |
| **Test runner** | xUnit + `WebApplicationFactory` (`SIMF.Api.Tests/Files/`) — Chrome DevTools MCP once the CP page ships |
| **Auth setup** | Upload/Delete: an Administrator (holds `Files.Upload` / `Files.Delete`). Download: anonymous at the route; authorized **in code** off the file's own `FileService` policy. |
| **Last reviewed** | 2026-06-30 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-FILE-001 | Upload a public image → anonymous download streams the bytes | happy | P0 | ✅ automated (`FilesEndpointsTests`) |
| E2E-FILE-002 | Upload an encrypted avatar → owner/admin download decrypts (envelope round-trip) | happy | P0 | ✅ automated |
| E2E-FILE-003 | Download authz matrix — owner-or-admin (avatar) | auth/IDOR | P0 | ✅ automated (`FileAuthorizationTests`) |
| E2E-FILE-004 | Download authz matrix — admin-only (VIP photo) | auth/IDOR | P0 | ✅ automated |
| E2E-FILE-005 | Download authz matrix — any authenticated (speaker presentation) | auth | P1 | ✅ automated |
| E2E-FILE-006 | Guessed GUID for a private file → uniform 404 (no oracle) | security | P0 | ✅ automated |
| E2E-FILE-007 | Wrong/unrecognized payload or wrong type for the service → 400 | error | P1 | ✅ automated |
| E2E-FILE-008 | Owner-required service without an owner → 400 | error | P1 | ✅ automated |
| E2E-FILE-009 | Upload auth gate — anonymous 401 / non-admin 403 | auth | P0 | ✅ automated |
| E2E-FILE-010 | Delete soft-deletes → later download 404 | happy | P1 | ✅ automated |
| E2E-FILE-011 | Retention hold — `IdDocument` delete → 409, still downloadable | error | P1 | ✅ automated |
| E2E-FILE-012 | Infected upload rejected; unscannable upload fail-closed in Production | security | P1 | _unit (`ClamAvResponseParsingTests`) + manual w/ clamd_ |

## Scenarios

### E2E-FILE-001 — Upload a public image, anonymous download streams it

```gherkin
Feature: one upload + one download-by-GUID
Background:
  Given an Administrator holding Files.Upload is signed in

Scenario: public image is served to anyone by GUID
  When the admin POSTs a PNG to /api/v1/files with Service=SpeakerPhoto, OwnerEntityType=Speaker, OwnerEntityId=<guid>
  Then the response is 200 with { id, url = "/api/v1/files/<id>", fileType = Image, isEncrypted = false }
  When an anonymous client GETs that url
  Then the response is 200 and the body equals the uploaded PNG bytes
  And the response carries X-Content-Type-Options: nosniff and a public cache header
```

### E2E-FILE-002 — Encrypted avatar round-trips through the cipher

```gherkin
Scenario: an avatar is encrypted at rest and decrypts on the way out
  Given the admin uploads a PNG with Service=Avatar, OwnerEntityType=UserProfile, OwnerEntityId=<ownerId>
  Then the response is 200 with isEncrypted = true
  And the bytes on disk are an AES-256-GCM envelope blob (not the plaintext)
  When the admin GETs the file url
  Then the response is 200 and the body equals the original PNG (the DEK was unwrapped and the body decrypted)
  When an anonymous client GETs the url
  Then the response is 404 (private file, no oracle)
```

### E2E-FILE-003 — Owner-or-admin matrix (avatar / ID document)

```gherkin
Scenario: only the owner or an admin may read an owner-scoped file
  Given the admin stored an Avatar owned by visitor V (OwnerEntityId == V's identity == V's JWT sub)
  Then V (the owner) GET → 200
  And an Administrator GET → 200
  And a different signed-in visitor GET → 404
  And an anonymous GET → 404
```

### E2E-FILE-004 — Admin-only matrix (VIP photo)

```gherkin
Scenario: a VIP photo is downloadable only by an admin with the gate permission
  Given the admin stored a VipPhoto
  Then an Administrator GET → 200
  And a signed-in visitor GET → 404
  And an anonymous GET → 404
```

### E2E-FILE-005 — Authenticated matrix (speaker presentation)

```gherkin
Scenario: a presentation is readable by any signed-in account but not anonymously
  Given the admin stored a SpeakerPresentation (a PDF, encrypted, Internal)
  Then any signed-in visitor GET → 200
  And an anonymous GET → 404
```

### E2E-FILE-006 — Guessed GUID for a private file → uniform 404

```gherkin
Scenario: IDOR is closed — authorization is off the file's service, not the URL
  Given a private file's GUID has leaked
  When an unauthorized caller GETs /api/v1/files/<that-guid>
  Then the response is 404 — identical to a non-existent id (no exists-but-forbidden signal)
  And an OperationLog FileAccessDenied row is written
```

### E2E-FILE-007 — Bad payload / wrong type → 400

```gherkin
Scenario: the magic bytes, not the client content-type, decide the type
  When the admin uploads "not-an-image" bytes to Service=SpeakerPhoto
  Then the response is 400 (unrecognized payload)
  When the admin uploads a %PDF to Service=SpeakerPhoto (image-only)
  Then the response is 400 (type not in the service allow-list)
```

### E2E-FILE-008 — Owner-required without an owner → 400

```gherkin
Scenario: an owner-scoped service rejects an upload with no owner
  When the admin uploads to Service=Avatar with no OwnerEntityId
  Then the response is 400 ("this file category requires an owner")
```

### E2E-FILE-009 — Upload auth gate

```gherkin
Scenario: only an admin holding Files.Upload may upload
  When an anonymous client POSTs to /api/v1/files
  Then the response is 401
  When a signed-in visitor (no Files.Upload) POSTs to /api/v1/files
  Then the response is 403
```

### E2E-FILE-010 — Delete soft-deletes

```gherkin
Scenario: deleting a deletable file hides it
  Given the admin stored a SpeakerPhoto
  When the admin DELETEs /api/v1/files/<id>
  Then the response is 200
  And a later GET of the file url → 404
```

### E2E-FILE-011 — Retention hold (ID document)

```gherkin
Scenario: an ID document is under a retention hold and cannot be deleted
  Given the admin stored an IdDocument (DeletableDefault = false)
  When the admin DELETEs it
  Then the response is 409 ("under a retention hold")
  And the admin can still GET it → 200
```

### E2E-FILE-012 — Malware scanning (fail-closed)

```gherkin
Scenario: an infected upload is rejected; an unscannable upload is rejected in Production
  Given the upload scanner returns Infected for an EICAR/known signature
  When any file with that signature is uploaded
  Then the response is 400 UPLOAD_MALWARE_DETECTED
  Given the ClamAV daemon is unreachable (verdict Skipped) and the environment is Production
  Then the upload is rejected 409 UPLOAD_SCAN_UNAVAILABLE (fail-closed, D-494) rather than stored unscanned
```

**Evidence captured (automated):**
- xUnit run: `dotnet test --filter "FullyQualifiedName~SIMF.Api.Tests.Files"` → **55/55** on a Region-consistent base.
- Audit rows: `OperationLog` `FileUploaded` / `FileDownloaded` (non-public reads) / `FileAccessDenied` / `FileDeleted`.

---

_Last reviewed:_ 2026-06-30 by the D-568 build.
