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

All four packages deploy to each of the two servers — `SIMF-Prod`
(pre-production) and `SIM-RNSF` (production) — so there is one deployment job per server,
and each package keeps its own environment script on that server. Each job is bound to its
environment's registered **VM resource** (D-938), and that binding is what makes two jobs
two destinations. Until 2026-08-19 no usable resource was registered, both jobs fell back
to the single `Default` pool agent, and every deploy landed on pre-production while
reporting success under either name (D-932 — see *Choosing which environments a run
deploys to*). The **mobile edge** is the presentation tier
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
published separately by [`set-env-webapp.ps1`](set-env-webapp.ps1)
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
  **Environment**, because the estate is two servers: **`SIMF-Prod`**
  (pre-production) and **`SIM-RNSF`** (production, `SIMF APP 01`). Each server
  hosts all four sites, so each job downloads `drop` and runs the shared steps in
  [`deploy-all-packages.yml`](deploy-all-packages.yml) — one step per package,
  each extracting its zip, stopping the site + app pool, releasing file locks,
  `robocopy /MIR`ing the files and restarting. Per-server order: **API**, then
  **CP**, then **Web**, then **Edge** last.
- That logic is **inline in the YAML**, not in a script. It used to live in
  `pipeline-deploy-one.ps1` calling `iis-deploy.ps1`; both were deleted so
  `deploy/` holds only the five `set-env-*.ps1` and `clean-env.ps1`. The
  sequence is unchanged, and the artifact no longer has to carry scripts.
- **Production waits for pre-production.** They originally had no `dependsOn` and
  were meant to deploy simultaneously, but a self-hosted organisation typically
  has **one parallel job slot**: the second job took an agent, initialised, then
  sat waiting for a slot that only freed when the first finished. A run that
  appears to hang after *Initialize job* with no step output is that (D-904).
  Sequencing costs nothing that was really being had, and pre-production now
  genuinely rehearses first. The `dependsOn` is conditional — on a
  production-only run `DeployPreProduction` is never emitted, and naming a
  missing job fails the pipeline at compile time.

### Choosing which environments a run deploys to

| Parameter | Label | Default |
|-----------|-------|---------|
| `deployPreProduction` | Deploy to Pre-production (`simf.zagali-ict.com`) | `true` |
| `deployProduction` | Deploy to Production (`web` / `cp` / `api.simrsnf.com`) | `true` |
| `preProductionMachineName` | *(not shown — the machine the pre-prod job asserts it landed on)* | `WIN-MAP9VAMAU4Q` |
| `productionMachineName` | *(not shown — same, for production)* | *empty, see below* |

Untick both and the whole Deploy stage is omitted: build and publish still run and
the artifact is still produced, so a build-only run needs no separate pipeline.

The stage also refuses to run on a **pull-request** build
(`ne(variables['Build.Reason'], 'PullRequest')`). Both parameters default to `true`,
so without that a validation build would deploy both servers from an unmerged branch.
Excluding the PR *reason* rather than pinning `refs/heads/main` keeps a deliberate
manual run from a branch working, which is how a hotfix gets rehearsed.

