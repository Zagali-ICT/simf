# SIMF — 2026 migration (data seeding)

This folder is the **2026 go-live data package**: the manual SQL that loads the
initial `SIMF_App` content for the SIMF-4 2026 event, plus the file assets that
ship with it. Run against `SIMF_App` at go-live (and after any fresh-DB rebuild).
See `docs/decisions/DECISIONS_LOG.md` D-718.

**Convention (D-718, owner rule).** SIMF splits seeding into two lanes:

| Lane | What | How | When it runs |
|------|------|-----|--------------|
| **Lookups / basic data** | `Country`, `Region`, `SessionCategory`, `Organisation`, and similar stable reference tables | EF `HasData` in the entity's `*Configuration.cs`, or a C# seeder | **Automatically** on migrate / boot |
| **Content / business data** | Speakers, Sponsors, News, Sessions/Programme, Booths, Delegations, FAQ, Archive editions, Media gallery, … | A **manual SQL** file in this folder (`SIMF_App_*.sql`) | **By hand only** — never auto-run |

**Why:** production content must be curated and reviewed (real, vetted rows), not
auto-populated with demo/placeholder data on every deploy. Lookups are stable
reference data every environment needs identically, so migrations are their home.

## Rules for a content-seed SQL file

- Target `SIMF_App` (never `SIMF_Identity`).
- **Idempotent** — every `INSERT` guarded by `IF NOT EXISTS`; safe to re-run.
- **One transaction** with `SET XACT_ABORT ON` so any error rolls the whole file
  back (no partial data). Set `QUOTED_IDENTIFIER`/`ANSI_NULLS` ON explicitly.
- Use the system actor `@sys = '00000000-0000-0000-0000-000000000000'` for
  `CreatedBy` (matches the app seeders) and `@now = SYSDATETIMEOFFSET()`.
- Respect the D-110 schema freeze: **data rows only**, no `ALTER`/`CREATE`.
  Adding a missing lookup *value* an admin could add via the CP (e.g. a `Country`
  row) is allowed as a guarded `INSERT` — it is data, not a schema change.
- Do **not** add content rosters to `IdentitySeeder` / `DefaultContentSeeder`.

## How to run

Order matters: `SIMF_App_Programme.sql` creates the **`MAIN` hall** that
`SIMF_App_SeedGaps.sql` (booths + venue-map nodes) references, so Programme runs
**before** SeedGaps; `SpeakerPhotos` must run **after** `Speakers` (it points at
those speaker rows). Both options below run the **same 9 content files** in that
order. The `RegistrationReferenceSequence` hotfix is **not** part of this run
(see the Files table) — it is a separate, prod-only unblock.

**Target database.** These seeds go to the **App / content** database — the one
that holds the app tables (`Speakers`, `Halls`, `Sessions`, …), i.e. whatever the
API's `SimfAppDb` connection string points at. Its physical name is
**environment-specific**: **`SIMF_Data`** on the deployment server, **`SIMF_App`**
on a local dev box. **Never** run them against the Identity database
(`SIMF_Identity`). To find it: `SELECT name FROM sys.databases WHERE
OBJECT_ID(QUOTENAME(name)+'.dbo.Speakers') IS NOT NULL;`

### Option A — SSMS, one click (`Run_All_App_Seeds.sql`)

Open **`Run_All_App_Seeds.sql`** in SSMS, turn on **SQLCMD Mode**
(*Query → SQLCMD Mode* — **required**, or the `:r` / `:setvar` lines error with
"Incorrect syntax near ':'"), edit its two `:setvar` lines at the top
(`MigrationDir` = the full path to this folder; `AppDb` = the App/content DB —
`SIMF_Data` on the server, `SIMF_App` on local dev), then press **F5**. It runs all 9 content seeds in order, prints `[1/9]…[9/9]`
progress, and stops on the first error (`:on error exit`, so no partial data).
Idempotent — safe to re-run. Then do the speaker-photo copy step below.

### Option B — `sqlcmd` CLI (one file at a time)

Run from the repo root. Set `$Db` to the App/content DB (`SIMF_Data` on the
server, `SIMF_App` on local dev — never `SIMF_Identity`); add `-S <server>` if
not the default local instance:

```powershell
$Db = "SIMF_Data"
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_Programme.sql
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_News.sql
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_Sponsors.sql
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_MediaPartners.sql
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_Archive.sql
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_Organization.sql
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_Speakers.sql
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_SpeakerPhotos.sql
sqlcmd -d $Db -f 65001 -i docs\migrations\2026\SIMF_App_SeedGaps.sql
# then copy docs\migrations\2026\speaker-photos\speakerphoto  →  C:\SIMF\Storage\files\speakerphoto
```

