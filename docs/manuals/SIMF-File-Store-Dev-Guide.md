# SIMF Centralized File Store — Developer Guide

> **Status:** as-built reference for the D-568 centralized file subsystem (Wave C
> cutover, D-620…D-628). Companion to the E2E catalogue
> [`docs/tests/e2e/cp-files.md`](../tests/e2e/cp-files.md) and the decision log
> (D-568, D-625). This documents what shipped; it does not restate the controlled
> docs. Where it disagrees with the code, the code wins — fix this doc.

## 1. Why it exists

Before D-568 the system had **seven bespoke file stores** (avatar, ID document,
VIP photo, speaker presentation, session recording, media image, and the unified
`Asset` pipeline) each with its own table, storage path, and ad-hoc access rules.
D-568 replaced all of them with **one table (`StoredFile`), one upload endpoint,
one download-by-GUID endpoint, and one policy registry**. Every per-file
protection (who can read it, whether it is encrypted, what may be uploaded, how it
is disposed) is derived from a single dimension — the file's **`FileService`** —
never from the URL or the caller's claims about it.

`StoredFile` lives on `SimfAppDbContext` (the App DB). Cross-DB rule (D-157) still
holds: a file that belongs to an Identity-owned entity (avatar, ID document) keys
off a bare `Guid` (the `SimfUser.Id`), resolved on read — no cross-DB FK.

## 2. The model

A `StoredFile` row carries: `Id` (the public GUID), `Service` (`FileService`),
`Tier` (`FileSensitivityTier`, persisted so classification is auditable, not
inferred), `OwnerEntityType` + `OwnerEntityId` (the polymorphic owner), `FileType`,
`ContentType`, `SizeBytes`, `Sha256`, `StorageKey`, `IsEncrypted`,
`CipherFormatVersion` + `KekVersion` (the crypto stamps, see §6.1),
`IsDeletable`, `RetainUntil`, plus the audit columns. External-link files carry a
validated `https` URL instead of stored bytes (the download endpoint
302-redirects to it).

Three enums drive everything (all `int`, **append-only** — never rename/reorder;
the D-110 enum-stability rule survives the D-568 freeze-lift):

- **`FileService`** (`SIMF.Common.Enums.FileService`) — 16 business categories, the
  one dimension that resolves to a policy. See the table in §3.
- **`FileSensitivityTier`** — `Public(0)`, `Internal(1)`, `Confidential(2)`,
  `Secret(3)`. Drives the download cache header and the classification register.
- **`FileAccessClass`** — `Public(0)`, `Authenticated(1)`, `Admin(2)`,
  `OwnerOrAdmin(3)`. Who may download.

## 3. The policy registry (source of truth)

`SIMF.Common.Files.FileServicePolicies` maps **every** `FileService` to an
immutable `FileServicePolicy`. `Resolve(service)` **hard-fails on an unmapped
service (default-deny)**, and `FileServicePolicyTests` asserts every enum value is
mapped — a new `FileService` cannot ship without a reviewed access decision. This
registry is also the file/PII classification register (SAMA A1-3 / NCA ECC 2-7-2).

| FileService | Tier | Access | Encrypt@rest | Allowed types | Owner entity | Owner req. | Deletable |
|---|---|---|---|---|---|---|---|
| `Avatar` | Confidential | OwnerOrAdmin (`Visitors.View`) | **yes** | Image | UserProfile | yes | yes |
| `IdDocument` | **Secret** | OwnerOrAdmin (`Visitors.View`) | **yes** | Image, Pdf | UserProfile | yes | **no** |
| `VipPhoto` | Confidential | Admin (`Visitors.View`) | **yes** | Image | UserProfile | yes | yes |
| `SpeakerPresentation` | Internal | Authenticated | **yes** | Pdf, Document | SpeakerPresentation | no | yes |
| `SessionRecording` | Internal | Authenticated | **no** ¹ | Video | Session | no | yes |
| `MediaGalleryImage` | Public | Public | no | Image | MediaItem | no | yes |
| `SpeakerPhoto` | Public | Public | no | Image | Speaker | no | yes |
| `NewsImage` | Public | Public | no | Image | News | no | yes |
| `SponsorLogo` | Public | Public | no | Image | Sponsor | no | yes |
| `MediaPartnerLogo` | Public | Public | no | Image | MediaPartner | no | yes |
| `CompanyLogo` | Public | Public | no | Image | Contact | no | yes |
| `OrganizationLogo` | Public | Public | no | Image | OrganizationProfile | no | yes |
| `ArchiveCover` | Public | Public | no | Image | ArchiveEdition | no | yes |
| `ProgrammeDayImage` | Public | Public | no | Image | ProgrammeDay | no | yes |
| `Banner` | Public | Public | no | Image | Banner | no | yes |
| `BoothLogo` | Public | Public | no | Image | Booth | no | yes |
| `OrganizationHeroVideo` | Public | Public | **no** ¹ | Video | OrganizationProfile | no | yes |
| `SessionLiveStream` | Public | Public | n/a ² | Video | Session | no | yes |
| `SessionSignLanguage` | Public | Public | n/a ² | Video | Session | no | yes |
| `SessionSummaryVideo` | Public | Public | n/a ² | Video | Session | no | yes |
| `MediaGalleryVideo` | Public | Public | n/a ² | Video | MediaItem | no | yes |
| `OrganizationLiveStream` | Public | Public | n/a ² | Video | OrganizationProfile | no | yes |