> ### ⚠️ For eleven days both jobs deployed to the same machine (D-932 → fixed by D-938)
>
> Kept because the failure was **silent**, and the shape of it will recur if the
> VM resources are ever removed.
>
> The `Default` pool holds a single agent — `server` on `WIN-MAP9VAMAU4Q`.
> D-906 recorded that machine as the **production** box (`SIMF APP 01`).
> **It is not.** It is the **pre-production** server, the one behind
> `simf.zagali-ict.com`. With no VM resource registered on either environment,
> both deployment jobs fell back to that one pool agent, so **every deploy —
> under either environment name, with either tick box — landed on
> pre-production**, and every one of them reported success.
>
> **How this was established:** the `Initialize job` step of *both* deployment
> jobs prints `Agent machine name: 'WIN-MAP9VAMAU4Q'`; the pool holds exactly one
> agent; deploys write `D:\System\v1.0.1\web` on that machine and the site whose
> content changes is `simf.zagali-ict.com`, while production stayed byte-identical
> (its `landing.css` still MD5s to `b98f39d41ff1b73ae3ac4e3db3e9179f` — commit
> `f81ba630`, 2026-08-08). They are also two machines on two networks:
> `*.simrsnf.com` resolves to `95.177.163.108`, `simf.zagali-ict.com` to
> `173.201.37.122`.
>
> **What fixed it:** the servers are registered as VM resources, and each job now
> binds to its own with `resourceType: virtualMachine`. See *Do NOT put a `pool:`
> on a deployment job* below.
>
> **What keeps it fixed:** the first step of every deploy is
> `Confirm the deployment target`, which prints the machine and the IIS site
> inventory *before* the artifact is downloaded or any site stopped, and throws if
> the machine is not the one the job pinned. `productionMachineName` starts
> **empty** on purpose — nobody has seen that server's name, because this pipeline
> has never run on it. Empty means *print, do not assert*: the first production
> deploy logs the real name, and copying it into `azure-pipelines.yml` closes the
> pin.
>
> **Empty is not unguarded.** Each job is also handed the *other* environment's
> name as **forbidden**, so the production job refuses to run on
> `WIN-MAP9VAMAU4Q` even with its own pin blank. That is the case worth covering:
> an environment whose VM resource was registered by running the *Add resource*
> script on whichever box happened to be open points at **that** box, and the job
> would otherwise deploy the wrong estate under the right name.
>
> **And the path is checked too.** A site that exists but still serves the
> previous version's folder would take the mirror, start clean and report success
> while serving old content. Each package asserts the site's `PhysicalPath` equals
> the path it is about to write, before it stops anything.

And three that are **off** by default:

| Parameter | Label | Default | Notes |
|-----------|-------|---------|-------|
| `runTests` | Run the test gates (slow) | **`false`** | The .NET suites + LocalDB |
| `runMobileApp` | Run the Flutter app stage | **`false`** | Independent of `runTests` |
| `runBadgeDesk` | Run the offline badge desk stage | **`false`** | Independent of `runTests` |

The last two are **disabled, not deferred** (D-889): ticking `runTests` on for a
merge-gating run does **not** bring them back — each needs its own box. Neither
was ever a `Deploy` dependency, so neither blocked a deployment; on the single
self-hosted `Default` agent they competed for it, which is the wall-clock this
buys back.

Each is the **only** signal of its kind, so know what stops being checked:

- **MobileApp** — the only CI proof the Flutter app analyses, passes its suites,
  and still **compiles for Android from a clean checkout**. That last gate exists
  because of a real escape: a `.gitignore` rule for build output also matched a
  Pigeon source shipping with the vendored video plugin, so `flutter build apk`
  failed on a clean clone while every developer machine stayed green. `analyze`
  and `test` are blind to it — the Android half of a federated plugin is never
  compiled on the host VM.
- **BadgeDesk** — the only build signal for `SIMF.BadgeDesk`, which sits outside
  `SIMF.slnx` deliberately (Windows-only: WinForms + DPAPI + native printing) and
  references `SIMF.Common` and `SIMF.Contracts`. Renaming `EventBadgeCodec`,
  `OfflineBadgeId` or `OfflineBadgeRegistration` now breaks the only tool that
  mints badges, with nothing reporting it until a desk fails to open at the venue.

Untick **both** and the whole `Deploy` stage is omitted — build, test and
publish still run and the `drop` artifact is still produced, so a build-only run
needs no separate pipeline.

They are `parameters`, not variables: only a parameter renders as a tick box in
the Run dialog, and only a parameter expands early enough to **omit** the job.
That distinction matters — a job skipped by a `condition:` still writes to its
environment's deployment history, so an untouched environment would show a
deployment that never happened.

> **A deployment job does not clone the repository.** Azure Pipelines states it
> outright: *"A deployment job doesn't automatically clone the source repo."*
> That is why the stop / mirror / start sequence is written **inline** in
> `deploy-all-packages.yml` and calls no file from the repo.
>
> Two earlier shapes are worth knowing so they are not reintroduced. The first
> used `checkout: self`, which pulled the whole repository and its history onto
> each production server every run to obtain 12 KB of PowerShell. The second
> staged `pipeline-deploy-one.ps1` and `iis-deploy.ps1` into the artifact by
> name — never with a `deploy/*.ps1` wildcard, because the `set-env-*.ps1` in
> that folder hold production secrets and must never reach a build artifact.
> Inlining removes both problems: the drop carries published applications and
> nothing else.

