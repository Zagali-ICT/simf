# SIMF deployment (CI/CD)

This folder + the root [`azure-pipelines.yml`](../azure-pipelines.yml) are the
Azure DevOps CI/CD definition for SIMF. They build, test, and publish the three
SIMF web apps and deploy them to IIS, mirroring the V10 ERP pipeline.

| App | Project | Artifact zip | IIS (placeholder) |
|-----|---------|--------------|-------------------|
| SimfAPI | `src/Backend/SIMF.Api/SIMF.Api.csproj` | `api/SIMF.Api.zip` | site `SIMF.API`, path `D:\SIMF\API` |
| SimfCP | `src/ControlPanel/SIMF.ControlPanel/SIMF.ControlPanel.csproj` | `cp/SIMF.ControlPanel.zip` | site `SIMF.CP`, path `D:\SIMF\CP` |
| SimfWeb | `src/Website/SIMF.Web/SIMF.Web.csproj` | `web/SIMF.Web.zip` | site `SIMF.WEB`, path `D:\SIMF\WEB` |

The **Flutter app's web build** (a static IIS site, proof of concept — D-376) is
published separately by [`app-web/publish-app-web.ps1`](app-web/publish-app-web.ps1)
with the API base compiled in; guide:
[`docs/deploy/SIMF-AppWeb-IIS-Deploy.md`](../docs/deploy/SIMF-AppWeb-IIS-Deploy.md).
It is not part of the .NET pipeline above.

## Pipeline shape

```
Build, Test & Publish ──▶ Deploy to IIS
```

- **Build, Test & Publish** — `dotnet restore` (nuget.org; no private feed) →
  `dotnet build -c Release` → `dotnet test` (a failing test stops the pipeline,
  per SIMF-OPS-001 §5) → `dotnet publish` each app (zipped) → publish artifact
  `drop`.
- **Deploy to IIS** — downloads `drop`, extracts the three zips, then runs
  [`iis-deploy.ps1`](iis-deploy.ps1) which stops each site + app pool, releases
  file locks, `robocopy /MIR`s the files, and restarts.

## Operating the sites (`ops.ps1`)

[`ops.ps1`](ops.ps1) is the single entry point for installing, removing, and
controlling the three IIS apps and the background-worker tier on the server. Run
it **as Administrator**.

| Action | Effect |
|--------|--------|
| `Status` | Report each site + app-pool state |
| `Start` / `Stop` / `Restart` | Control the site + its application pool |
| `Install` | Create the app pool (No Managed Code) + site + HTTP binding if missing |
| `Uninstall` | Remove the site + app pool |

`-Target` selects the scope: `All` (default), `Api`, `Cp`, `Web`, or `Workers`.

The 10 background workers run **in-process inside the API application pool**, so
`-Target Workers` maps to the API app: restarting the workers restarts the API
pool. Their live health is on the Control Panel "Background services" page
(`/admin/ops/services`, gated by `ServicesMonitor.View`) plus the `/health`
`workers` check, and their logs are written to their own `SIMF.Workers` folder
under `Storage:LogDirectory`. When the workers later move to a dedicated Windows
Service, only the `Workers` block in `ops.ps1` changes.

```
.\ops.ps1 -Action Status
.\ops.ps1 -Action Restart -Target Workers
.\ops.ps1 -Action Install -Target All -ApiPort 12340 -CpPort 12341 -WebPort 12342
```

TLS bindings and the CA certificate are configured separately (see the HLD /
SIMF-OPS-001); `Install` creates the HTTP binding only.

## ⚠️ Prerequisite — code must be on `main`

`main` currently holds **documentation only**; the application code lives on the
integration branches (`feature/login-api` → `feature/app-cp-api-split`). The
pipeline triggers on `main` and references `src/…` paths, so **a run on `main`
will fail at restore/build until that code is merged into `main`.** The YAML is
correct; it simply needs the code present on the trigger branch.

## Placeholders to confirm before the Deploy stage runs

These are **placeholders** — set them to the real SIMF server values:

1. **`pool` name** (in `azure-pipelines.yml`) — the self-hosted agent pool. The
   build agent needs the **.NET 10 SDK**; the deploy agent needs **IIS** + the
   `WebAdministration` PowerShell module and rights to stop/start sites. The
   build agent also runs the integration test gate, which hosts the API against
   **SQL Server LocalDB** (`(localdb)\MSSQLLocalDB`); the `Provision SQL Server
   LocalDB` pipeline step installs it on first run via Chocolatey (so the agent
   needs outbound access to `community.chocolatey.org` + the package source, and
   rights to install an MSI), then creates/starts the instance. Installing
   LocalDB on the agent once removes the per-run download.
2. **`environment` name** (`SIMF-Prod` placeholder) — register an Azure DevOps
   **Environment** of this name and bind it to the SIMF server.
3. **IIS site names + physical paths** — the `-ApiSiteName/-ApiPath`,
   `-CpSiteName/-CpPath`, `-WebSiteName/-WebPath` arguments in the
   `Deploy to IIS` step. The IIS sites + app pools must already exist on the
   server (this script deploys files; it does not create sites).

## Secrets / production config — the `set-env-*.ps1` templates

Per SIMF-OPS-001 §6, production overrides and every secret are applied as
**Machine-scope environment variables** on the server by a per-service script —
**not** baked into the pipeline or committed with real values. The committed
templates here carry **empty values**; fill them on the server, run **as
Administrator**, then **restart the IIS app pool** so `w3wp` picks them up:

