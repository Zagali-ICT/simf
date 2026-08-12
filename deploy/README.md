# SIMF deployment (CI/CD)

This folder + the root [`azure-pipelines.yml`](../azure-pipelines.yml) are the
Azure DevOps CI/CD definition for SIMF. They build, test, and publish the four
SIMF web apps and deploy them to IIS, mirroring the V10 ERP pipeline.

| App | Project | Artifact zip | IIS (placeholder) |
|-----|---------|--------------|-------------------|
| SimfAPI | `src/Backend/SIMF.Api/SIMF.Api.csproj` | `api/SIMF.Api.zip` | site `SIMF.API`, path `D:\System\v1.0.1\api` |
| SimfCP | `src/ControlPanel/SIMF.ControlPanel/SIMF.ControlPanel.csproj` | `cp/SIMF.ControlPanel.zip` | site `SIMF.CP`, path `D:\System\v1.0.1\cp` |
| SimfWeb | `src/Website/SIMF.Web/SIMF.Web.csproj` | `web/SIMF.Web.zip` | site `SIMF.WEB`, path `D:\System\v1.0.1\web` |
| SimfEdge | `src/Edge/SIMF.MobileEdge/SIMF.MobileEdge.csproj` | `edge/SIMF.MobileEdge.zip` | site `SIMF.EDGE`, path `D:\System\v1.0.1\edge` |