## Building a package locally (`publish.ps1`)

[`publish.ps1`](../publish.ps1) at the repository root builds the same four web
apps outside the pipeline, for a manual release or a handover package. It cleans
the old output, runs `dotnet clean` on each project (so a stale DLL cannot ship),
restores, then publishes each sequentially in `Release`, stopping at the first
failure — and on a failure it re-runs that publish verbosely so the real error is
visible rather than swallowed.

Output folders are named per package, matching the layout the pipeline expects:

```powershell
.\publish.ps1
# -> publish\api  publish\cp  publish\web  publish\edge
```

There is no longer a companion deploy script to point at that output —
`iis-deploy.ps1` was deleted along with the rest, and the stop / mirror / start
sequence now lives inline in `deploy-all-packages.yml`. For a **manual** release,
copy each folder over its site's physical path (`D:\System\v1.0.1\{api,cp,web,edge}`)
with the site and its app pool stopped, then start them again.

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

## Operating the sites

`ops.ps1` was deleted with the rest of the extra scripts. **Creating an IIS site
or app pool is now a manual IIS Manager job**, and so are start / stop / status.
Know what that costs before the next fresh server:

- Each of the two servers hosts **all four sites**, so a new box needs four app
  pools (No Managed Code) and four sites created by hand.
- The **API and the edge need distinct hostnames** on the same box — two sites
  cannot share one. `ops.ps1` used to refuse the mistake; nothing does now.
- TLS bindings and the CA certificate are configured separately (see the HLD /
  SIMF-OPS-001).

Routine restarts are covered: each `set-env-*.ps1` restarts its own app pool at
the end, and a deploy stops and starts each site itself.

The 10 background workers run **in-process inside the API application pool**, so
restarting the workers means restarting the API pool. Their live health is on the
Control Panel "Background services" page (`/admin/ops/services`, gated by
`ServicesMonitor.View`) plus the `/health` `workers` check, and their logs go to
their own `SIMF.Workers` folder under `Storage:LogDirectory`.

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
   the two real Azure DevOps **Environments**, both of which exist with their
   server registered as a VM resource:

   > ### ⚠️ The names lie — do not "correct" them
   >
   > | Environment | Is actually | Server |
   > |---|---|---|
   > | **`SIMF-Prod`** | **PRE-production** | the pre-production box |
   > | **`SIM-RNSF`** | **PRODUCTION** | `SIMF APP 01` |
   >
   > The one that reads like production is not. Anyone who "fixes" this mapping
   > on sight deploys straight to production believing they are rehearsing.
   > Trust the job names and `displayName`s in `azure-pipelines.yml`, not the
   > environment string. Pinned by `PipelineTestGateTests` (D-896).

   **A missing environment cannot be created by the pipeline.** Azure DevOps
   auto-creates an environment named by a pipeline **only when the YAML was
   edited in the Azure Pipelines web editor**, because only then does it know
   which user to attribute the new environment to. This repository is edited
   locally and pushed, so a missing environment instead fails the run with
   *"Environment X could not be found. The environment does not exist or has not
   been authorized for use."*

   That asymmetry explains the four stray `SIMF-Prod-Api` / `-Cp` / `-Web` /
   `-Edge` entries in the portal: they were created by a web-editor save, not by
   anyone deliberately registering them. **Delete those four in Pipelines →
   Environments** — they hold no resources and no history worth keeping.

   `PipelineTestGateTests.The_pipeline_deploys_only_to_the_two_real_environments`
   fails the build if a name outside the reviewed set appears, so the mistake
   surfaces at build time rather than as either a portal mess or a failed deploy.

   **The names must match the portal exactly**, including case. They are
   `SIMF-Prod` and `SIM-RNSF` — not `Pre-production`/`Production`, which were
   assumed once and broke every deploy until corrected (D-896). If they ever
   change, edit the two `environment:` values in `azure-pipelines.yml` **and**
   the `expected` array in that test together — the test pins the YAML to a
   reviewed list and cannot see the portal.

