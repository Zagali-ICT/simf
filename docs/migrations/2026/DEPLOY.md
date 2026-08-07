# SIMF — deploy, migrate, seed, sign in

A one-page runbook card. **The authoritative document is
[`SIMF-OPS-001 Deployment and Operations`](../../SIMF-OPS-001-Deployment-and-Operations.md)**
— where the two disagree, OPS-001 wins. Seeding detail: [`README.md`](README.md).

## 1. Deploy

Azure DevOps builds and publishes on every push to `main`. **Its test step is
disabled**, so verify locally before merging.

| Surface | URL |
|---|---|
| API | `https://api.simrsnf.com` (health: `/health`) |
| Control Panel | `https://cp.simrsnf.com` |
| Website | `https://web.simrsnf.com` |
| App (web build) | `https://web.simrsnf.com` |

The deployed `appsettings.json` is **overwritten by every deploy** — all real
configuration comes from Machine-scope `SIMF_*` environment variables.

> **TLS action owed.** The hosts moved to `simrsnf.com` (D-868), which — unlike
> the old underscore hostnames — are valid public-CA subjects, so a real
> certificate is now obtainable (win-acme / Let's Encrypt). Until one is
> installed the certificate is still self-signed, and each browser must visit the
> API `/health` once and accept it or XHR fails with
> `ERR_CERT_AUTHORITY_INVALID` before CORS is even evaluated. Installing a real
> cert also clears the app's blanket TLS trust-all, which is an NCA handover
> blocker — see `SIMF-Security-Assessment-2026-06-20.md` H2/C2.

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

> **One-off, on any database created before D-868 (2026-08-07).** The super-admin
> address changed from `superadmin@zagali-ict.com`. The seeder finds the
> super-admin **by e-mail** and creates one when it does not find it, so booting
> the new build against an old database does **not** break sign-in — it leaves
> **two accounts, both `Administrator`, i.e. both holding the `perm:*` wildcard.**
> This was reproduced locally, not predicted.
>
> **`SET QUOTED_IDENTIFIER ON` first.** `AspNetUsers` carries a filtered index, so
> any write to it fails with *"DELETE failed because the following SET options
> have incorrect settings: 'QUOTED_IDENTIFIER'"* unless the option is on. SSMS
> sets it on by default; **`sqlcmd` does not** — pass `-I`, or run the `SET` line
> shown below. This was hit for real running the statement below, not predicted.
>
> Do it **before** the new build boots and you keep one account with its password
> and 2FA intact:
>
> ```sql
> SET QUOTED_IDENTIFIER ON;
> UPDATE SIMF_Identity.dbo.AspNetUsers
>    SET Email='superadmin@simrsnf.com', NormalizedEmail='SUPERADMIN@SIMRSNF.COM',
>        UserName='superadmin@simrsnf.com', NormalizedUserName='SUPERADMIN@SIMRSNF.COM'
>  WHERE NormalizedEmail='SUPERADMIN@ZAGALI-ICT.COM';
> ```
>
> **If it has already booted**, that UPDATE hits the unique index because both
> rows now exist. Delete the superseded one instead — every foreign key into
> `AspNetUsers` is `ON DELETE CASCADE`, so tokens, roles and device keys go with
> it:
>
> ```sql
> SET QUOTED_IDENTIFIER ON;
> DELETE FROM SIMF_Identity.dbo.AspNetUsers
>  WHERE NormalizedEmail='SUPERADMIN@ZAGALI-ICT.COM';
> ```
>
> Then sign in with `SIMF_SuperAdmin__TempPassword`. Verify exactly one row
> remains:
>
> ```sql
> SELECT Email FROM SIMF_Identity.dbo.AspNetUsers WHERE NormalizedEmail LIKE '%SUPERADMIN%';
> ```
>
> Both paths were executed against a real database carrying the duplicate: the
> DELETE reported `1 rows affected` and left exactly one row. A database created
> after D-868 is unaffected.

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
