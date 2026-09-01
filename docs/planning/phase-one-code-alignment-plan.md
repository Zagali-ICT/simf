# Plan A. Make the code match the phase one architecture

The architecture is fixed by the customer requirement of 2026-08-30 and by
Figure 1 of SIMF-HLD-004 v1.4. This plan changes only where the platform sends
four calls. It changes no zone, no database, no schema and no contract.

Four moves. Three need work.

| # | Move | Today | Work |
|---|---|---|---|
| 0 | YouTube caption fetch runs from the Control Panel | Runs in the API host | Code |
| 1 | AI runs on the on-site GPT OSS 120B | Points at cloud by configuration | Configuration |
| 2 | Mail goes to the on-site relay | Already supported | Configuration |
| 3 | Files go to MinIO over S3 | Filesystem only | Code |

---

## 0. Move the YouTube caption fetch to the Control Panel

**Do this first. The feature breaks the day the firewall rule is applied.**

`FetchSessionSubtitleEndpoint` in
`src/Backend/SIMF.Api/Endpoints/Admin/SessionSubtitleEndpoints.cs` calls
`IYoutubeTranscriptService`, registered at
`src/Backend/SIMF.Infrastructure/DependencyInjection.cs:750`. The API is in HSA,
which phase one gives no outbound route, so every call to this endpoint will
fail once the egress rule matches the diagram.

**Change.** Move the outbound fetch into the Control Panel, which is in SSA and
has internet access. The Control Panel fetches the caption track and posts the
text to the API. The API keeps the endpoints that store and review a transcript
and loses the one that reaches YouTube.

Move `YoutubeTranscriptService` with its SSRF hardening unchanged: it
re-validates the caption host before the second request and uses an HTTP client
that does not follow redirects.

The Control Panel is a BFF with no catch-all proxy. A new route needs both a
method on the typed client in `src/Shared/SIMF.ApiClient` and an explicit
mapping in the Control Panel endpoints, or it returns 404 with nothing logged.

**Verify.** Block outbound HTTPS from the API host, allow it from the Control
Panel host, then import a subtitle from the Control Panel. It succeeds.

---

## 1. Point the AI at the on-site GPT OSS 120B endpoint

`OpenAiProvider` (`src/Backend/SIMF.Infrastructure/Ai/OpenAiProvider.cs`) already
speaks the OpenAI-compatible protocol the LLM server serves, and
`AiOptions.OpenAi.BaseUrl` is a bound configuration value. No code change.

`src/Backend/SIMF.Api/appsettings.json` ships `"DefaultProvider": "Echo"`, which
is the offline stub, with cloud base URLs and empty keys.

**Change.** In `deploy/set-env-api.ps1`, set:

- `Ai:DefaultProvider` to `OpenAi`
- `Ai:OpenAi:BaseUrl` to the on-site LLM endpoint
- `Ai:OpenAi:DefaultModel` to the model id SITE serves
- `Ai:OpenAi:ApiKey` to the credential SITE requires, or leave empty

**Verify.** With outbound access blocked from the API host, the AI assistant
answers. A packet capture on that host during an AI request shows traffic to the
LLM host only.

---

## 2. Point mail at the on-site relay

`EmailOptions` (`src/Shared/SIMF.Common/Options/EmailOptions.cs`) binds `Host`,
`Port` defaulting to 587, `User`, `Password` and `FromAddress`.
`SmtpEmailSender` (`src/Backend/SIMF.Infrastructure/Email/SmtpEmailSender.cs:184`)
authenticates only when `User` is non-empty, and selects STARTTLS for port 587.
An unauthenticated on-site relay works with no code change.

**Change.** In `deploy/set-env-api.ps1`, set `Email:Host` to the relay and leave
`Email:User` empty if the relay does not authenticate.

**Verify.** A verification e-mail arrives, and the relay log shows the connection
came from the API host.

---

## 3. Store files in MinIO

`IFileStorageProvider`
(`src/Backend/SIMF.Application/Files/Abstractions/IFileStorageProvider.cs`) has
eight members: `WriteAsync`, `ReadAsync`, `WriteStreamAsync`, `OpenReadAsync`,
`ExistsAsync`, `DeleteAsync`, `SecureEraseAsync`, and the `StorageKey` the write
methods return. The only implementation is
`src/Backend/SIMF.Infrastructure/Files/FilesystemFileStorageProvider.cs`. No S3
or MinIO package is referenced by any csproj.

The interface addresses content by an opaque `StorageKey`, not by a path, and
encryption happens above it in `AesGcmEnvelopeCipher`. The provider therefore
carries the same ciphertext bytes either way, and nothing above it changes.

**Steps.**

1. Reference an S3 client. `AWSSDK.S3` reaches MinIO with a custom `ServiceURL`
   and path-style addressing. The `Minio` package is the alternative.
2. Add `MinioFileStorageProvider : IFileStorageProvider`. One bucket. Reuse the
   existing `StorageKey` as the object key.
3. Add a typed options class for endpoint, bucket, credentials and TLS, bound
   from a `FileStorage` section, and a `FileStorage:Provider` key that selects
   filesystem or MinIO. Keep the filesystem provider for development.
4. Decide `SecureEraseAsync`. The filesystem provider overwrites bytes in place;
   object storage cannot. The S3 equivalent is a delete against a bucket with
   versioning off and no lifecycle retention. Confirm this satisfies the NCA
   position before writing the provider, because it cannot be retrofitted.
5. Copy existing objects from `FileStorage:RootPath` into the bucket under the
   same keys. Verify each by SHA-256, not by count.
6. Tests: a round trip per `FileService` value, a range read on the session
   recording path because HTTP 206 needs a seekable stream, a delete, and the
   behaviour chosen in step 4.

**Verify.** With the file share unmounted: an avatar uploads, an identity
document downloads and decrypts, and a session recording seeks to the middle and
plays.

---

## Order

0, then 2, then 1, then 3. Item 0 leads because it is the only one that turns a
working feature into a broken one, and it breaks on a firewall change rather
than on a code change.

## Fixed, not to be changed by this work

The two databases and their separation, the five network zones and two security
areas, the permission model, the EF schema (frozen under D-895), and the mobile
wire contract, which stays append-only.
