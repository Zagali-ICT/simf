# SIMF Media-Asset — the one way to upload/download an image

> **Authority:** D-357. This is the single mechanism for attaching an image (or an
> external image/video/document link) to an entity in SIMF. If you are adding a
> picture to anything, use this — do **not** add a new per-entity upload column,
> controller, or storage path. The pre-existing avatar / media-gallery / ID-document
> / session-recording pipelines are left as-is and are **not** replaced by this.

## 1. The model in one paragraph

Every image lives as one row in the single `Asset` table (`SimfAppDbContext`). A row
is identified by a **category** (`AssetCategory` enum) + a **polymorphic owner id**
(a bare `Guid` pointing at the owning row in its own table — **no** cross-table FK,
per D-157). A row is either an **upload** (bytes stored out-of-row on disk via
`IImageAssetStorage`, mirroring the Media image pattern D-90) or an **external link**
(`AssetSourceType.ExternalLink` — just a URL). Each row has a **kind**
(`Image` / `Video` / `Document`) and is **soft-deleted** (`IsActive` / `DeletedAt`).
A filtered unique index `(Category, OwnerId) WHERE IsActive` guarantees **one live
asset per (category, owner)**.

```
Asset
 ├─ Category    AssetCategory     (SpeakerPhoto, CompanyLogo, MediaPartnerLogo,
 │                                 SponsorLogo, ArchiveCover, NewsImage, …)
 ├─ OwnerId     Guid              (the owning row's id — logical FK, resolved on read)
 ├─ SourceType  Upload | ExternalLink
 ├─ Kind        Image | Video | Document
 ├─ StoragePath / ExternalUrl / ContentType / SizeBytes / OriginalFileName
 └─ IsActive / DeletedAt          (soft-delete)
```

All logic lives in **one** service, `IAssetService`:
`SetUploadAsync`, `SetExternalLinkAsync`, `ResolveAsync`, `ListAsync`,
`GetByIdAsync`, `DeactivateAsync`, `RestoreAsync`. Validation is centralised there
(≤5 MB `png|jpeg|webp`, ≤10 MB `pdf`, video = link-only), as is the audit
(`AssetUploaded` / `AssetLinked` / `AssetRemoved` / `AssetRestored`) and the
per-category owner-name resolution.

## 2. Endpoints (already generic — you rarely touch these)

| Verb + path | Auth | Purpose |
|-------------|------|---------|
| `POST /admin/assets/{category}/{owner}/image` | owning entity's **Edit** (via `AssetPermissionRegistry`) | upload a file (`?kind=Image\|Document`) |
| `PUT /admin/assets/{category}/{owner}/link` | owning entity's **Edit** | set an external link `{ Kind, Url }` |
| `GET /admin/assets/{category}/{owner}/image` | owning entity's **View** | admin preview (streams bytes, 302s a link, 404s none) |
| `GET /app/assets/{category}/{owner}/image` | **anonymous** | public/app read (same behaviour) |
| `POST /admin/assets/list` | `MediaLibrary.View` | management grid |
| `GET/DELETE/POST …/item/{id}[/restore]` | `MediaLibrary.View` / `.Manage` | get / deactivate / restore |

The CP reaches these through the BFF passthroughs in `AccountEndpoints.cs`
(`/account/api/admin/assets/...`); the website reaches the anonymous read through the
same-origin proxy `/content/assets/{category}/{owner}/image` in `SiteContentEndpoints.cs`.

## 3. Add an image to a NEW entity — the checklist

Say you add an entity `Foo` and want a `FooBanner` image.

1. **Add the category.** Append a value to `AssetCategory` (additive only — never
   rename/reorder existing values; the enum is persisted by name where it matters).
   ```csharp
   public enum AssetCategory { …, FooBanner = 6 }
   ```
2. **Map its permission.** In `AssetPermissionRegistry`, add
   `[AssetCategory.FooBanner] = new Gate(PermissionCatalog.Foo.View, PermissionCatalog.Foo.Edit)`.
   The guard test `AssetPermissionRegistryTests` fails the build until every category
   is mapped — so this step is not optional.