3. **Both servers are registered as VM resources**, so the deployment steps run
   on the machine rather than on the build agent. To re-register one, or to add
   a third: the environment → **Add resource** → **Virtual machines**, copy the
   script, run it **as Administrator on that server**, then confirm the machine
   appears on the environment's **Resources** tab.

   The script embeds a PAT that expires three hours after it is generated; if it
   lapses, reopen the environment and select **Add resource** for a fresh one.

   > **Both SIMF servers also run a `Default` pool agent.** An environment VM
   > resource is a **second** agent on that box, so the script prompts for an
   > agent name and it must be **unique** — reusing the pool agent's name
   > collides. The two are separate on purpose: the pool agent runs `Build`, the
   > environment agent runs that server's deployment job.

   If the environment exists but the run still cannot find it, it is the second
   half of the error message: **Security → Pipeline permissions** on that
   environment, and authorize this pipeline.

   **The environments are no longer empty shells, and that is what fixed the
   routing.** Until 2026-08-19 neither had a usable VM resource. An environment
   with no resource is what Azure calls an *"abstract shell to record deployment
   history"*, so the steps fell back to the **`Default` pool agent** — `server` on
   `WIN-MAP9VAMAU4Q` — and both jobs deployed to **pre-production** (D-932).

   **`resourceType: virtualMachine` is now used, and that reverses the warning
   this file used to carry.** It was tried twice *before* the resources existed
   and broke the deploy both times (D-903, D-905) with *"No resource were found in
   the environment with ID 3"*. That was the construct being right and the estate
   not being ready — not the construct being wrong. It is ready (D-938).

   If a job fails with that message again, a resource has been removed from that
   environment: fix it on the environment's **Resources** tab. Re-register with
   the environment → **Add resource** → **Virtual machines**, copy the script, run
   it **as Administrator on that server** with a **unique** agent name, then
   confirm the machine appears on the Resources tab. The agent polls **outbound
   over 443** and needs no inbound port opened — which is the only reason
   production is reachable at all, since it exposes nothing but 443 and 3389 (no
   Web Deploy on 8172, no FTP, no WinRM).

   The agent's Windows service must log on as an account in the **local
   Administrators** group. Without it the deploy throws on its first package:
   reading IIS configuration needs elevation, and the agent installs as a
   non-admin service by default.

   **Do NOT put a `pool:` on a deployment job.** A job's pool overrides its
   environment's VM resource, so the job returns to the shared agent and both
   jobs deploy to the same machine again — the exact defect above, and it reports
   success while doing it. `PipelineTestGateTests` fails the build on an indented
   `pool:` and on a deployment job missing its `resourceType`.

4. **IIS site names + physical paths** — the `packages` list and `sitePathRoot`
   parameter at the top of [`deploy-all-packages.yml`](deploy-all-packages.yml).
   The IIS sites + app pools must **already exist** on the server: the deploy
   copies files, it does not create sites, and `ops.ps1` (which used to create
   them) is gone. Create them in IIS Manager before the first deploy.

## Secrets / production config — the `set-env-*.ps1` scripts

Per SIMF-OPS-001 §6, production overrides and every secret are applied as
**Machine-scope environment variables** on the server by a per-service script —
**not** baked into the pipeline or committed with real values. As of 2026-08-22
the five `set-env-*.ps1` are **not in the repository at all** (see the box
below) — the operator holds them. Run **as Administrator**, then **restart the
IIS app pool** so `w3wp` picks them up:

| Script | Server | Key groups |
|--------|--------|-----------|
| [set-env-api.ps1](set-env-api.ps1) | SimfAPI | The bulk, 62 keys: `SIMF_API_ConnectionStrings__*`, `SIMF_API_Jwt__*`, `SIMF_API_FileStorage__*`, `SIMF_API_Email__*`, `SIMF_API_SuperAdmin__*`, `SIMF_API_Seed__DemoPassword`, `SIMF_API_Ai__*`, `SIMF_API_MeetingLinks__*`, `SIMF_API_Cors__WebAppOrigins__n`, `SIMF_API_RateLimit__*`, `SIMF_API_WalkInMode__*`, `SIMF_API_Swagger__*` |
| [set-env-cp.ps1](set-env-cp.ps1) | SimfCP | `SIMF_CP_Api__BaseUrl`, `SIMF_CP_Session__LifetimeHours`, `SIMF_CP_DataProtection__KeyRingPath` |
| [set-env-web.ps1](set-env-web.ps1) | SimfWeb | `SIMF_WEB_Api__BaseUrl`, `SIMF_WEB_DataProtection__KeyRingPath` |
| [set-env-edge.ps1](set-env-edge.ps1) | SimfEdge | `SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address`, `SIMF_EDGE_ReverseProxy__KnownProxies__0` |
| [set-env-webapp.ps1](set-env-webapp.ps1) | Flutter web bundle | Compiled in by `flutter build web`, not environment variables: `ApiBase` (the EDGE), `OutDir`, optional app key / support contacts / social links |
| [clean-env.ps1](clean-env.ps1) | any (`-Target`) | Removes the Machine-scope `SIMF_*` secrets (keeps the shared non-secret config unless `-Full`) |