> **UTF-8 Arabic (`-f 65001`).** The seed files are UTF-8 with **no BOM**. The
> `sqlcmd` CLI otherwise reads input in the ANSI code page and mangles the Arabic
> (it can also overflow `nvarchar` limits and fail the run), so every command
> above passes **`-f 65001`**. SSMS SQLCMD Mode (Option A) reads UTF-8 natively
> and needs no flag.

> **Dev / Test auto-run (D-747).** In **Development** and **Testing** these files
> are applied automatically by `SqlContentSeeder` (dev boot runs all of them;
> the test fixture runs the roster set, i.e. everything except `SeedGaps`), so a
> fresh dev/test DB is not empty. **Production still runs them by hand** with the
> commands above. The content that used to be seeded in C# (`DefaultContentSeeder`
> hall/programme/news; `IdentitySeeder` speakers/sponsors/media-partners/archive/
> org-about) now lives ONLY in these files.

## Files

Two files are **not** content seeds:

- **`Run_All_App_Seeds.sql`** — the SSMS one-click runner (Option A above). Runs
  the 9 content seeds below, in order, via SQLCMD-Mode `:r` includes.
- **`SIMF_App_RegistrationReferenceSequence_Hotfix.sql`** — a separate, **prod-only**
  unblock that creates the missing `dbo.RegistrationReferenceSequence` on a
  *running* DB (fixes create-user 500 · SqlException 208). Run by hand **only** if
  a live prod is missing it and you are not rebuilding; see the file header. The
  permanent fix is the sequence on the model + the consolidated App migration
  (D-373), so a fresh DB needs nothing from it. **Not part of the seed run.**

The 9 content seeds (run in this order):

| File | Seeds | Decision |
|------|-------|----------|
| `SIMF_App_Programme.sql` | Main hall · 5 themes (axes) · 3 programme days (23-25 Nov 2026) · 59 real run-of-show sessions + session↔theme links (soft-deletes the old placeholder days/sessions) | D-747 (was `DefaultContentSeeder`, D-681); real 2026 deck |
| `SIMF_App_News.sql` | One "Highlights" news item | D-747 (was `DefaultContentSeeder`, D-681) |
| `SIMF_App_Sponsors.sql` | 10 sponsors (SAMI Platinum · GAMI/RSNF/GADD Gold · 6 Silver fillers) | D-747 (was `IdentitySeeder`, D-348) |
| `SIMF_App_MediaPartners.sql` | 3 media partners (external placeholder logos) | D-747 (was `IdentitySeeder`, D-348) |
| `SIMF_App_Archive.sql` | 4 past editions (2022-2025) + 2024 child session-titles + past speakers | D-747 (was `IdentitySeeder`, D-347) |
| `SIMF_App_Organization.sql` | Org About/Vision/Mission/Themes (real deck text) + social links | D-747 (was `IdentitySeeder`, D-586) |
| `SIMF_App_SeedGaps.sql` | Booths · Delegations · FAQ · Venue map (the 4 empty app screens) | D-687 |
| `SIMF_App_Speakers.sql` | 32 real SIMF-4 2026 speakers (text) (+ Poland/Tunisia country rows) from `15-04-2024/3قائمة المتحدثين.pptx` | D-718 |
| `SIMF_App_SpeakerPhotos.sql` | 23 speaker photos as `StoredFile` rows (SpeakerPhoto, public/plaintext) | D-718 |

### File assets — centralized StoredFile store

Content that carries a file (speaker photos, logos, …) has TWO parts: the DB
row (a `StoredFile`, seeded by SQL) **and** the bytes on disk under the API
file-storage root (`FileStorage:RootPath`). SQL seeds the row; the bytes ship as a
folder you deploy into that root. For a **public, un-encrypted** service (e.g.
`SpeakerPhoto`) the bytes can be pre-placed as-is; encrypted services (avatar, ID
doc) must go through the upload API instead.

> **Root path (D-718):** in production `FileStorage:RootPath` is pinned to
> `C:\SIMF\Storage\files` (`deploy/set-env-api.ps1`), **outside** the IIS site —
> the code default `App_Data/files` sits *inside* the site, where
> `iis-deploy.ps1`'s `robocopy /MIR` would purge every runtime upload + seeded
> file on the next deploy. Dev uses the `App_Data/files` default.

`speaker-photos/speakerphoto/` (this folder) holds the 23 speaker images, each named
`{StoredFile.Id:N}.{ext}` to match its `StorageKey`. **Deploy step:** copy that
`speakerphoto` folder into the root, giving (prod)
`C:\SIMF\Storage\files\speakerphoto\{id}.{ext}` (see `speaker-photos/MANIFEST.txt`).
Run `SIMF_App_SpeakerPhotos.sql` and deploy the folder together — a row with no
file on disk simply 404s the photo serve.
