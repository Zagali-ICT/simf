# Plan A. Bring the code to the phase one architecture

Owner requirement of 2026-08-30. Status 2026-08-31: the documents and the three
diagrams describe phase one; the code implements one of its four moves. This
plan closes that gap.

Nothing below is taken from a document. Every "current state" line names the
file it was read from, because the defect this exercise uncovered is that the
documents were checked against each other and never against the source.

## The four moves, and how far each already is

| Move | Current state | Work |
|---|---|---|
| YouTube caption fetch runs from the Control Panel | Runs in the API host | **Code. And it BREAKS on the firewall change** |
| AI runs on the on-site GPT OSS 120B | Provider exists, points at cloud by config | Config, plus optional code to remove the cloud paths |
| Mail goes to an on-site relay | Fully supported already | **Config only. No code** |
| Files go to MinIO over S3 | Filesystem provider only | **Code. The only substantial build** |

---

## 0. The caption import breaks the day the firewall rule lands (do this first)

**This is not a documentation mismatch. It is a functional regression waiting on
a network change**, and it will present as a production bug rather than as a
known consequence.

`FetchSessionSubtitleEndpoint` lives in
`src/Backend/SIMF.Api/Endpoints/Admin/SessionSubtitleEndpoints.cs` and calls
`IYoutubeTranscriptService`, registered into the API container at
`src/Backend/SIMF.Infrastructure/DependencyInjection.cs:750`. The API sits in
HSA. Phase one gives HSA no outbound route, so the moment the egress rule is
applied as drawn, every call to this endpoint fails.

**Change.** Move the outbound fetch to the Control Panel, which is in SSA and is
the tier the diagram grants internet access. The Control Panel fetches the
caption track and posts the text to the API; the API keeps the endpoint that
stores and reviews a transcript and loses the one that reaches YouTube.

**Watch for.** The Control Panel is a BFF with no catch-all proxy: a new
`/account/api/*` route needs an entry on the typed client and an explicit
mapping, or it returns 404 with nothing logged anywhere. `IYoutubeTranscriptService`
moves with its SSRF hardening intact, meaning the caption-host revalidation and
the no-redirect handler, which are load-bearing and not incidental.

**Falsifiable done.** With outbound HTTPS blocked from the API host and permitted
from the Control Panel host, an administrator imports a subtitle successfully.

---

## 1. AI on the on-site GPT OSS 120B endpoint

**Current state.** `src/Shared/SIMF.Common/Enums/AiProvider.cs` declares
`Echo = 0, OpenAi = 1, AzureOpenAi = 2, Anthropic = 3, Gemini = 4`.
`src/Backend/SIMF.Infrastructure/Ai/` holds `EchoAiProvider`, `OpenAiProvider`,
`AnthropicAiProvider` and `GeminiAiProvider`. `AiOptions.OpenAi.BaseUrl` is a
bound configuration value.

**The on-site path therefore needs no code at all.** `OpenAiProvider` against a
local `BaseUrl` is exactly the OpenAI-compatible route the LLM server serves.
What ships today is `"DefaultProvider": "Echo"` with `https://api.openai.com/v1`,
`https://generativelanguage.googleapis.com` and empty keys, so the delivered
default performs no inference and the delivered URLs point at the internet.

**Configuration change**, in `deploy/set-env-api.ps1` rather than in a committed
appsettings file: `Ai:DefaultProvider = OpenAi`,
`Ai:OpenAi:BaseUrl = <the on-site endpoint>`,
`Ai:OpenAi:DefaultModel = <the model id SITE serves>`, and whatever credential
SITE requires, or none.

**Optional code change, recommended.** If a cloud provider must be impossible
rather than merely unselected, delete `GeminiAiProvider` and
`AnthropicAiProvider` with their registrations and configuration sections.
**Do not remove the enum values.** The enum surface is frozen against rename and
reorder (D-110, reinstated by D-895); removal is a reorder-class change, and
D-186 set the precedent that it needs a named lift and a reserved slot. Deleting
the providers makes those values unroutable, which buys the same protection
without a lift. Add a boot guard that refuses to start when `DefaultProvider`
resolves to a provider that is not registered.