All four carry `ASPNETCORE_ENVIRONMENT` and a log directory, because every host
reads both — but the log key is per-host like the rest: `SIMF_API_`,
`SIMF_CP_`, `SIMF_WEB_` and `SIMF_EDGE_` each prefix their own
`Storage__LogDirectory`.

**The prefix is per host, and a bare `SIMF_` one binds to nothing.** The names
above were written before the split and are the actual keys, verbatim from the
templates. Setting the pre-split form leaves the host reading its built-in
default instead — for the API that means no connection string and a boot
failure, which reads as a broken deployment rather than as a mistyped variable.
`clear-env.ps1` is the one place a bare `SIMF_*` is still right: it sweeps the
whole namespace on purpose, and knows all four prefixes.

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
`SIMF-Prod` and `SIM-RNSF`, each hosting all four sites - so all four
scripts run on the same box, and the last-writer problem the 2026-08-06 merge
solved would be back, were it not already solved a second way. What actually
keeps them apart now is the **prefix per application** below
(`SIMF_API_` / `SIMF_CP_` / `SIMF_WEB_` / `SIMF_EDGE_`): four scripts on one
machine write four separate namespaces and cannot overwrite each other, whatever
key names they share. Four files remain the right count for a different reason
than the one first given - each host still reads only its own application's
values, and an operator running one script can see exactly which application it
configures.

The two Blazor hosts are pinned to agree on `Api__BaseUrl` and
`DataProtection__KeyRingPath` by
`The_blazor_hosts_agree_on_the_settings_that_must_match` in
`DeploymentEnvTemplateTests` — same value, or the build fails. `Gate` is
deliberately allowed to differ, because it records whether **that** host refuses
to start without the value.

A deployment is therefore: **the pipeline publishes and deploys all four
packages to each server, an operator runs that server's scripts, and each script
restarts its own pool.**

```powershell
# on the server, as Administrator
.\deploy\set-env-api.ps1
.\deploy\set-env-cp.ps1
.\deploy\set-env-web.ps1
.\deploy\set-env-edge.ps1
```

> ### These five files are NOT in the repository — the operator holds them
>
> `set-env-{api,cp,web,edge,webapp}.ps1` **are** the operator's filled scripts,
> holding the real connection strings, the JWT signing key, both encryption
> keys, the SMTP password and the AI keys. They were tracked for a period at
> the owner's instruction; that was **reversed on 2026-08-22**. They are now
> `.gitignore`d and untracked, so a fresh clone does not contain them and the
> links above resolve only on a machine that already has them.
>
> **Untracking is not rotation.** Every secret they carried is still readable in
> git history, permanently. Rotate anything that reached a remote you do not
> control — that is the step that closes the exposure.
>
> **Consequence to know:** `clean-env.ps1` reads `set-env-$Target.ps1` from its
> own directory to learn which variables to sweep, so its scoped mode
> (`-Target Api`) throws on a machine without these files. Its `-Target All`
> path is unaffected. If a fresh clone ever needs to run scoped, track
> `set-env-*.template.ps1` files carrying the key names with empty values and
> point that lookup at them — deliberately not done here, because it re-adds a
> committed file whose whole purpose is to mirror the secret one.

Each variable carries a comment saying what breaks when it is missing, including
the Production **boot gates** that stop a host starting at all. Five entries are
deliberately blank, because blank is their correct value:
`WalkInMode__ExpiresAt` (null = never expires), `WalkInMode__BadgeKey` and
`PreviousBadgeKey` (blank disables offline badges / signals no rotation in
progress), and the Gemini and OpenAI keys (the provider is `Anthropic`).

