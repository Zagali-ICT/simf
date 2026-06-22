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
| [set-env-api.ps1](set-env-api.ps1) | SimfAPI | `SIMF_ConnectionStrings__*`, `SIMF_Jwt__SigningKey`, `SIMF_Email__*`, `SIMF_SuperAdmin__*`, `SIMF_Storage__*`, `SIMF_Ai__*`, `SIMF_ReverseProxy__KnownProxies__n`, `SIMF_RateLimit__*`, media/presentation/recording roots, `ASPNETCORE_ENVIRONMENT` |
| [set-env-cp.ps1](set-env-cp.ps1) | SimfCP | `SIMF_Api__BaseUrl`, `SIMF_Storage__LogDirectory`, `ASPNETCORE_ENVIRONMENT` |
| [set-env-web.ps1](set-env-web.ps1) | SimfWeb | `SIMF_Api__BaseUrl`, `SIMF_Storage__LogDirectory`, `ASPNETCORE_ENVIRONMENT` |

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
