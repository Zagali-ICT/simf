# SIMF — deploy, migrate, seed, sign in

A one-page runbook card. **The authoritative document is
[`SIMF-OPS-001 Deployment and Operations`](../../SIMF-OPS-001-Deployment-and-Operations.md)**
— where the two disagree, OPS-001 wins. Seeding detail: [`README.md`](README.md).

## 1. Deploy

Azure DevOps builds and publishes on every push to `main`. **Its test step is
disabled**, so verify locally before merging.

> The pipeline definition was silently emptied by a merge on 2026-08-02 and
> restored under D-871 — for those five days nothing built, published or deployed
> at all. If a push to `main` produces no run, check `azure-pipelines.yml` is
> non-empty before looking anywhere else.

| Surface | URL |
|---|---|
| API | `https://api.simrsnf.com` (health: `/health`) |
| Control Panel | `https://cp.simrsnf.com` |
| Website | `https://web.simrsnf.com` |
| App (web build) | `https://web.simrsnf.com` |

The deployed `appsettings.json` is **overwritten by every deploy** — all real
configuration comes from Machine-scope `SIMF_*` environment variables.

> **TLS is real now (D-872).** The hosts moved to `simrsnf.com` (D-868), which —
> unlike the old underscore hostnames — are valid public-CA subjects, and the
> server carries a proper certificate. So the old "visit `/health` once and accept
> the warning" step is **gone**, and so is the app's blanket TLS trust-all, which
> was finding **C2** and an NCA handover blocker.
>
> That makes a certificate problem a real outage rather than a warning: the app no
> longer accepts an untrusted certificate, so if one expires or a host is added
> without one, the app stops reaching the API. Renewal is now an operational
> commitment, and `test/repo/platform_projects_tracked_test.dart` fails the build
> if anyone reintroduces a bypass to work around it.

## 2. Migrate — automatic

The API runs `MigrateAsync()` on both contexts at startup, so **there is no
manual migration step**. Booting against an empty database creates and migrates
it. Verify:

```sql
SELECT MigrationId FROM SIMF_Data.dbo.__EFMigrationsHistory_App     ORDER BY MigrationId;
SELECT MigrationId FROM SIMF_Identity.dbo.__EFMigrationsHistory_Identity ORDER BY MigrationId;
```

Note the **per-context** history table names — there is no plain
`__EFMigrationsHistory`.

Lookups (countries, regions, permissions, roles, profile types) seed themselves
on boot. Content does not — that is step 3.

## 3. Seed content — by hand

```powershell
cd docs\migrations\2026
.\Run-AppSeeds.ps1 -Server "PROD\SQL01" -Database SIMF_Data
```

Then the step the runner deliberately does **not** do — the photo bytes:

```powershell
robocopy .\speaker-photos\speakerphoto C:\SIMF\Storage\files\speakerphoto /E
```

Without it every photo 404s while the `StoredFile` rows look perfectly healthy.

`-Database` is the **content** DB: `SIMF_Data` on the server, `SIMF_App` on dev.
Never `SIMF_Identity` — the runner refuses it. Safe to re-run.

## 4. Users and passwords

**No password is stored in this repository.** Every account below is created or
updated from Machine-scope environment variables on the server; the live values
belong in the git-ignored `deploy/set-env-*.local.ps1` (or your password store),
never in a tracked file.

| Account | Surface | Password comes from |
|---|---|---|
| `superadmin@simrsnf.com` | Control Panel only | `SIMF_SuperAdmin__TempPassword` |
| Demo/visitor accounts | App + Website | `SIMF_Seed__DemoPassword` |
| Everyone else | — | created in the CP, or self sign-up |

> **Production starts from an EMPTY database (owner decision, 2026-08-07).**
> The old databases are dropped and recreated, so the super-admin migration
> that D-868/D-869 described does not apply: with nothing pre-existing there is
> no second `Administrator` row to reconcile. The seeder creates exactly one
> super-admin from `SIMF_SuperAdmin__*` on first boot.
>
> The duplicate-detection in `IdentitySeeder` stays regardless — it guards any
> FUTURE change to `SuperAdmin:Email` against a database that already has one,
> which is a permanent sharp edge and not specific to this migration.

Setting or rotating the super-admin:

```powershell
[Environment]::SetEnvironmentVariable('SIMF_SuperAdmin__Email',        'superadmin@simrsnf.com','Machine')
[Environment]::SetEnvironmentVariable('SIMF_SuperAdmin__TempPassword', '<new-strong-password>',    'Machine')
[Environment]::SetEnvironmentVariable('SIMF_SuperAdmin__PasswordChangeRequired','false',            'Machine')
# then restart the API app pool
```

The API re-applies the super-admin on **any** boot when Email + TempPassword are
set, so this is also the account-recovery path.

> Identity's password policy rejects sequential characters — `Aa@12345` fails.
> The prefix must be `SIMF_`; an unprefixed `SuperAdmin__…` is ignored.

The API **refuses to start** in Production unless these are set:
`SIMF_Jwt__SigningKey`, `SIMF_Storage__UserIdDocumentEncryptionKey`,
`SIMF_FileStorage__EncryptionKey`, `SIMF_Ai__PromptHash__Secret`.

## 5. First sign-in to the Control Panel

`https://cp.simrsnf.com/login` — email + password, then **one** of:

- **The account already has an authenticator** → enter the 6-digit code.
- **It does not** → the CP withholds the session and forces enrolment: it shows a
  QR plus a manual key, you pair an authenticator, enter the code, and it then
  shows **10 recovery codes once**. Save them.

Both are normal. Setting `SIMF_SuperAdmin__TotpSecret` makes 2FA active from
creation; leaving it unset gives you the enrolment flow above.

Lost the authenticator? Use a recovery code, or reset the account through
`SIMF_SuperAdmin__*` + an app-pool restart.

## 6. If something breaks right after deploy

| Symptom | Cause | Fix |
|---|---|---|
| Sign-in succeeds, then every page bounces to `/login` within seconds; the CP console shows 401s on `/account/api/*` | The API rejected the token it had just minted (D-848). Fixed in code; before that fix it happened on any host not at UTC+03:00 | Deploy a build containing D-848. Check the host with `tzutil /g` — no env var fixes this, and raising `SIMF_Session__TimeoutHours` does nothing because a rejected token is rejected however long it was minted to live |
| Arabic garbled, or `Msg 2628 … would be truncated` while seeding | `sqlcmd` read the file as ANSI | Use `Run-AppSeeds.ps1`; keep the UTF-8 BOM on the seeds |
| `sqlcmd: 'C': Unknown Option` | server has pre-17 `sqlcmd` | Already handled — the runner probes for `-C` |
| Photos 404 but rows exist | photo bytes never copied | Step 3's `robocopy` |
| Uploaded files stop resolving | DB reset, storage keys now orphaned | Re-upload through the CP |
| Website 500 on `/app` | `web.config` `<rewrite>` needs URL Rewrite + ARR | Install both IIS modules |
