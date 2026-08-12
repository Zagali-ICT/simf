# SIMF.MobileEdge — deployment guide

The mobile presentation tier. A YARP reverse proxy that takes over the public
`api.simrsnf.com` name and forwards only the mobile surface to an API that is no
longer published at all.

- **Project:** `src/Edge/SIMF.MobileEdge/SIMF.MobileEdge.csproj`
- **Artifact:** `edge/SIMF.MobileEdge.zip`, or `publish\edge` from `publish.ps1`
- **IIS site:** `SIMF.EDGE`, path `D:\System\v1.0.1\edge`, HTTP port 12343
- **Pipeline:** `deploy/README.md`

## Why it exists

The Flutter app compiles its API base URL in, so the public hostname cannot change
without a store release on both platforms. The edge takes that hostname over, which
lets the API move to a private address and stop being published. Installed apps
need no rebuild.

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
(`SimfAdminClient`). Once the edge owns `api.simrsnf.com`, `SIMF_Api__BaseUrl` must
point at the API's **private** address, or every admin page 404s while the mobile
app carries on working. CP and Website are server-side callers in the presentation
zone; they reach the application zone directly and never traverse the public edge.

## Addressing

Hostnames, never raw IPs. Every certificate bypass was removed on 2026-08-08, so
the API certificate has to validate, and no public CA issues one for a bare address.

| | Name | Internet | Hosting |
|---|---|---|---|
| Edge (tier 1) | `api.simrsnf.com` | yes, via the WAF/LB | IIS site `SIMF.EDGE` |
| API (tier 2) | private, e.g. `api-int.simrsnf.local` | no | IIS site `SIMF.API` |

**Two certificates:** the public one for `api.simrsnf.com`, moved to the edge site
at cutover, and an internal one for the API's private name so the edge, CP and
Website can validate it. The internal one is the one that gets forgotten.

## Configuration

Both of these are **boot gates** — the edge refuses to start without them, rather
than 502-ing every app user or trusting an unverified header.

| Variable | Value |
|---|---|
| `SIMF_ReverseProxy__Clusters__api__Destinations__primary__Address` | the API's **private** HTTPS address. Never `api.simrsnf.com`, or the edge forwards to the load balancer and back to itself. |
| `SIMF_ReverseProxy__KnownProxies__0` | the WAF / load balancer address. Without it `X-Forwarded-For` is unverified, and any caller can spoof its source address past the API's rate limiter and into the audit log. |

It also reads `SIMF_Storage__LogDirectory` and writes to
`{dir}/SIMF.MobileEdge/log-{Date}.log`, 31 days retained — same shape as the API,
CP and Website. This host is internet-facing and the first thing a mobile request
touches, so its log is where an incident starts; the app-pool identity needs write
access there. `ops.ps1` grants it.

Production values arrive as `SIMF_`-prefixed Machine-scope environment variables
(`deploy/set-env.ps1`); the host strips the prefix. Restart the app pool after
changing them so `w3wp` picks them up.

## Availability

Run **two nodes**, not one. Once the edge owns the public name it carries 100% of
mobile traffic, so a single instance turns a four-node API into a single point of
failure — an availability regression made in the name of security. It is a
stateless proxy: no affinity, no shared state, no key ring. Size it from peak
concurrent mobile users on event day, not from the API's sizing.

The load balancer probes `GET /health`, which returns `healthy` and deliberately
does **not** probe the API. A health check that fails because a downstream is
unhealthy takes the edge out of rotation for a fault it cannot fix, turning one
outage into two. The trade-off is that a downstream outage is invisible to the LB
by design, so end-to-end monitoring has to live somewhere else.

## Install and cut over

```powershell
# 1. Create the site and pool. -ApiHost must be the API's PRIVATE name; the
#    script refuses to install while it still matches -EdgeHost.
.\deploy\ops.ps1 -Action Install -Target Edge `
    -ApiHost api-int.simrsnf.local -EdgePort 12343 -CertThumbprint <thumbprint>

# 2. Fill in and run set-env.ps1 on the server (as Administrator).

# 3. Deploy the files.
.\deploy\iis-deploy.ps1 -ArtifactRoot .\publish `
    -EdgeSiteName "SIMF.EDGE" -EdgePath "D:\System\v1.0.1\edge"
```

4. Move the `api.simrsnf.com` certificate binding to the edge site.
5. Firewall: presentation to application on 443. Resolve the key-ring rule noted
   against `SIMF_DataProtection__KeyRingPath` first, or CP and Website will not boot.
6. Repoint DNS `api.simrsnf.com` at the edge, and unpublish the API.
7. Restart all app pools.

**Rollback** is a DNS change: point `api.simrsnf.com` back at the API and republish
it. No app release either way, which is the whole reason for taking the public name.

## Verifying a deployment

| Check | Expected |
|---|---|
| `GET https://api.simrsnf.com/health` | `healthy` |
| A call under `/api/v1/app/*` through the edge | succeeds, and the JSON is **byte-identical** to the same call made directly against the API |
| `GET /api/v1/admin/anything` through the edge | **404** — the admin surface is not reachable here |
| Edge started with an empty destination address | refuses to start, naming the missing variable |
| Edge started with empty `KnownProxies` outside Development | refuses to start, naming the missing variable |
| Response headers | `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Content-Security-Policy` |
| API audit log after a call through the edge | shows the **client's** address, not the edge's, proving `X-Forwarded-For` is honoured |
| The real Flutter app pointed at `api.simrsnf.com` | signs in and loads sessions with no rebuild |

## Related

- `deploy/README.md` — pipeline, `ops.ps1`, `set-env.ps1`, and the cutover section
- `src/Edge/SIMF.MobileEdge/Program.cs` — the host, and the reasoning in its comments
- `tests/SIMF.Api.Tests/Operations/MobileEdgeRoutingTests.cs` — route allow-list tests