| Script | Service | Key groups |
|--------|---------|-----------|
| [set-env-api.template.ps1](set-env-api.template.ps1) | SimfAPI | `SIMF_ConnectionStrings__*`, `SIMF_Jwt__*`, `SIMF_FileStorage__*`, `SIMF_Storage__*`, `SIMF_Email__*`, `SIMF_SuperAdmin__*`, `SIMF_Seed__DemoPassword`, `SIMF_Ai__*`, `SIMF_MeetingLinks__*`, `SIMF_ReverseProxy__KnownProxies__n`, `SIMF_Cors__WebAppOrigins__n`, `SIMF_RateLimit__*`, `SIMF_Swagger__*`, `ASPNETCORE_ENVIRONMENT` |
| [set-env-cp.ps1](set-env-cp.ps1) | SimfCP | `SIMF_Api__BaseUrl`, `SIMF_Storage__LogDirectory`, `ASPNETCORE_ENVIRONMENT` |
| [set-env-web.ps1](set-env-web.ps1) | SimfWeb | `SIMF_Api__BaseUrl`, `SIMF_Storage__LogDirectory`, `ASPNETCORE_ENVIRONMENT` |
| [configure-prod-env.ps1](configure-prod-env.ps1) | SimfAPI (runbook) | Generates the missing crypto keys, prompts for the rest, verifies, restarts the pools, health-checks |
| [clear-env.ps1](clear-env.ps1) | all | Removes the Machine-scope `SIMF_*` secrets (keeps the shared non-secret config unless `-Full`) |

### The API script is a TEMPLATE with a different name — read this before deploying

The API's variable list is the big one, and its filled form carries every
production secret. So the repository tracks **`set-env-api.template.ps1`**
(every value empty) and **`.gitignore` deliberately ignores `set-env-api.ps1`**,
which is the filled overlay you create on the server:

```powershell
Copy-Item .\deploy\set-env-api.template.ps1 .\deploy\set-env-api.ps1
# edit set-env-api.ps1 on the server, then run it as Administrator
.\deploy\set-env-api.ps1
```

**Never delete the `.gitignore` entry for `set-env-api.ps1` to make it
trackable — that commits live production credentials.** Edit the template
instead; it is the shared, reviewable copy. Each variable in the template
carries a comment saying what breaks when it is missing, including the three
Production **boot gates** (`SIMF_FileStorage__EncryptionKey`,
`SIMF_Storage__UserIdDocumentEncryptionKey`, `SIMF_Ai__PromptHash__Secret`) that
stop the API starting at all.

### First-time provisioning — `configure-prod-env.ps1`

[`configure-prod-env.ps1`](configure-prod-env.ps1) is the runbook for a fresh
server. Run it **as Administrator**; it is safe to re-run.

1. **Generates** a cryptographically-random base64 32-byte AES key
   (`RandomNumberGenerator`) for each key that is not already set, and writes it
   straight to the Machine environment **without printing it**.
2. **Never overwrites an existing encryption key.** Rotating
   `FileStorage:EncryptionKey` makes every previously stored file
   undecryptable, and rotating `Storage:UserIdDocumentEncryptionKey` strands
   every encrypted PII column — so the script warns loudly and skips. There is
   no `-Force`: a genuine rotation needs a decrypt-and-re-encrypt migration.
3. **Prompts** for the values it cannot generate (connection strings, SMTP
   credentials, the public Website origin) using `Read-Host -AsSecureString`, so
   nothing is echoed.
4. **Verifies**, reporting each key's **name** and whether it is set — never a
   value.
5. **Restarts** the IIS app pools and **health-checks** the API.

```powershell
.\deploy\configure-prod-env.ps1                # full provisioning pass
.\deploy\configure-prod-env.ps1 -VerifyOnly    # audit only, changes nothing
.\deploy\configure-prod-env.ps1 -SkipPrompts   # keys + verify, no prompts
```

Naming uses the **`SIMF_` project prefix** + the ASP.NET Core double-underscore
convention (`SIMF_Section__Key`). Each app registers
`AddEnvironmentVariables("SIMF_")` (branch `feature/env-var-prefix`), which
strips the prefix, so `SIMF_ConnectionStrings__SimfAppDb` binds to
`ConnectionStrings:SimfAppDb`. **Exception:** `ASPNETCORE_ENVIRONMENT` is
host-level (read before configuration sources load) and stays **un-prefixed**.
Each script skips empty values (so an unedited run never sets blanks) and lists
which keys are `[REQUIRED]` / `[SECRET]`. Generate the secret keys per
SIMF-OPS-001 §B.3. Machine-scope variables are shared across all apps on the box
(so `SIMF_Api__BaseUrl` / `ASPNETCORE_ENVIRONMENT` are common to CP + Web).

## Out of scope here (handled elsewhere)
- **Database migrations.** Applied **in-process at API startup** — `Program.cs`
  runs `SimfAppDbContext` then `SimfIdentityDbContext` `MigrateAsync` (App before
  Identity, SIMF-OPS-001 §B.2). No EF step in the pipeline.
- **`web.config`.** Generated by `dotnet publish` for Web SDK projects
  (`AspNetCoreModuleV2`, in-process). Not hand-authored.
- **NCA security pre-flight + smoke test.** See SIMF-OPS-001 §B.7 / §B.8 — the
  gate before production go-live.

## Decision record

This pipeline was added on branch `feature/cicd-pipeline` (off `main`), mirroring
the V10 ERP pipeline with three SIMF-specific adaptations: three web apps; no
SDK-package/local-feed machinery (SIMF has none); and an added test gate
(SIMF-OPS-001 §5). The formal `DECISIONS_LOG.md` entry belongs on the integration
branch that carries the log (`main` does not have it); add it there when this work
is merged up.