### First-time provisioning on a fresh server

`configure-prod-env.ps1` was the runbook for this and has been deleted. On a new
box the sequence is: create the IIS sites and app pools in IIS Manager, copy the
five scripts across, and run each as Administrator.

**What went with the runbook, so it is a known gap rather than a surprise:** it
generated the base64 32-byte AES keys and - crucially - **refused to overwrite an
existing one**. The two keys are not equivalent, and the difference decides
whether a rotation is survivable. `FileStorage:EncryptionKey` wraps a per-file
data key rather than the file itself, so it has a path: promote the new key, move
the outgoing pair into `FileStorage:PreviousEncryptionKey` /
`PreviousKekVersion`, and everything already stored still opens - but the job
that re-wraps each blob under the new KEK is designed and not built
(SIMF-OPS-001 C.7), so treat it as set-once for now.
`Storage:UserIdDocumentEncryptionKey` has no previous-key slot at all: changing
it strands every encrypted PII column outright, with no window and no way back.

Those keys now come from the values already in `set-env-api.ps1`, and no script
regenerates them. If key generation is ever added back to a set-env script, the
never-overwrite guard has to come with it.

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

The failure looks like this, and the count is the number of live values already
on the box:

```
This server still carries 61 environment variable(s) using the retired 'SIMF_'
prefix, which this build does not read: SIMF_Ai__Anthropic__ApiKey, ...
```

Those 61 hold real production values, but you no longer need to move them: the
five `set-env-*.ps1` in this folder already carry the values under the new
prefixed names. Run them, then clear the old ones, elevated, in this order:

```powershell
# 1. Write the new SIMF_API_ / SIMF_CP_ / SIMF_WEB_ / SIMF_EDGE_ variables.
#    Each script restarts its own pool at the end.
.\deploy\set-env-api.ps1
.\deploy\set-env-cp.ps1
.\deploy\set-env-web.ps1
.\deploy\set-env-edge.ps1

# 2. THEN remove the pre-split variables the host refuses to start alongside.
.\deploy\clean-env.ps1 -Full
```

**Step 2 after step 1, never before**: clearing first leaves the box with no
configuration at all if anything is interrupted.

`migrate-env-prefix.ps1`, which used to copy each legacy value onto its new
prefix, is gone with the other extra scripts. It mattered for the keys that fan
out to more than one prefix — `SIMF_Storage__LogDirectory` feeds all four,
`SIMF_Api__BaseUrl` feeds CP and Web, `SIMF_ReverseProxy__KnownProxies__0` feeds
API and Edge — so if you ever do have to move values by hand, those are the ones
a one-to-one rename misses.

**Seven keys are boot gates** — a host refuses to start without its own:

| Key | Host |
|---|---|
| `SIMF_API_FileStorage__EncryptionKey` | API |
| `SIMF_API_Storage__UserIdDocumentEncryptionKey` | API |
| `SIMF_API_Ai__PromptHash__Secret` | API |
| `SIMF_CP_DataProtection__KeyRingPath` | CP |
| `SIMF_WEB_DataProtection__KeyRingPath` | Web |
| `SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address` | Edge |
| `SIMF_EDGE_ReverseProxy__KnownProxies__0` | Edge |

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

1. On the edge server, create the `SIMF.EDGE` site and its app pool in IIS
   Manager and bind the certificate. (`ops.ps1 -Action Install` did this and has
   been deleted.)
2. Run `set-env-edge.ps1` on that server as Administrator. **Both edge variables
   are BOOT GATES** - it refuses to start without them:
   - `SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address`
     = `https://api.simrsnf.com/` - inward at the API, never the edge's own name
   - `SIMF_EDGE_ReverseProxy__KnownProxies__0` = the WAF / load balancer address
3. Deploy the package by running the pipeline with `deployProduction` ticked; the
   edge step is the last of the four.
4. Publish `edge.simrsnf.com` in DNS and bind its certificate to the edge site.
5. Firewall: presentation to application on 443; application to data on 1433 and
   445. Resolve the key-ring rule noted against
   `SIMF_CP_DataProtection__KeyRingPath` / `SIMF_WEB_DataProtection__KeyRingPath`
   first, or neither host boots.
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
