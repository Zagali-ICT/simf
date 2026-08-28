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
- Respect the D-110 schema freeze: **data rows only**, no `ALTER`/`CREATE` —
  in the CONTENT SEEDS. The prod-only `*_Hotfix.sql` files are the stated
  exception and do carry DDL, because of this:
  **a regenerated `InitialCreate` is a no-op against a database that already
  has one.** `__EFMigrationsHistory` records it as applied, so `MigrateAsync`
  applies nothing and the live DB never receives the newer schema. Any schema
  change landing on a database with data therefore needs a hand-run delta here
  as well as the regeneration — see `tools/migrations/Regenerate-Migration.ps1`.
  Adding a missing lookup *value* an admin could add via the CP (e.g. a `Country`
  row) is allowed as a guarded `INSERT` — it is data, not a schema change.
- Do **not** add content rosters to `IdentitySeeder` / `DefaultContentSeeder`.

## How to run

Order matters: `SIMF_App_Programme.sql` creates the **`MAIN` hall** that
`SIMF_App_SeedGaps.sql` (booths + venue-map nodes) references, so Programme runs
**before** SeedGaps; `SpeakerPhotos` must run **after** `Speakers` (it points at
those speaker rows). Both options below run the **same 9 content files** in that
order.

**Target database.** These seeds go to the **App / content** database — the one
that holds the app tables (`Speakers`, `Halls`, `Sessions`, …), i.e. whatever the
API's `SimfAppDb` connection string points at. Its physical name is
**environment-specific**: **`SIMF_Data`** on the deployment server, **`SIMF_App`**
on a local dev box. **Never** run them against the Identity database
(`SIMF_Identity`). To find it: `SELECT name FROM sys.databases WHERE
OBJECT_ID(QUOTENAME(name)+'.dbo.Speakers') IS NOT NULL;`

### Option A — terminal (`Run-AppSeeds.ps1`) — preferred

```powershell
cd docs\migrations6
.\Run-AppSeeds.ps1 -Server "PROD\SQL01" -Database SIMF_Data   # local dev: just .\Run-AppSeeds.ps1
```

Runs all 9 seeds in order, stops on the first error, and refuses to run against a
database with no `dbo.Speakers` (so it cannot be pointed at `SIMF_Identity`).
Idempotent. Then do the speaker-photo copy step below.

### Option B — SSMS, one click (`Run_All_App_Seeds.sql`)

Open **`Run_All_App_Seeds.sql`** in SSMS, turn on **SQLCMD Mode**
(*Query → SQLCMD Mode* — **required**, or the `:r` / `:setvar` lines error with
"Incorrect syntax near ':'"), edit its two `:setvar` lines at the top
(`MigrationDir` = the full path to this folder; `AppDb` = the App/content DB —
`SIMF_Data` on the server, `SIMF_App` on local dev), then press **F5**. It runs all 9 content seeds in order, prints `[1/9]…[9/9]`
progress, and stops on the first error (`:on error exit`, so no partial data).
Idempotent — safe to re-run. Then do the speaker-photo copy step below.

### Option C — `sqlcmd` CLI (one file at a time)

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

> **UTF-8 Arabic.** Since D-845 every seed file carries a **UTF-8 BOM**, so
> `sqlcmd`, SSMS and any other tool read the Arabic correctly with no flag. The
> commands above still pass `-f 65001` belt-and-braces. **Do not re-save these
> files without the BOM** — without it `sqlcmd` falls back to the system ANSI code
> page, each Arabic character becomes 2-3 Latin-1 characters, and the run dies on
> `Msg 2628 ... would be truncated`.

> **Dev / Test auto-run (D-747).** In **Development** and **Testing** these files
> are applied automatically by `SqlContentSeeder` (dev boot runs all of them;
> the test fixture runs the roster set, i.e. everything except `SeedGaps`), so a
> fresh dev/test DB is not empty. **Production still runs them by hand** with the
> commands above. The content that used to be seeded in C# (`DefaultContentSeeder`
> hall/programme/news; `IdentitySeeder` speakers/sponsors/media-partners/archive/
> org-about) now lives ONLY in these files.

## Files

Not content seeds:

- **`Run-AppSeeds.ps1`** — the terminal runner (Option A above). Preferred.
- **`Run_All_App_Seeds.sql`** — the SSMS one-click runner (Option B). Runs the 9
  content seeds below, in order, via SQLCMD-Mode `:r` includes.
- **`DEPLOY.md`** — the one-page deploy / migrate / sign-in runbook card. The
  authoritative operations document is `docs/SIMF-OPS-001`.
- **`SIMF_App_RegistrationReferenceSequence_Hotfix.sql`** — a **prod-only** unblock
  creating a missing `dbo.RegistrationReferenceSequence` on a *running* DB (fixes
  create-user 500 · SqlException 208). A fresh DB needs nothing from it (the
  sequence is in the consolidated App `InitialCreate`), but it stays for an
  existing production database. **Not part of the seed run.**
- **`SIMF_App_D944_OrganisationOther_Hotfix.sql`** — a **prod-only** unblock
  adding `dbo.UserProfiles.OrganisationOther` and seeding the "Other"
  organisation on a *running* DB (D-944). A fresh DB needs nothing from it. It
  is the worked example of the trap below: the API deployed, the code-only half
  worked, and the schema half silently did not exist — so every
  `GET /app/account/user-profile` answered 500 on Invalid column name.
  **Not part of the seed run.**
- **`SIMF_App_AssistancePromptGrounding.sql`** / **`SIMF_App_AssistancePromptHistory.sql`**
  — idempotent one-shot updates that re-point an **already-seeded** `assistance`
  AI prompt at the grounded / history-carrying template. A freshly-seeded DB
  already has it (`IdentitySeeder`), so they update 0 rows there; they exist for
  databases seeded before that change. `SIMF-OPS-001` §"Existing databases and the
  grounded assistant prompt" instructs operators to run the first one. **Not part
  of the seed run.**

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

> **Dev / Test auto-copy (BUG-001, D-771).** Nobody performed that copy in
> Development or Testing, where `SqlContentSeeder` applies the SQL automatically —
> so every seeded speaker photo pointed at a storage key with no bytes and 404'd
> behind the UI's graceful placeholder (68+ failed image requests on a QA sweep).
> `SqlContentSeeder` now materialises the companion bytes through
> `IFileStorageProvider` right after it applies `SIMF_App_SpeakerPhotos.sql`, and
> **deactivates** any seeded row it cannot back with bytes — so a seeded asset
> reference either resolves or is gone and the surface shows its proper empty
> state. Idempotent (a row whose bytes are already on disk is untouched).
> **Production is unchanged** — it never runs `SqlContentSeeder`, so the manual
> copy step above is still required there. A new content file that seeds
> `StoredFile` rows must add itself to `SqlContentSeeder.CompanionFileBytes`
> alongside its byte folder, or the same defect returns.
