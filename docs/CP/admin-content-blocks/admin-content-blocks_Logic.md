# CP Content blocks — Logic (`/admin/content-blocks`)

The rules behind the page — the keyed-upsert model, normalisation, soft-delete,
the public read the app consumes, and the seeded core content. Grounded in
`AdminCmsService.cs`, `PublicCmsEndpoints.cs`, `ContentBlock.cs`,
`IdentitySeeder.cs`, and the razor pages.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## L-1 — One block = one keyed bilingual row
A `ContentBlock` (`SIMF.Domain.Cms`) is a stable **Key** slug plus paired
**Content** (English) + **ContentArabic** (Arabic) bodies (up to 8000 chars
each, markdown allowed), an **IsActive** soft-delete flag, `LastUpdatedAt`, and
`LastUpdatedByUserId` (logical FK to `SimfUser.Id` on the **Identity** DB —
resolved on read, never a cross-DB constraint per D-157). The entity lives on
the **App** DB (`SimfAppDbContext.ContentBlocks`), backed by migration `AddCms`
(2026-05-29).

## L-2 — Upsert is keyed, not id-based
The single `PUT /admin/content-blocks` (`UpsertContentBlockAsync`) serves both
**create** and **edit**:
- The server normalises the Key (`Trim().ToLowerInvariant()`) and looks up by
  it. **Absent → insert** a new row (new `Id`, `CreatedAt = LastUpdatedAt = now`).
  **Present → update in place** (same `Id`; overwrite Content/ContentArabic/
  IsActive; bump `LastUpdatedAt`).
- The CP locks the **Key field on Edit** (`Disabled="_busy || IsEdit"`), so a
  key collision is only reachable from the New-block path — and it **silently
  upserts** onto the existing row. `CONTENT_BLOCK_KEY_DUPLICATE` exists in
  `ErrorCodes` but this path never raises it.

## L-3 — Key normalisation
`NormaliseKey(raw) = (raw ?? "").Trim().ToLowerInvariant()`. So
`"HOME.WELCOME.TITLE"` and `"home.welcome.title"` are the **same** block. The CP
form trims the Key before the PUT; the server normalises again on every read,
upsert and delete. Convention: lower-kebab-case, dotted hierarchy
(e.g. `home.welcome.title`).

## L-4 — Validation (server-side only)
The razor only guards a present, ≤ 128-char Key (`Admin.ContentBlocks.Required`).
Everything else is enforced in `UpsertContentBlockAsync`:
- Key length must be **2..128** → 400 `CONTENT_BLOCK_INVALID`.
- Content / ContentArabic each ≤ **8000** chars → else 400 `CONTENT_BLOCK_INVALID`.
- Null bodies default to `""` before the length check, so an empty body is
  accepted on the admin upsert. (The Excel **import** path additionally requires
  Key/Content/ContentArabic non-blank per row — see the CP-ref doc §4.5.)

## L-5 — Soft-delete + idempotency
`DeactivateContentBlockAsync` sets `IsActive = false` (never hard-deletes); the
row stays in the grid with an **off** pill.
- Missing key → 404 `CONTENT_BLOCK_NOT_FOUND`.
- Already inactive → **idempotent no-op** (returns without writing; the endpoint
  still answers HTTP 200 `Data = true`).

## L-6 — Audit
Both mutations write an audit entry (`IAuditLog.WriteAsync`):
- upsert → `AuditEvents.ContentBlockUpserted`, `Detail = "key=<key>"`;
- deactivate → `AuditEvents.ContentBlockDeactivated`, `Detail = "key=<key>"`.
Both carry `ActorUserId` (the `sub` claim) and `AuditOutcome.Success`.

## L-7 — The public read the app consumes
The same blocks are read **anonymously** by the Flutter app + Website through
`GET /api/v1/app/content/{key}` (`GetPublicContentBlockEndpoint`,
`AllowAnonymous`). Logic that differs from the admin side:
- **Inactive blocks are hidden** — `PublicCmsService.GetContentBlockAsync`
  returns null for an absent **or inactive** key → the endpoint 404s
  (`CONTENT_BLOCK_NOT_FOUND`). So deactivating a block here breaks the app's
  read of that key.
