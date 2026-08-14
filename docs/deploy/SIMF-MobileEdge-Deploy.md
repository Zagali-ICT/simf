# SIMF.MobileEdge — deployment guide

The mobile presentation tier. A YARP reverse proxy published at
`edge.simrsnf.com` that forwards only the mobile surface to an API which is not
published to the internet at all.

- **Project:** `src/Edge/SIMF.MobileEdge/SIMF.MobileEdge.csproj`
- **Artifact:** `edge/SIMF.MobileEdge.zip`, or `publish\edge` from `publish.ps1`
- **IIS site:** `SIMF.EDGE`, path `D:\System\v1.0.1\edge`, HTTP port 12343
- **Pipeline:** `deploy/README.md`

## Why it exists

It is the only public entry point for the mobile clients, which lets the API stop
being published to the internet: the app reaches `edge.simrsnf.com`, the edge
reaches the API inside the estate, and nothing outside can address the API at all.

**It needs a mobile release.** `build_config.dart` compiles the base URL in
(`String.fromEnvironment`, default `https://api.simrsnf.com/api/v1`), so an
installed app talks to the API directly and knows nothing about the edge. Routing
mobile traffic through it means rebuilding with `--dart-define` pointing at
`edge.simrsnf.com` and shipping to both stores. Withdrawing the API's public DNS
record and shipping that release must land together, or the installed app has
nothing to reach in between.

It deliberately does almost nothing: no reshaping, no aggregation, no business
logic. The shipped mobile wire contract is append-only (D-219), so every field the
app decodes has to survive this hop byte for byte.

## What it publishes

Exactly one route:

```json
"mobile-app-surface": { "ClusterId": "api", "Match": { "Path": "/api/v1/app/{**catch-all}" } }
```

Routes and clusters are configuration rather than code, so a new mobile endpoint
needs no rebuild — but the published path set stays a deliberate allow-list. That
is what keeps `/api/v1/admin/**` unreachable through the edge.

**Consequence for the Control Panel.** The CP calls `/api/v1/admin/**`
(`SimfAdminClient`), which the edge has no route for. `SIMF_Api__BaseUrl` therefore
points CP and Website at `api.simrsnf.com` directly: they are server-side callers
in the presentation zone and never traverse the public edge. Pointing either at
`edge.simrsnf.com` would 404 every admin page.

## Addressing

Hostnames, never raw IPs. Every certificate bypass was removed on 2026-08-08, so
the API certificate has to validate, and no public CA issues one for a bare address.

| | Name | Internet | Hosting |
|---|---|---|---|
| Edge (tier 1) | `edge.simrsnf.com` | yes, via the WAF/LB | IIS site `SIMF.EDGE` |
| API (tier 2) | `api.simrsnf.com`, internal to the estate | no | IIS site `SIMF.API` |

**Two certificates:** a public one for `edge.simrsnf.com`, and one for
`api.simrsnf.com` so the edge, CP and Website can validate the API. The second is
the one that gets forgotten, because it is internal.

## Configuration

Both of these are **boot gates** — the edge refuses to start without them, rather
than 502-ing every app user or trusting an unverified header.

| Variable | Value |
|---|---|
| `SIMF_ReverseProxy__Clusters__api__Destinations__primary__Address` | the API's HTTPS address inside the estate, e.g. `https://api.simrsnf.com/`. Never `edge.simrsnf.com`, which is this host: the edge would forward through the load balancer back to itself. |
| `SIMF_ReverseProxy__KnownProxies__0` | the WAF / load balancer address. Without it `X-Forwarded-For` is unverified, and any caller can spoof its source address past the API's rate limiter and into the audit log. |

It also reads `SIMF_Storage__LogDirectory` and writes to
`{dir}/SIMF.MobileEdge/log-{Date}.log`, 31 days retained — same shape as the API,
CP and Website. This host is internet-facing and the first thing a mobile request
touches, so its log is where an incident starts; the app-pool identity needs write
access there. `ops.ps1` grants it.

Production values arrive as `SIMF_`-prefixed Machine-scope environment variables,
set on this server by `deploy/set-env-edge.ps1` (filled from
`set-env-edge.template.ps1`); the host strips the prefix. Restart the app pool
after changing them so `w3wp` picks them up.

## Availability

Run **two nodes**, not one. Once the app is pointed at it, the edge carries 100% of
mobile traffic, so a single instance turns a four-node API into a single point of
failure — an availability regression made in the name of security. It is a
stateless proxy: no affinity, no shared state, no key ring. Size it from peak
concurrent mobile users on event day, not from the API's sizing.

The load balancer probes `GET /health`, which returns `healthy` and deliberately
does **not** probe the API. A health check that fails because a downstream is
unhealthy takes the edge out of rotation for a fault it cannot fix, turning one
outage into two. The trade-off is that a downstream outage is invisible to the LB
by design, so end-to-end monitoring has to live somewhere else.

## Install and deploy

```powershell
# 1. Create the site and pool on the edge server.
.\deploy\ops.ps1 -Action Install -Target Edge -EdgePort 12343 -CertThumbprint <thumbprint>

# 2. Fill in and run set-env-edge.ps1 on that server (as Administrator).
#    BOTH of its variables are boot gates.

# 3. Deploy this package to that server.
.\deploy\pipeline-deploy-one.ps1 -Package edge -ZipName SIMF.MobileEdge.zip `
    -SiteName SIMF.EDGE -SitePath D:\System\v1.0.1\edge -Drop <drop> -Root <root>
```

4. Publish `edge.simrsnf.com` in DNS and bind its certificate to the edge site.
5. Firewall: presentation to application on 443. Resolve the key-ring rule noted
   against `SIMF_DataProtection__KeyRingPath` first, or CP and Website will not boot.
6. Restart the edge app pool.

At this point the edge is live and serving, and **no client is using it yet** -
installed apps still reach the API directly. Two steps remain, and they are a
release decision rather than a deployment one:

7. Ship a mobile release built with `--dart-define` pointing at `edge.simrsnf.com`.
8. Once that release has reached users, withdraw the API's public DNS record.

**Rollback** before step 8 costs nothing: no client depends on the edge, so
removing it affects nobody. After step 8 it is a DNS change, restoring the API's
public record.

## Verifying a deployment

| Check | Expected |
|---|---|
| `GET https://edge.simrsnf.com/health` | `healthy` |
| A call under `/api/v1/app/*` through the edge | succeeds, and the JSON is **byte-identical** to the same call made directly against the API |
| `GET https://edge.simrsnf.com/api/v1/admin/anything` | **404** - the admin surface is not reachable here |
| Edge started with an empty destination address | refuses to start, naming the missing variable |
| Edge started with empty `KnownProxies` outside Development | refuses to start, naming the missing variable |
| Response headers | `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Content-Security-Policy` |
| API audit log after a call through the edge | shows the **client's** address, not the edge's, proving `X-Forwarded-For` is honoured |
| A Flutter build made with `--dart-define` for `edge.simrsnf.com` | signs in and loads sessions through the edge |
| The Control Panel | still reaches `api.simrsnf.com` directly and loads an admin grid |

## Related

- `deploy/README.md` - pipeline, `ops.ps1`, the per-server env scripts, and the mobile edge section
- `src/Edge/SIMF.MobileEdge/Program.cs` — the host, and the reasoning in its comments
- `tests/SIMF.Api.Tests/Operations/MobileEdgeRoutingTests.cs` — route allow-list tests
