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

Run from the repo root, in this order. `SpeakerPhotos` must run **after**
`Speakers` (it points at those speaker rows). Add `-S <server>` if not the
default local instance.

```powershell
sqlcmd -d SIMF_App -i docs\migrations\2026\SIMF_App_SeedGaps.sql
sqlcmd -d SIMF_App -i docs\migrations\2026\SIMF_App_Speakers.sql
sqlcmd -d SIMF_App -i docs\migrations\2026\SIMF_App_SpeakerPhotos.sql
# then copy docs\migrations\2026\speaker-photos\speakerphoto  →  C:\SIMF\Storage\files\speakerphoto
```

## Files

| File | Seeds | Decision |
|------|-------|----------|
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
