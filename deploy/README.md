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

All four packages deploy to **each of the two servers** — `Pre-production` and
`Production` — so there is one deployment job per server, and each package keeps
its own environment script on that server. The **mobile edge** is the presentation tier
for the mobile clients: a YARP reverse proxy published at `edge.simrsnf.com` that
forwards only `/api/v1/app/**` inward. See
[the mobile edge section](#the-mobile-edge-at-edgesimrsnfcom) before deploying
it: routing the app through it needs a mobile store release.

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
  `dotnet build -c Release` → `dotnet test` → `dotnet publish` each app (zipped)
  → publish artifact `drop`.

> ### ⚠️ The test gates are OFF by default (D-887)
>
> `runTests` defaults to **`false`**, so an ordinary run **does not test**. It
> skips the fast suites, `SIMF.Api.Tests`, the LocalDB provisioning that serves
> them, and the Flutter and BadgeDesk stages. The full list of what that covers,
> and what each one was catching, is in the `runTests` comment in
> [`azure-pipelines.yml`](../azure-pipelines.yml) — kept in one place so the two
> cannot drift apart.
>
> **A green run with `runTests` off means the code compiles and the packages
> publish. It says nothing about behaviour, permissions or the security
> surface.** Tick **Run the test gates (slow)** on for any run that gates a
> merge to `main`, and before a production publish — SIMF-OPS-001 §5 and the NCA
> Secure App-Dev Standard §3-20 both require a failing test to stop the
> pipeline, and neither is satisfied while it is off.
>
> The steps use a `condition:`, never `enabled: false`, so they stay listed as
> **skipped** in the run summary rather than disappearing from it.
- **Deploy to IIS** — **TWO** deployment jobs, one per Azure DevOps
  **Environment**, because the estate is two servers: **`Pre-production`** and
  **`Production`** (`SIMF APP 01`). Each server hosts all four sites, so each
  job checks out the repo, downloads `drop`, and runs the shared steps in
  [`deploy-all-packages.yml`](deploy-all-packages.yml) — four calls to
  [`pipeline-deploy-one.ps1`](pipeline-deploy-one.ps1), each extracting one zip
  and handing one site to [`iis-deploy.ps1`](iis-deploy.ps1), which stops the
  site + app pool, releases file locks, `robocopy /MIR`s the files, and
  restarts. Per-server order: **API**, then **CP**, then **Web**, then **Edge**
  last.
- The two jobs carry **no `dependsOn` between them**, so pre-production and
  production deploy **at the same time** off one build. Production does not wait
  on a pre-production rehearsal.

### Choosing which environments a run deploys to

Two tick boxes in the **Run pipeline** dialog, both **on** by default:

| Parameter | Label | Effect when unticked |
|-----------|-------|----------------------|
| `deployPreProduction` | Deploy to Pre-production | The pre-production job is omitted |
| `deployProduction` | Deploy to Production | The production job is omitted |

And one that is **off** by default:

| Parameter | Label | Default |
|-----------|-------|---------|
| `runTests` | Run the test gates (slow) | **`false`** |

Untick **both** and the whole `Deploy` stage is omitted — build, test and
publish still run and the `drop` artifact is still produced, so a build-only run
needs no separate pipeline.

They are `parameters`, not variables: only a parameter renders as a tick box in
the Run dialog, and only a parameter expands early enough to **omit** the job.
That distinction matters — a job skipped by a `condition:` still writes to its
environment's deployment history, so an untouched environment would show a
deployment that never happened.

> **A deployment job does not clone the repository.** Azure Pipelines states it
> outright: *"A deployment job doesn't automatically clone the source repo. You
> can check out the source repo within your job with `checkout: self`."* The
> deploy scripts live in the repository and the `drop` artifact carries only the
> published apps, so `deploy-all-packages.yml` opens with `checkout: self`.
> Without it every deploy step fails with "the term
> `…/deploy/pipeline-deploy-one.ps1` is not recognized".

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
secrets — those remain Machine-scope environment variables set on each server by
its own `set-env-{api|cp|web|edge}.ps1`, below.

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
    -EdgePort 12343
.\ops.ps1 -Action Install -Target Edge -CertThumbprint <thumbprint>
```

Each of the two servers hosts all four sites, so **`-Target All` is the normal
case** on both `Pre-production` and `Production` (D-886); the individual targets
remain for installing or restarting one site at a time. `ops.ps1` refuses to
install while `-ApiHost` and `-EdgeHost` name the same host, since two sites
cannot share a hostname — so the API and the edge need distinct hostnames on the
same box.

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
2. ~~**`environment` names**~~ — **no longer a placeholder.** The pipeline names
   the two real Azure DevOps **Environments**, `Pre-production` and
   `Production`; each must have its server registered as a VM resource so the
   deployment job runs on that machine.

   **Azure DevOps creates an environment on first reference.** A name here that
   matches no registered environment does not fail the run — it quietly adds an
   empty one to the portal. That is how the portal came to list four
   `SIMF-Prod-Api` / `-Cp` / `-Web` / `-Edge` entries nobody had created, under
   the earlier one-server-per-package reading of the estate. **Delete those four
   in Pipelines → Environments**; they hold no resources and no history worth
   keeping. `PipelineTestGateTests.The_pipeline_deploys_only_to_the_two_real_environments`
   now fails the build if a third name appears.

   **The names must match the portal exactly**, including case and the hyphen in
   `Pre-production`. If the registered names differ, change the two
   `environment:` blocks in `azure-pipelines.yml` **and** the `expected` array in
   that test together — the test pins the YAML to a reviewed list, and it cannot
   see the portal.

3. **Each server registered as a VM resource** inside its environment. Both jobs
   declare `resourceType: virtualMachine`, so the steps run **on that machine**
   rather than on the build agent:

   ```yaml
   environment:
     name: Production
     resourceType: virtualMachine
   ```

   This is not decoration. A bare `environment: Production` is also legal — as
   an abstract shell that only records deployment history — and then the steps
   run on the `pool` agent, which would have `iis-deploy.ps1` stop, mirror and
   restart IIS sites **on the build box** and report success. Register the VM
   with the script from **Pipelines → Environments → the environment → Add
   resource → Virtual machines**.

   A run reporting **no matching resources** means the machine is not registered.
   Register it; do not fall back to the bare form, which turns a loud failure
   into a deploy onto the wrong machine. Azure's documentation warns that
   `resourceType` is case-sensitive while its own examples disagree on the
   casing, so if the VM *is* registered and still does not match, try
   `VirtualMachine`.
4. **IIS site names + physical paths** — the `-ApiSiteName/-ApiPath`,
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

| Script | Server | Key groups |
|--------|--------|-----------|
| [set-env-api.template.ps1](set-env-api.template.ps1) | SimfAPI | The bulk, ~62 keys: `SIMF_ConnectionStrings__*`, `SIMF_Jwt__*`, `SIMF_FileStorage__*`, `SIMF_Email__*`, `SIMF_SuperAdmin__*`, `SIMF_Seed__DemoPassword`, `SIMF_Ai__*`, `SIMF_MeetingLinks__*`, `SIMF_Cors__WebAppOrigins__n`, `SIMF_RateLimit__*`, `SIMF_WalkInMode__*`, `SIMF_Swagger__*` |
| [set-env-cp.template.ps1](set-env-cp.template.ps1) | SimfCP | `SIMF_Api__BaseUrl`, `SIMF_Session__LifetimeHours`, `SIMF_DataProtection__KeyRingPath` |
| [set-env-web.template.ps1](set-env-web.template.ps1) | SimfWeb | `SIMF_Api__BaseUrl`, `SIMF_DataProtection__KeyRingPath` |
| [set-env-edge.template.ps1](set-env-edge.template.ps1) | SimfEdge | `SIMF_ReverseProxy__Clusters__api__Destinations__primary__Address`, `SIMF_ReverseProxy__KnownProxies__0` |
| [configure-prod-env.ps1](configure-prod-env.ps1) | any (`-Target`) | Generates the missing crypto keys, prompts for the rest, verifies, restarts the pool, health-checks |
| [clear-env.ps1](clear-env.ps1) | any (`-Target`) | Removes the Machine-scope `SIMF_*` secrets (keeps the shared non-secret config unless `-Full`) |

All four carry `ASPNETCORE_ENVIRONMENT` and `SIMF_Storage__LogDirectory`, because
every host reads both.

### One script per server - read this before deploying

The file count has moved twice, and the reasoning differs each time. Until
2026-08-06 there were three scripts, one per service, all running on one box:
they wrote to the same Machine-scope namespace and overlapped on several keys,
each noting "running both is fine, the last writer wins" - true only while the
copies agree. They were merged into one file, because one file cannot disagree
with itself.

On 2026-08-12 they split again, one file per package, on a reading of the estate
as one server per package - which would have removed the collision outright, a
variable set on the Website host not being visible on the API host. Keeping one
file would then have meant shipping the API's connection strings, SMTP password
and encryption keys to three servers that never read them.

**The estate is not one server per package (D-886).** It is **two** servers,
`Pre-production` and `Production`, each hosting all four sites - so all four
scripts run on the same box, and the last-writer problem the 2026-08-06 merge
solved would be back, were it not already solved a second way. What actually
keeps them apart now is the **prefix per application** below
(`SIMF_API_` / `SIMF_CP_` / `SIMF_WEB_` / `SIMF_EDGE_`): four scripts on one
machine write four separate namespaces and cannot overwrite each other, whatever
key names they share. Four files remain the right count for a different reason
than the one first given - each host still reads only its own application's
values, and an operator running one script can see exactly which application it
configures.

The keys that legitimately appear in more than one file are pinned by
`Shared_keys_agree_across_templates` in `DeploymentEnvTemplateTests`: same
value, same `Secret` flag, or the build fails. `Gate` is deliberately allowed to
differ, because it records whether **that** host refuses to start without the
value.

A deployment is therefore: **the pipeline publishes and deploys all four
packages to each server, an operator runs that server's four scripts, restart
the pools.**

Each filled form carries production values, so the repository tracks the four
**`.template.ps1`** files and **`.gitignore` deliberately ignores the filled
`set-env-{api,cp,web,edge}.ps1`** you create on each server:

```powershell
# on the API server
Copy-Item .\deploy\set-env-api.template.ps1 .\deploy\set-env-api.ps1
# fill the Secret entries in set-env-api.ps1 on the server, then run as Administrator
.\deploy\set-env-api.ps1
```

Every entry marked `Secret = $true` ships **empty**, and a test fails the build
if one is ever committed with a value. Non-secret settings that are identical on
every SIMF box (the environment name, the storage roots) ship **pre-filled**, so
an operator fills roughly a dozen secrets rather than sixty variables. Non-secret
settings that differ per site - public origins, proxy IPs, the SMTP host - are
marked `SITE-SPECIFIC` and also ship empty.

**Never delete a `.gitignore` entry for a filled `set-env-*.ps1` to make it
trackable - that commits live production credentials.** Edit the matching
template instead; it is the shared, reviewable copy. Each variable carries a
comment saying what breaks when it is missing, including the Production **boot
gates** that stop a host starting at all.

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
.\deploy\configure-prod-env.ps1 -Target Api               # this server's full pass
.\deploy\configure-prod-env.ps1 -Target Edge -VerifyOnly  # audit only, changes nothing
.\deploy\configure-prod-env.ps1 -Target Cp -SkipPrompts   # keys + verify, no prompts
```

**Pass `-Target`.** Each key and prompt declares which packages read it, and the
runbook asks only for that server's set. Unscoped it would ask an operator on the
Website host for a database connection string that host never reads, and then
write that credential into a machine with no reason to hold one. `-Target All`
remains for a single box that still runs everything.

### Naming — one prefix per application

Each application reads its **own** prefix, plus the ASP.NET Core
double-underscore convention:

| Application | Prefix | Example |
|---|---|---|
| SimfAPI | `SIMF_API_` | `SIMF_API_ConnectionStrings__SimfAppDb` → `ConnectionStrings:SimfAppDb` |
| SimfCP | `SIMF_CP_` | `SIMF_CP_Api__BaseUrl` → `Api:BaseUrl` |
| SimfWeb | `SIMF_WEB_` | `SIMF_WEB_Storage__LogDirectory` → `Storage:LogDirectory` |
| SimfEdge | `SIMF_EDGE_` | `SIMF_EDGE_ReverseProxy__KnownProxies__0` → `ReverseProxy:KnownProxies:0` |

**Why not one shared `SIMF_`?** Machine scope is shared by every process on a
box. While all four hosts read one common prefix, two SIMF applications on one
server could not be given different values for the same key: there was a single
`SIMF_Storage__LogDirectory` and a single `SIMF_Api__BaseUrl` between them,
shared whether that was wanted or not. The Control Panel's `Session:LifetimeHours`
and the API's `Session:TimeoutHours` sat in the same namespace under one section
name. A prefix per application removes that.

Be precise about what it is: **a naming boundary, not a security one.** Any
process on the box can still read any variable whatever it is called. The real
isolation is that each server receives only its own package's values, which is
what the four separate scripts deliver.

**Exception:** `ASPNETCORE_ENVIRONMENT` is host-level — read before any
configuration source loads — so it stays **un-prefixed** and is set identically
on every server.

Each script skips empty values (so an unedited run never sets blanks) and lists
which keys are `[REQUIRED]` / `[SECRET]`. Generate the secret keys per
SIMF-OPS-001 §B.3.

### Upgrading a server provisioned before 2026-08-12

This change is **not backward compatible**, deliberately: honouring the old
`SIMF_` names as a fallback would keep alive the very collision the split
removes. Each host therefore **refuses to start** when it finds pre-split
variables, naming them and the script that fixes it — rather than booting and
reporting an encryption key missing while the value sits there under its former
name.

Per server, in this order:

```powershell
# 1. Provision the new namespace (fill the template's copy first).
.\deploy\set-env-api.ps1            # or -cp / -web / -edge on that box

# 2. Remove the pre-split variables the host now refuses to start alongside.
.\deploy\clear-env.ps1 -Full

# 3. Restart that server's app pool.
.\deploy\ops.ps1 -Action Restart -Target Api
```

Step 2 after step 1, not before: clearing first leaves the box with no
configuration at all if step 1 is interrupted.

## The mobile edge at `edge.simrsnf.com`

The edge is the presentation tier for the mobile clients: a YARP reverse proxy
that publishes only `/api/v1/app/**` and forwards it inward, so the API can stop
being published to the internet.

It is served on its **own** name. `api.simrsnf.com` stays with the API and is
reserved for it, resolving inside the estate only.

**This needs a mobile app release.** `build_config.dart` compiles the base URL in
(`String.fromEnvironment`, default `https://api.simrsnf.com/api/v1`), so an
installed app talks to the API directly and knows nothing about the edge. Routing
mobile traffic through it means rebuilding with `--dart-define` pointing at
`edge.simrsnf.com` and shipping to both stores. **Withdrawing the API's public DNS
record and shipping that release have to land together**, or the installed app has
nothing to reach in between.

**Addressing.** Hostnames, never raw IPs: every certificate bypass was removed on
2026-08-08, so the API's certificate has to validate and no public CA issues one
for a bare address.

| | Name | Internet? | Hosting |
|---|---|---|---|
| Edge (tier 1) | `edge.simrsnf.com` | yes, via the WAF/LB | IIS site `SIMF.EDGE` |
| API (tier 2) | `api.simrsnf.com`, internal to the estate | no | IIS site `SIMF.API` |

**Two certificates are needed** and the second is the one that gets forgotten:
a public certificate for `edge.simrsnf.com`, and one for `api.simrsnf.com` so the
edge, the Control Panel and the Website can all validate the API.

**What the edge does NOT serve.** It publishes exactly one route,
`/api/v1/app/**`. The Control Panel calls `/api/v1/admin/**`, which the edge has
no route for, so `SIMF_Api__BaseUrl` points CP and Web at `api.simrsnf.com`
directly. They are server-side callers in the presentation zone and never
traverse the public front door.

**Availability.** Run the edge on **two nodes**, not one. Once the app is pointed
at it, it carries 100% of mobile traffic, so a single instance turns a four-node
API into a single point of failure. It is a stateless proxy, so a second node
needs no affinity and no shared state.

**Order of operations.**

```powershell
# 1. On the edge server, as Administrator.
.\ops.ps1 -Action Install -Target Edge -CertThumbprint <thumbprint>

# 2. Fill in set-env-edge.ps1 on that server and run it as Administrator.
#    BOTH edge variables are BOOT GATES - it refuses to start without them.
#      SIMF_ReverseProxy__Clusters__api__Destinations__primary__Address = https://api.simrsnf.com/
#      SIMF_ReverseProxy__KnownProxies__0                               = the WAF / load balancer address

# 3. Deploy this package to that server.
.\pipeline-deploy-one.ps1 -Package edge -ZipName SIMF.MobileEdge.zip `
    -SiteName SIMF.EDGE -SitePath D:\System\v1.0.1\edge -Drop <drop> -Root <root>
```

4. Publish `edge.simrsnf.com` in DNS and bind its certificate to the edge site.
5. Firewall: presentation to application on 443; application to data on 1433 and
   445. Resolve the key-ring rule noted against `SIMF_DataProtection__KeyRingPath`
   in the CP and Web templates first, or neither host boots.
6. Ship the mobile release built against `edge.simrsnf.com`.
7. Only then withdraw the API's public DNS record.

**Rollback** before step 7 costs nothing: the installed app is still reaching the
API directly, so removing the edge affects no client. After step 7 it is a DNS
change, restoring the API's public record.

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
