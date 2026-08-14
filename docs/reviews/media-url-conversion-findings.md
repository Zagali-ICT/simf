# Converting the media URL columns: what the code actually requires

> Working note for the stream/video half of the one-store programme. Produced by
> reading every call site and then trying to **refute** three claims I had been
> proceeding on. All three were refuted. This note exists because two of them were
> written into the approved plan, and following that plan literally would have
> broken the shipped app in one place and every logo paste in another.

## Claim 1 — REFUTED: "move `LiveStreamUrlPolicy` into `CreateExternalLinkAsync` so it guards every external reference"

This is in the plan. It is wrong, and it would break the feature it sits next to.

`LiveStreamUrlPolicy.IsAllowed` is a **live-feed allow-list**, not a URL sanity
check: it returns true only for a YouTube link with an extractable 11-character
video id, or a path ending `.m3u8` / `.mp4`. Every `ExternalLink` row that exists
today is an **image** — `CreateExternalLinkAsync` hardcodes
`FileType = FileType.Image` with the comment "external links back the public image
surfaces (logos / covers)". So the proposed guard makes the only thing that path is
used for illegal:

- the three seeded `placehold.co` partner logos (no extension, not YouTube);
- every paste through `SimfImageUpload`'s "External link" tab — the one shared
  control for all eleven asset categories;
- `POST /files/link`, documented as "record an external image link";
- two pinning tests that use `https://cdn.example/x.jpg` and
  `.../sponsor-logo.png`.

It is also **not a superset** of the existing check: `https://localhost/x.mp4`
passes `IsAllowed` but is correctly rejected by `ValidateExternalLink`'s
private-host guard. It could only ever be an additive overlay, never a replacement.

**Why the mistake was available to make.** The image surfaces never inspect the URL
— the download endpoint 302-redirects and the client follows it. Only the live and
hero surfaces classify the string. A rule written for the classifying consumers
cannot be hoisted onto the non-classifying ones.

### The real hole the instruction was reaching for

`CreateExternalLinkAsync` accepts **any** `FileService`. Nothing stops
`POST /files/link` with `Service=IdDocument` (Secret) or `Avatar` / `VipPhoto`
(Confidential, `EncryptAtRest: true`), which creates an unencrypted, unscanned row
that points a "private" file at a third-party host and bypasses the entire ingest
pipeline. That is a genuine defect, it is what "guard every external reference"
should have meant, and the guard is per-policy rather than per-URL-shape.

### The correct split, keyed on `FileServicePolicy`

| Layer | Rule |
|---|---|
| Base (all services) | absolute `https`, <= 1024 chars, public host. Unchanged; it is the floor. |
| A — who may be linked at all | refuse an external link when the service is not Public tier **and** Public access. Closes the Secret/Confidential hole above. |
| B — per `AllowedTypes` | image services: base only, and deliberately **no** extension rule (that over-correction breaks the same seeds). Video services: base **plus** `LiveStreamUrlPolicy`, because those URLs ride the wire verbatim and both clients classify them by string. |
| C — stop hardcoding the type | `FileType = Image` must become policy-derived once video links are allowed, or a hero-video link is typed `Image` and the download endpoint's inline-vs-attachment rule (`Tier == Public && FileType == Image`) misfires. `AssetService.SetExternalLinkAsync` also silently ignores its own `kind` argument while `SetUploadAsync` tells the admin "video assets must be an external link" — the two should be reconciled together. |

## Claim 2 — REFUTED: "converting the column is safe as long as the server emits the same JSON key carrying a loadable URL"

The claim assumes the app hands the string to a player. It does not. For every one
of these keys the app either **parses** the URL to decide how to load it, or uses
it as a **boolean** and fetches from a route it composes itself. "Loadable" is
therefore not the operative property.