The **mobile edge** is the presentation tier for the mobile clients: a YARP
reverse proxy that publishes only `/api/v1/app/**` and forwards it to the API on
its private address. See
[the cutover section](#the-mobile-edge-and-the-apisimrsnfcom-cutover) before
deploying it, because it changes what `SIMF_Api__BaseUrl` must be set to.

All four sites and the SQL Server are addressed by hostname, not by IP: every
certificate bypass was removed on 2026-08-08, so the API certificate has to
validate and no public CA issues one for a private address. Point DNS (or the
hosts file on the web server) at the estate's addresses instead.

The deploy root is VERSIONED (`v1.0.1`). Two consequences: the IIS sites' physical
paths must be repointed when the version changes, and nothing that must survive a
release may live under it - uploads and logs are configured outside this tree for
exactly that reason (`SIMF_FileStorage__RootPath`, `SIMF_Storage__LogDirectory`).

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
- **Deploy to IIS** — downloads `drop`, extracts the four zips, then runs
  [`iis-deploy.ps1`](iis-deploy.ps1) which stops each site + app pool, releases
  file locks, `robocopy /MIR`s the files, and restarts.

## Building a package locally (`publish.ps1`)

[`publish.ps1`](../publish.ps1) at the repository root builds the same four web
apps outside the pipeline, for a manual release or a handover package. It cleans
the old output, runs `dotnet clean` on each project (so a stale DLL cannot ship),
restores, then publishes each sequentially in `Release`, stopping at the first
failure — and on a failure it re-runs that publish verbosely so the real error is
visible rather than swallowed.

Output folders are named to match the `iis-deploy.ps1` contract, so the package
deploys with no repackaging step:

```powershell
.\publish.ps1
# -> publish\api  publish\cp  publish\web  publish\edge

.\deploy\iis-deploy.ps1 -ArtifactRoot .\publish `
    -ApiSiteName  "SIMF.API"  -ApiPath  "D:\System\v1.0.1\api"  `
    -CpSiteName   "SIMF.CP"   -CpPath   "D:\System\v1.0.1\cp"   `
    -WebSiteName  "SIMF.WEB"  -WebPath  "D:\System\v1.0.1\web"  `
    -EdgeSiteName "SIMF.EDGE" -EdgePath "D:\System\v1.0.1\edge"
```

`publish/` is git-ignored. There is no separate Worker output because the
background workers run in-process inside the API app pool (see below).

The Control Panel and Website need `ErrorOnDuplicatePublishOutputFiles=false` or
their publish fails on duplicate static assets. That property lives in **their
`.csproj`**, not in this script or the pipeline: it used to be passed as a
`-p:` flag by both, which meant any new way of publishing those projects
inherited the failure until someone remembered the flag. No caller passes it
now.

The script builds and packages **only**. It applies no configuration and no
secrets — those remain Machine-scope environment variables set on the server by
`set-env.ps1`, below.

## Operating the sites (`ops.ps1`)

[`ops.ps1`](ops.ps1) is the single entry point for installing, removing, and
controlling the four IIS apps and the background-worker tier on the server. Run
it **as Administrator**.

| Action | Effect |
|--------|--------|
| `Status` | Report each site + app-pool state |
| `Start` / `Stop` / `Restart` | Control the site + its application pool |
| `Install` | Create the app pool (No Managed Code) + site + HTTP binding if missing |
| `Uninstall` | Remove the site + app pool |

`-Target` selects the scope: `All` (default), `Api`, `Cp`, `Web`, `Edge`, or
`Workers`.

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
.\ops.ps1 -Action Install -Target All -ApiPort 12340 -CpPort 12341 -WebPort 12342 `
    -EdgePort 12343 -ApiHost api-int.simrsnf.local
```

Installing the edge requires `-ApiHost` to be the API's **private** name: the edge
takes `api.simrsnf.com`, and `ops.ps1` refuses to install while both still resolve
to the same hostname.

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
   `-CpSiteName/-CpPath`, `-WebSiteName/-WebPath` and `-EdgeSiteName/-EdgePath`
   arguments in the `Deploy to IIS` step. The IIS sites + app pools must already
   exist on the server (this script deploys files; it does not create sites) —
   create them with `ops.ps1 -Action Install`. The edge pair is optional in
   `iis-deploy.ps1`, so an estate without an edge leaves that zip unused.

## Secrets / production config — the `set-env-*.ps1` templates

Per SIMF-OPS-001 §6, production overrides and every secret are applied as
**Machine-scope environment variables** on the server by a per-service script —
**not** baked into the pipeline or committed with real values. The committed
templates here carry **empty values**; fill them on the server, run **as
Administrator**, then **restart the IIS app pool** so `w3wp` picks them up:

| Script | Service | Key groups |
|--------|---------|-----------|
| [set-env.template.ps1](set-env.template.ps1) | SimfAPI + SimfCP + SimfWeb | Every variable the deployment needs: `SIMF_ConnectionStrings__*`, `SIMF_Jwt__*`, `SIMF_FileStorage__*`, `SIMF_Storage__*`, `SIMF_Email__*`, `SIMF_SuperAdmin__*`, `SIMF_Seed__DemoPassword`, `SIMF_Ai__*`, `SIMF_MeetingLinks__*`, `SIMF_ReverseProxy__KnownProxies__n`, `SIMF_Cors__WebAppOrigins__n`, `SIMF_RateLimit__*`, `SIMF_WalkInMode__*`, `SIMF_Swagger__*`, `SIMF_Api__BaseUrl`, `SIMF_Session__LifetimeHours`, `ASPNETCORE_ENVIRONMENT` |
| [configure-prod-env.ps1](configure-prod-env.ps1) | SimfAPI (runbook) | Generates the missing crypto keys, prompts for the rest, verifies, restarts the pools, health-checks |
| [clear-env.ps1](clear-env.ps1) | all | Removes the Machine-scope `SIMF_*` secrets (keeps the shared non-secret config unless `-Full`) |

### One script for all four sites — read this before deploying

Until 2026-08-06 there were three scripts, one per service. They wrote to the
same Machine-scope namespace and overlapped on `ASPNETCORE_ENVIRONMENT`,
`SIMF_Api__BaseUrl`, `SIMF_Api__AllowSelfSignedCertificate` (since retired) and
`SIMF_Storage__LogDirectory`, each noting that "running both is fine, the last
writer wins". That holds only while the copies agree; edit one and the box
silently takes whichever ran last. They are now a single file, so a deployment
is: **the pipeline publishes, an operator runs one script, restart the pools.**

Its filled form carries every production secret, so the repository tracks
**`set-env.template.ps1`** and **`.gitignore` deliberately ignores
`set-env.ps1`**, which is the filled overlay you create on the server:

```powershell
Copy-Item .\deploy\set-env.template.ps1 .\deploy\set-env.ps1
# fill the Secret entries in set-env.ps1 on the server, then run as Administrator
.\deploy\set-env.ps1
```

Every entry marked `Secret = $true` ships **empty**, and a test fails the build
if one is ever committed with a value. Non-secret settings that are identical on
every SIMF box (the environment name, the loopback API URL, the storage roots)
ship **pre-filled**, so an operator fills roughly a dozen secrets rather than
sixty variables. Non-secret settings that differ per site — public origins,
proxy IPs, the SMTP host — are marked `SITE-SPECIFIC` and also ship empty.

**Never delete the `.gitignore` entry for `set-env.ps1` to make it
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

## The mobile edge and the `api.simrsnf.com` cutover

The edge exists so the API can stop being published. The installed Flutter app
compiles its base URL in (`https://api.simrsnf.com/api/v1`), so the public name
cannot change without a store release on both platforms; the edge therefore
**takes that name over** and forwards only the mobile surface inward.

**Addressing.** Hostnames, never raw IPs: every certificate bypass was removed on
2026-08-08, so the API's certificate has to validate and no public CA issues one
for a bare address.

| | Name | Internet? | Hosting |
|---|---|---|---|
| Edge (tier 1) | `api.simrsnf.com` | yes, via the WAF/LB | IIS site `SIMF.EDGE` |
| API (tier 2) | private, e.g. `api-int.simrsnf.local` | no | IIS site `SIMF.API` |

**Two certificates are needed** and the second is the one that gets forgotten:
the public certificate for `api.simrsnf.com` (moved to the edge site at cutover),
and an internal certificate for the API's private name, so the edge, the Control
Panel and the Website can all validate it.

**The trap.** The edge publishes exactly one route, `/api/v1/app/**`. The Control
Panel calls `/api/v1/admin/**`. If `SIMF_Api__BaseUrl` is left pointing at
`api.simrsnf.com` after the cutover, every admin page 404s while the mobile app
keeps working, which reads as "the CP is broken" rather than "the cutover is
half-done". CP and Web are server-side callers in the presentation zone: point
them at the API's **private** address and never through the public front door.

**Availability.** Run the edge on **two nodes**, not one. Once it owns the public
name it carries 100% of mobile traffic, so a single instance turns a four-node API
into a single point of failure. It is a stateless proxy, so a second node needs no
affinity and no shared state.

**Order of operations.**

```powershell
# 1. On the edge server (as Administrator). -ApiHost must be the API's PRIVATE
#    name; the script refuses to install if it still matches -EdgeHost.
.\ops.ps1 -Action Install -Target Edge -ApiHost api-int.simrsnf.local -CertThumbprint <thumbprint>

# 2. Fill in set-env.ps1 on each server, then run it as Administrator.
#    Both edge variables are BOOT GATES - it refuses to start without them.
#      SIMF_ReverseProxy__Clusters__api__Destinations__primary__Address = the API's private https address
#      SIMF_ReverseProxy__KnownProxies__0                               = the WAF / load balancer address
#      SIMF_Api__BaseUrl                                                = the same private API address (the CP fix)

# 3. Deploy, then restart the pools so w3wp picks up the machine variables.
.\iis-deploy.ps1 -ArtifactRoot .\publish -EdgeSiteName "SIMF.EDGE" -EdgePath "D:\System\v1.0.1\edge" ...
```

4. Move the `api.simrsnf.com` certificate binding to the edge site.
5. Firewall: presentation to application on 443; application to data on 1433 and
   445. Resolve the key-ring rule noted against `SIMF_DataProtection__KeyRingPath`
   in the template first, or the CP and Website will not boot.
6. **Repoint DNS** `api.simrsnf.com` at the edge, and unpublish the API.

**Rollback** is a DNS change: point `api.simrsnf.com` back at the API and
republish it. No app release is involved either way, which is the whole reason
for taking the public name.

Full component guide:
[`docs/deploy/SIMF-MobileEdge-Deploy.md`](../docs/deploy/SIMF-MobileEdge-Deploy.md).

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
the V10 ERP pipeline with three SIMF-specific adaptations: four web apps; no
SDK-package/local-feed machinery (SIMF has none); and an added test gate
(SIMF-OPS-001 §5). The formal `DECISIONS_LOG.md` entry belongs on the integration
branch that carries the log (`main` does not have it); add it there when this work
is merged up.