3. **Resolve its owner name.** In `AssetService.ResolveOwnerNamesAsync`, add the
   `FooBanner` case that batch-loads `Foo.Name` for the owner ids (so the Media
   Library shows a human owner, not a Guid).
4. **CP Add/Edit form (edit mode only).** Drop the self-localising control in, after
   the IsActive checkbox, inside `@if (IsEdit && Initial is not null)`:
   ```razor
   <div class="simf-field">
       <span class="simf-field__label">@L["Admin.Asset.Heading"]</span>
       <SimfImageUpload Category="FooBanner" OwnerId="@Initial.Id" Alt="@_model.Name" />
   </div>
   ```
   It is **edit-only on purpose** — the owner row must exist (a non-empty `OwnerId`)
   before bytes can attach; with `Guid.Empty` the control shows the "save first" hint.
   `SimfImageUpload` self-localises from `Admin.Asset.*`, so those three attributes
   are all you pass.
5. **CP View form.** Show the thumbnail before the `<dl>`:
   ```razor
   <div class="simf-image-upload__preview">
       <SimfImageThumb Src="@($"/account/api/admin/assets/FooBanner/{Initial.Id}/image")"
                       Alt="@Initial.Name" Class="simf-img-thumb--lg" />
   </div>
   ```
6. **Website (only if the image is public).** Render
   `/content/assets/FooBanner/{ownerId}/image` (same-origin proxy). Note: a pure
   no-IO content reshape cannot tell whether an asset exists, so do **not** emit that
   URL unconditionally where a 404 would break the layout — prefer it only when the
   API signals asset presence (see the speaker-card follow-up).
7. **DoD (same changeset):** docs (`PAGE-INDEX` + the per-page doc) + unit/integration
   tests + the E2E catalogue file. No new storage, controller, or column — the
   pipeline already carries all of it.

## 4. Rules of the road

- **One mechanism.** No new per-entity upload column/endpoint/storage. If you find
  yourself adding a `XxxImageBytes` column or a bespoke uploader, stop — use a
  category here instead.
- **No cross-DB / cross-table FK.** `OwnerId` is a logical FK (bare `Guid`), resolved
  on read; never add a navigation/`HasForeignKey` from `Asset` to the owner (D-157).
- **Soft-delete, never hard.** Deactivate; the bytes/link survive so a restore is
  loss-less. Restore is blocked (409) if a newer live asset already owns the pair.
- **Permissions:** category endpoints reuse the **owning entity's** View/Edit (no new
  per-entity codes); only the management page uses `MediaLibrary.View` / `.Manage`.
- **Validation lives in `IAssetService`** — change limits there once, not per call site.
- **Additive only** — new `AssetCategory` values are fine; never rename/reorder
  existing enum values, and keep the shipped mobile wire contract intact.

## 5. Source map

| Concern | File |
|---------|------|
| Entity + config + migration | `SIMF.Domain/Assets/Asset.cs`, `…/Configurations/App/AssetConfiguration.cs`, `…/Migrations/App/*_D357_AddAssets.cs` |
| Service + storage | `SIMF.Application/Assets/Abstractions/IAssetService.cs`, `SIMF.Infrastructure/Assets/AssetService.cs`, `…/FilesystemImageAssetStorage.cs` |
| Enums + permission map + contracts | `SIMF.Common/Enums/Asset*.cs`, `SIMF.Common/AssetPermissionRegistry.cs`, `SIMF.Contracts/Assets/Assets.cs` |
| API + BFF + clients | `SIMF.Api/Endpoints/Assets/AssetEndpoints.cs`, `ControlPanel/Endpoints/AccountEndpoints.cs`, `ApiClient/Simf{Admin,Public}Client.cs` |
| CP UI | `ControlPanel/Components/SimfImageUpload.razor`, `Shared/SIMF.Components/Forms/SimfImageThumb.razor`, `ControlPanel/Components/Pages/Admin/MediaLibraryList.razor` |
| Website proxy | `Website/SIMF.Web/Endpoints/SiteContentEndpoints.cs` |

---

_Last reviewed:_ 2026-06-10 (D-357).