**Recorded while reading, not part of this move.** `AzureOpenAi = 2` has no
implementation anywhere in the tree. It is an enum value nothing can resolve.

**Falsifiable done.** With the API host's outbound access blocked, the AI
assistant answers. A packet capture on the API host during an AI request shows
traffic to the LLM host and to nothing else.

---

## 2. Mail through the on-site relay

**Current state, and it is better than the documents implied.**
`src/Shared/SIMF.Common/Options/EmailOptions.cs` binds `Host`, `Port` defaulting
to `587`, `User`, `Password` and `FromAddress`.
`src/Backend/SIMF.Infrastructure/Email/SmtpEmailSender.cs:184` authenticates
**only when `User` is non-empty**, and `SecureOptionsForPort` selects `StartTls`
for port 587.

**An unauthenticated on-site relay on 587 with STARTTLS therefore works today
with no code change whatsoever.** Point `Email:Host` at the relay and leave
`Email:User` empty.

**Falsifiable done.** A verification e-mail arrives, and the relay log shows the
connection came from the API host.

---

## 3. MinIO in place of the filesystem store

**The only item here that is a real build.**

**Current state.** `IFileStorageProvider`
(`src/Backend/SIMF.Application/Files/Abstractions/IFileStorageProvider.cs`) has
eight members: `WriteAsync`, `ReadAsync`, `WriteStreamAsync`, `OpenReadAsync`,
`ExistsAsync`, `DeleteAsync`, `SecureEraseAsync`, and the `StorageKey` the write
methods return. The only implementation is
`src/Backend/SIMF.Infrastructure/Files/FilesystemFileStorageProvider.cs`. No S3
or MinIO package is referenced by any csproj.

**Why this is smaller than it looks.** The interface is already object-shaped: it
addresses content by an opaque `StorageKey` rather than by a path, and
encryption happens above it in `AesGcmEnvelopeCipher`, so the provider carries
ciphertext bytes either way and key handling does not change.

**Work.**

1. Reference an S3 client. `AWSSDK.S3` speaks to MinIO with a custom
   `ServiceURL` and path-style addressing; the `Minio` package is the
   alternative. Pick one and record why in the decisions log.
2. `MinioFileStorageProvider : IFileStorageProvider`, one bucket, reusing the
   existing `StorageKey` as the object key so nothing above the provider changes.
3. `FileStorage:Provider` selects filesystem or MinIO, keeping the filesystem
   provider for development. Bind endpoint, bucket, credentials and TLS through
   a typed options class, never scattered magic strings.
4. **Decide `SecureEraseAsync` deliberately.** The filesystem provider overwrites
   bytes in place. Object storage cannot: the S3 equivalent is a delete against a
   bucket with versioning off and no lifecycle retention. If the NCA position
   requires a genuine overwrite, that must be argued now rather than discovered
   at audit.
5. One-time migration of existing objects from `FileStorage:RootPath` into the
   bucket, keyed identically, verified by SHA-256 per object and not by count
   alone.
6. Tests: a round trip per `FileService`, a range read on the recording path
   because HTTP 206 needs a seekable stream, a delete, and whatever step 4
   decides.

**Falsifiable done.** With the file share unmounted, an avatar uploads, an
identity document downloads decrypted, and a session recording seeks to the
middle and plays.

---

## Order, and why

Item 0 first: it is the only one that turns a working feature into a broken one,
and it breaks on someone else's firewall change rather than on ours. Then 2,
which costs nothing. Then 1, a configuration line plus an optional deletion.
Then 3, which is the build.

## Out of scope, deliberately

The two databases, the zone layout, the permission model and the schema. This
plan changes how the platform reaches four services. It changes no contract: the
shipped mobile wire contract stays append-only, and the schema freeze (D-895)
stays sealed, because nothing here needs a column.

## What this plan cannot decide

Whether a cloud AI provider should remain in the tree at all, and whether
`SecureEraseAsync` may become a delete. Both are owner calls and both are
flagged above rather than assumed.
