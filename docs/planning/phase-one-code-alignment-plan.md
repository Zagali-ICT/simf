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

---

# Part two. The security hardening the documents describe

**Read this first.** On 2026-09-02 the owner directed that the customer review
be answered in the documents now and reflected in the code after the customer
approves. SIMF-HLD-004 v1.4 and SIMF-LLD-003 v1.5 therefore describe the items
below **in the present tense, as the agreed design**. They are not in the code.

That is a deliberate choice, not an oversight, and this section is the only
record of the gap. Nothing here may be dropped without the owner saying so.

Items 4 to 8 are the ones a reviewer can test. Items 12 to 14 are infrastructure
rather than code and belong to whoever provisions the estate.

## Already true, and worth knowing before anyone "fixes" it

Three of the customer's findings are already answered by the build. Do not open
work for them:

- **Scheduled jobs do not run four times.** `WorkerLease` takes an exclusive
  `sp_getapplock` and `LeasedHostedService` runs each worker only while its node
  holds the lease, standing down when it is lost. Seventeen workers are wrapped.
- **The JWT signing key never leaves HSA.** `AddJwtBearer` appears only in
  `SIMF.Api`; the Control Panel is a BFF and the mobile edge is a pass-through
  proxy. Moving to RS256 is still worth doing, but not for the reason given.
- **The file store already rotates its key.** `FileStorageOptions` carries
  `KekVersion`, `PreviousEncryptionKey` and `PreviousKekVersion`.

## 4. RS256 in place of HS256

`src/Backend/SIMF.Api/Authentication/JwtBearerSetup.cs` pins
`SecurityAlgorithms.HmacSha256` and builds a `SymmetricSecurityKey` from
`Jwt:SigningKey`; `JwtTokenService` signs with the same. Move to an asymmetric
key pair: the API holds the private key, verifiers hold only the public key.
Keep the algorithm pinned, so `alg:none` and confusion attacks stay rejected.

**Verify.** A token signed with the old symmetric key is rejected. The public
key alone cannot mint a token.

## 5. Keys in a key management service, and a rotation path for the PII key

`StorageOptions.UserIdDocumentEncryptionKey` is a single key with no version and
no previous-key field, so the PII columns have no rotation path at all. Give it
the versioning the file store already has, then move both keys plus the JWT key
out of environment variables into the key service the ministry provides.

**Verify.** Rotate the PII key with rows encrypted under the old one; every row
still reads, and re-wrapping completes without downtime.

## 6. Mutual TLS between the presentation tier and the API

Nothing in `SIMF.ApiClient` or the API host requests or validates a client
certificate. Add it in both directions so reaching the API needs a credential
rather than a network position.

## 7. Hardware-bound mobile keys

The app stores the ES256 private key in `EncryptedSharedPreferences` and the iOS
Keychain. Generate it inside the Android Keystore (StrongBox where present) and
the iOS Secure Enclave instead, non-exportable, with user authentication
required at the key. The server contract does not change: it still receives a
SubjectPublicKeyInfo and verifies an ES256 signature.

## 8. Prompt sanitisation before the model

No sanitisation exists on the path into `OpenAiProvider`. Imported YouTube
transcripts and attendee question text reach the model as-is, so an instruction
hidden in a transcript can steer a summary. Add a sanitising layer with tests
carrying real injection strings.

## 9. Least-privilege database accounts

`src/Backend/SIMF.Api/Program.cs:617-618` runs `MigrateAsync()` on both contexts
at startup, so the runtime account holds DDL rights. Split it: migrations run in
the deployment step under an account with DDL, and the runtime account gets DML
only. This also removes a schema change from the request-serving hot path.

## 10. Audit trails that cannot be erased by the actor

`OperationLog` and `RowAudit` live in `SimfAppDbContext`, the database they
audit. Deny UPDATE and DELETE on both tables to the runtime account, and ship
every entry to the ministry log collector so a copy exists outside the database.

## 11. The polling budget

The documents state a 30-second interval, conditional requests and cache-served
304s, giving about 1,000 requests a second at the 30,000-attendee peak. Confirm
the app's actual interval, add response caching with ETag handling on the poll
endpoints, and prove the figure under load rather than asserting it.

## 12. A caching layer in front of MinIO

Session recordings are served through the API nodes as HTTP 206 ranges. Put a
cache in front of the store so a viewing burst consumes cache bandwidth instead
of API capacity. Depends on item 3.

## 13. Cross-database reconciliation

D-157 forbids cross-database foreign keys, so nothing at the database level
stops a business row outliving its identity owner. Add a scheduled
reconciliation that resolves each cross-database reference and reports the
orphans, under the same worker lease as every other job.

## 14. Infrastructure, not code

A second WAF/load balancer and a second API load balancer as active/passive
pairs; the mobile edge at two nodes and MinIO at two nodes, both sized as the
HLD's proposed minimums; staging on SQL Server Enterprise in an Availability
Group; and the disaster-recovery site with its RTO and RPO, which is still an
open item with the site.

---

## Fixed, not to be changed by this work

The two databases and their separation, the five network zones and two security
areas, the permission model, the EF schema (frozen under D-895), and the mobile
wire contract, which stays append-only.