² **The five feed services store no bytes at all.** They are always
`ExternalLink` rows: SIMF does not host a broadcast, it points at one. Encryption
is therefore moot, and the URL is served to the client **verbatim rather than
through the 302** every other external link uses. That is not an inconsistency, it
is the requirement: both clients decide *how* to play a feed by inspecting the
string - the player extracts a YouTube id and branches on it, the hero refuses to
mount unless the last path segment is `.mp4`/`.m3u8` - so an indirected URL is
loadable and still wrong. `IFeedLinkService` is the only way to write or read one.

An external link for these services is validated against `LiveStreamUrlPolicy`;
an external link for an **image** service deliberately is not, because an image
URL is never read by the client (the endpoint 302s and the browser follows), and
applying the video rule there would reject every CDN logo and the seeded
placeholders. And no service outside Public tier + Public access may be linked at
all - a private file must never become a pointer at somebody else's server.

¹ **`SessionRecording` is deliberately plaintext** (D-568 Wave C S7 / D-625): a
conference recording is Internal-tier (not PII) and Range/seek streaming (HTTP 206)
needs a **seekable** file — AES-GCM is not seekable. This matches the posture of
the legacy plaintext recording store it replaced (no security regression) and it is
streamed to disk, never buffered whole.

`Retention` is `null` (indefinite) for every service today — the concrete schedule
is an open owner decision (D-568 #7).

## 4. Endpoints (`/api/v1`)

| Verb | Route | Auth | Notes |
|---|---|---|---|
| POST | `/files` | `Files.Upload` + Approved | Multipart. `Service` + `OwnerEntityId` ride the form; the **owner family is forced from the policy** (P2) and the owner id is server-derived for owner-scoped self-service uploads — a caller cannot over-post a mismatched owner. Scanned (fail-closed in Production), validated, encrypted-per-policy, stored. |
| POST | `/files/link` | `Files.Upload` + Approved | Record an external `https` image link (logos/covers hosted elsewhere); owner-upsert. |
| GET | `/files/{id:guid}` | **anonymous route** | Authorization is resolved **in code from the file's own policy**, never the URL — a guessed/leaked GUID for a private file returns a uniform **404**. Public images serve `inline`; everything else (and every private file) serves as an `attachment` (defeats stored-XSS). Cache header per tier (`public,max-age=300` / `private,max-age=60` / `no-store`). External-link files 302-redirect. |
| DELETE | `/files/{id:guid}` | `Files.Delete` + Approved | Soft-delete; **409** when under a retention hold. |
| DELETE | `/files/{id:guid}/force` | `Files.ForceDelete` + Approved | PDPL right-to-erasure — securely destroys bytes even under a hold. Held as a separate, independently-grantable, audited permission. |

## 5. How the app + CP actually reach files

**Clients do NOT call `/files/{guid}` directly.** Every app/website/CP surface
keeps its **stable, per-surface public route**, which internally resolves to a
`StoredFile` (D-568 invariant — app routes + JSON keys stay stable, D-219):

- `GET /app/assets/{category}/{ownerId}/image` — public images (speaker photo,
  sponsor/partner/company/org logo, news, archive cover, programme-day, banner,
  booth). `category` = the owner family; `hasPhotoAsset` on the parent DTO says
  whether a stored file exists (else the client uses its fallback).
- `GET /app/media/{id}/{image,thumbnail}` — media gallery.
- `GET /app/account/avatar/{userId}` · `GET /app/account/user-profile/id-image` —
  owner-scoped (avatar, ID document); ID reads are `no-store` + audited.
- `GET /app/presentations/{id}/file` — speaker presentation (Authenticated).
- Session recording — token + Range-streamed (HTTP 206).

The download-by-GUID endpoint is the **internal primitive**; the stable routes are
thin front-doors over it. Wire JSON keys (`avatarUrl`, `imageUrl`/`thumbnailUrl`,
`hasPhotoAsset`, presentation `fileName`/`contentType`/`sizeBytes`) are preserved.
`ArchivePastSpeaker.photoRelativePath` **has been migrated** (D-891). It was
kept out of the store on the grounds that it was an external-URL datum the app
renders directly, but the real reason it could not move was the write path: the
archive's children were replaced wholesale on every save, so a file owned by a
child id was orphaned immediately. Those lists reconcile by id now, the photo is
an uploaded file like every other, and the wire key keeps its name while carrying
an absolute URL — which is what the app needs, since it tests the string with
`isHttpUrl` before it will load anything.

The Flutter app fetches bytes through its **authenticated Dio client** (bearer +
self-signed-TLS handling), never a bare `Image.network` (D-422).

## 6. Storage, encryption & scanning

- **`FilesystemFileStorageProvider`** — bytes on disk under the configured root,
  keyed by `StorageKey`. Streaming: multipart → temp file with incremental
  SHA-256; seekable `OpenReadAsync` for Range video.
- **`AesGcmEnvelopeCipher`** — per-file DEK wrapped by a master **KEK** for every
  `EncryptAtRest:true` service. The KEK is a boot-fail-fast config value in
  Production (`FileStorage` options) — the API will not start without it.
- **`ClamAvUploadScanner`** — malware scan on upload; **fail-closed in Production**
  (`UploadScanningOptions.FailClosed`), pass-through in dev/test.
- **Fail-closed SHA-256** (P4): a hash mismatch on read audits + refuses — tampered
  bytes are never served.
- **Arabic filenames** (P1): `Content-Disposition` carries an RFC 5987
  `filename*=UTF-8''…` plus an ASCII fallback.

### 6.1 `KekVersion` - which key sealed this file

Every encrypted blob's header carries two bytes before the crypto: the format
version and the **KEK version** that wrapped that file's data key. `StoredFile`
mirrors both, as `CipherFormatVersion` and `KekVersion`.

**What the column records.** The KEK version copied **from the cipher at write
time** (`IFileCipher.ActiveKekVersion`), not from configuration. That
distinction is the point of the property: configuration can be reloaded, so a
value re-read later might not be the one that actually did the wrapping. The
number in the column and the number in the blob header are the same number, by
construction.

**Why mirror it at all**, given the header already has it: a header is readable
only one file at a time. Without the column, "how much of the store is still on
key 1" can only be answered by walking every blob on disk. With it, that is one
query.

**What `NULL` means.** Either **no KEK applies** (the row is plaintext or an
external link, and the `NULL` is correct and permanent) or the row **predates
the column**, in which case its blob is wrapped under a key nobody recorded. The
two are told apart by `IsEncrypted`. The second case must be treated as
**unknown and therefore due for re-wrapping**, never as "probably current":
assuming current is exactly how a file gets skipped and then stranded when the
old key is retired.

**The filtered index makes the inventory a `GROUP BY`.** `StoredFileConfiguration`
indexes `KekVersion` with `HasFilter("[IsEncrypted] = 1")`. The filter keeps
plaintext rows out, since they have no KEK and would only dilute the count, and
it deliberately does **not** exclude nulls, because an encrypted row with no
recorded version is precisely what a re-wrap has to find. The index is
provisioned ahead of that worker, on the same reasoning as the `RetainUntil`
one.

**Designed, not operational.** Nothing rotates today. The blob format has always
supported it and `FileStorageOptions` will hold a `PreviousEncryptionKey` /
`PreviousKekVersion` pair through a rotation window, but **the re-wrap pass that
finishes a rotation does not exist**; the owner has deferred rotation. So a
rotation can be started and never completed, and the previous key can never be
retired. Treat both data keys as set-once for the life of the store. The
inventory query, the re-wrap design, and the completion criterion are in
`SIMF-OPS-001` §C.6 and §C.7. Read those before touching anything key-related,
and note that the one irreversible mistake available is clearing the previous
key too early.

## 7. Adding a new file-backed surface

1. Add a `FileService` value (next free int; append-only) with an XML-doc line.
2. Add its `FileServicePolicy` to `FileServicePolicies.Map` (tier, access, encrypt,
   allowed types, owner family, owner-required, deletable) — the guard test fails
   the build until you do.
3. Add the `Files.*` permission use if it introduces a new admin read gate.
4. Wire the parent entity's stable public route to `IFileService` (upload on the
   CP write path, serve on the app read path); keep the JSON keys stable.