**The decisive case.** `LiveVideoPlayer` does not gate, it *branches*: it calls
`YoutubeUrl.tryParseId(url)` and, on a hit, builds a YouTube controller from the
extracted id; otherwise it falls through to `VideoPlayerController.networkUrl`.
Today's live values are YouTube links. Route one through a resolved `/files/{id}`
URL — which is exactly what the claim licenses, and it *is* loadable, it redirects
fine — and `tryParseId` returns null, so the app hands a YouTube **watch page** to
ExoPlayer and lands on the player-error surface.

**The silent case.** The home hero asks `isSupported(url)` before mounting
anything, and that requires a last path segment ending `.mp4` / `.m3u8`. A
`/files/{id}` URL fails it, so the hero is never mounted and drops to the banner
carousel. Nothing throws, nothing logs, and no test fails unless it asserts the URL
itself. A regression there looks exactly like "the old hero".

Note the two surfaces disagree: the same converted URL produces a **visible error**
on the live screen and an **invisible fallback** on home.

**Presence sentinels, again.** `mainUrl != null` drives five separate pieces of UI
that have nothing to do with video bytes: the LIVE badge fallback, whether the
Ask-a-Question affordance appears, the sign-language toggle, and whether the
organiser notice renders at all. The media gallery reduces `imageUrl` /
`thumbnailUrl` to booleans and fetches from a client-composed route — its own
comment says the wire string "is a presence signal, not a fetch URL".

**The corrected claim.** The server must emit a URL that survives the client's own
classifier unchanged:

- an `ExternalLink` row must be emitted **verbatim**, never behind `/files/{id}`;
- an `Upload` row must resolve to an absolute `https` URL whose last path segment
  ends `.mp4` / `.m3u8`;
- null-versus-empty semantics must stay bit-for-bit identical.

## Claim 3 — REFUTED: "every server-side consumer can still obtain the raw URL"

Two consumers that parse these URLs live in processes with **no database**:

- **`SIMF.Web`** classifies `BackgroundVideoUrl` to choose a YouTube iframe versus
  a `<video>` tag. It references only ApiClient / Common / Components / Contracts —
  no Infrastructure, no EF. It cannot resolve a Guid at any price.
- **The Control Panel** is the same four references, and it hosts both the paste
  field and the "is this ours" predicate.
- **`YoutubeTranscriptService`** takes `(HttpClient, ILogger)` and extracts a video
  id from the URL to fetch subtitles. Its input arrives from the request body via
  the CP form. Every hop is wire-only.

And the read path cannot compose an absolute URL today:
`OrganizationProfileReadService` takes `(IMemoryCache, SimfAppDbContext)` — no
`IHttpContextAccessor`, no `IOptions` — and caches the composed response for five
minutes across requests, so a request-derived host is both unavailable and wrong to
bake into a shared cache entry.

**The precedent points the wrong way.** `Banner.ImageUrl` -> `ImageFileId` ended
with the wire URL permanently null and replaced by a 302 route. Applying that
precedent here fails both branches of the website's classifier and silently breaks
both heroes.

## What this means for the conversion

It is still the right destination, and nothing here argues for a URL column. But it
is not a retype:

1. The read path must gain a **base-URL** dependency so an `Upload` row can compose
   an absolute `https` `.mp4` route. That plumbing does not exist — the base URL is
   an API-layer option consumed only on two admin writes.
2. `ExternalLink` rows must be emitted **verbatim**, which is the opposite of what
   the download-by-id indirection does everywhere else in this programme.
3. The client's classifier rules must be **pinned by a server-side test** —
   the emitted string has to satisfy "extractable YouTube id, or last segment
   `.mp4`/`.m3u8`". The codebase already accepts this kind of mirror:
   `LiveStreamUrlPolicy` is documented as the C# twin of `youtube_url.dart`. That
   test is what substitutes for a device check on the invisible hero path, which no
   passing test would otherwise catch.
4. `OrganizationHeroVideoService`'s full-string equality predicate, and the CP's
   *suffix* predicate for the same question, both disappear — the conversion
   removes the two-authoring-paths problem rather than merely documenting it.