- **Public payload is trimmed** — `PublicContentBlock` is `{ key, content,
  contentArabic, lastUpdatedAt }` only (no id, no `isActive`, no actor).
- **`If-Modified-Since` / 304** — the endpoint truncates `lastUpdatedAt` to the
  second, emits it as `Last-Modified`, and answers `304 Not Modified` when the
  request's `If-Modified-Since` is at/after that instant. (The shipped Flutter
  Terms page does **not** send the header — every load is a full 200; the 304
  path is unused by the app — see Page_009_API.md.)
- **Batch read** — `POST /app/content/batch` (`{ Keys }`) returns only the
  existing active keys as a map.

## L-8 — Well-known keys (the app wire contract)
The client codes against the **slug**, so renaming a Key is a **wire-breaking**
change. The well-known keys this page governs:
| Key | App surface | Read route |
|-----|-------------|-----------|
| `terms` | App Page 009 — الشروط والأحكام · Terms | `GET /api/v1/app/content/terms` |
| `about` | App Home / static About (Page 013 group) | `GET /api/v1/app/content/about` |
| `cyber.*` | App cybersecurity-policy screen | seeded by `IdentitySeeder` |

`terms` returns the T&C body the app splits into lines (Page_009_Logic);
`about` backs the static About copy. Editing/deactivating/renaming any of these
from this page changes (or breaks) the live app screen.

## L-9 — Core-content seed (D-377)
`IdentitySeeder.EnsureCoreAppContentAsync` inserts the **`terms` + `about`**
blocks **per absent key** (the same insert-when-absent shape as the cyber +
landing content seeds), so every fresh environment boots with non-empty T&C /
About pages. The first production install shipped with these keys missing
(empty T&C/About) — D-377 moved the reviewed production copy into the startup
seed. Data-only, idempotent, no migration. The seed never resurrects a key an
admin deliberately deleted at runtime once it exists (insert-when-absent).

## L-10 — List query rules (grid)
`ListContentBlocksAsync`:
- `Skip` ≥ 0; `Top` clamped to `[1,200]` (default 25 when ≤ 0; the page sends 20).
- `Search` → `LIKE %term%` over `Key` / `Content` / `ContentArabic`.
- Per-column `Filters`: **`key`** → `Key.Contains`, **`content`** →
  `Content.Contains`, **`isactive`** → `IsActive ==` (parsed bool). Unknown
  columns ignored.
- `Sort`: **`key`**, **`content`**, **`lastupdatedat`** (asc/desc); default
  **Key ascending**.

> **Filter-key naming:** the live grid column + the service both use **`content`**
> for the English-body filter/sort. The existing CP-ref + E2E docs name it
> `contentEn`; the source agrees on `content`. Treat `content` as authoritative.

## L-11 — No detail round-trip
`AdminContentBlockSummary` carries every field the Add/Edit and View/Delete
forms need (Key, Content, ContentArabic, IsActive, LastUpdatedAt). The grid row
binds straight into the form — there is **no** `GET /admin/content-blocks/{key}`
call to open a form. (That GET endpoint exists and is gated `ContentBlocks.View`,
but the page does not use it for its forms.)

## Edge cases / known limitations
- **No client-side validation** beyond the Key-present/≤128 guard — every other
  bound is reached by submitting the out-of-bound value (server 400).
- **Selection is cosmetic for the grid** — no bulk delete; ticks only narrow the
  Excel export.
- **Renaming a Key is wire-breaking** — there is no rename operation (Key is
  locked on Edit); a "rename" is a new block + a delete, which the app would see
  as the old key going 404.
- **Empty body accepted on admin upsert** but the app treats a block whose both
  bodies are blank as an **empty** state (Page_009: `200` + blank → empty+retry).