5. Docs + E2E (`cp-files.md`) + tests in the same changeset (D-246).

## 8. Operational note — post-squash deploy resets all files

`StoredFile` rows live in `SIMF_App`. Deploying the squashed `main` **drops both
databases** (the documented DEPLOY-CRITICAL step) → recreate from baselines +
seeders. **Seeders restore only reference/demo data**, and seeded speaker/sponsor
images are mostly absent: only the media-partner seed writes URLs (three
`placehold.co` links), while the sponsor, speaker, news and archive seeds pass
`NULL` and expect an upload through the CP. The speaker-photo seed is the one
that does it properly, inserting real `StoredFiles` rows whose bytes ship with it.

So after a fresh deploy
there are **no uploaded files** and every StoredFile-backed asset serve returns
**404** until the event content is re-entered and files re-uploaded through the CP.
This is expected (data is disposable), **not** a store bug — the store correctly
404s missing files and 200s real ones. (Diagnosed 2026-07-07 against the live API;
see the decision log.) The Wave C P6/E2 follow-up this section used to list as
optional polish — seed `StoredFile` **ExternalLink** rows for the placeholder
logos instead of raw `*RelativePath` URLs — is **done** (2026-08-14). It stopped
being optional once the pointer columns started becoming typed keys: a
`uniqueidentifier` cannot hold a URL, so the media-partner seed would have failed
on its first run.

---

_Authored 2026-07-07 (SIMF Team). Source of truth: `FileService.cs`,
`FileSensitivityTier.cs`, `FileServicePolicy.cs`, `FileEndpoints.cs`,
`StoredFileService.cs`, `FilesystemFileStorageProvider.cs`._
