# SIMF-PTS-001 — Performance Testing Strategy

**Status:** v1.0 (2026‑06‑22) · **Owner:** QA / DevOps · **Governs:** how SIMF's performance is
specified, tested, and signed off.

**Why this exists:** the Technology & Methodology Approval Checklist accepted End‑to‑End testing
(#58) *"Approved with comments — Performance Testing Strategy needs to be included."* This document
is that strategy. It complements the functional **E2E catalogue** (`docs/tests/e2e/`) and the
engineering rulebook (`docs/SIMF-SES-001`), which cover correctness; this covers **speed, capacity,
and stability under load**.

---

## 1. Scope & system under test

SIMF is a single‑tenant maritime‑forum platform for a **time‑boxed live event**:

- **API** — .NET 10 / FastEndpoints, behind IIS (reverse proxy), over two SQL Server databases
  (`SIMF_Identity`, `SIMF_App`).
- **Control Panel** — Blazor Server (operator‑scale, tens of concurrent admins).
- **Website** — Blazor SSR (public, read‑heavy).
- **Mobile app** — Flutter (the **highest‑concurrency** client on event day).

Performance testing targets the **API** (the shared bottleneck) first, then the Website read paths.
The Blazor Server CP is operator‑scale and is load‑checked only for the heaviest grids/exports.

**Defining characteristic — the event‑day spike.** Unlike a steady SaaS load, SIMF's traffic is
**bursty and correlated**: thousands of attendees arrive in the same 30–60 minutes (gate scans),
open the agenda at once, and join a session together. The strategy is built around that spike, not
an average RPS.

## 2. Performance objectives (SLOs)

Targets are p95 server‑side latency at the **expected peak concurrency** (set the real numbers from
the event's registered‑attendee count before the first run — placeholders below).

| Class | Endpoints (examples) | p95 latency | Error rate | Notes |
|---|---|---|---|---|
| **Auth** | `POST /app/auth/sign-in`, `/verify-otp`, `/refresh` | ≤ 400 ms | < 0.5 % | PBKDF2 hashing dominates; watch CPU |
| **Gate / badge scan** | gate scan + idempotency | ≤ 250 ms | < 0.1 % | The hard real‑time path — queues at the door |
| **Read (hot)** | sessions list, agenda, badge, notifications, `users/me` | ≤ 300 ms | < 0.5 % | Cacheable; highest RPS |
| **Media fetch** | avatar / asset / media bytes | ≤ 200 ms (TTFB) | < 0.5 % | Served from `App_Data`, private‑cached |
| **Write** | profile save, booking/seat reserve, question submit | ≤ 600 ms | < 1 % | Includes RowAudit + validation |
| **Admin grids / export** | CP list + XLSX export | ≤ 2 s (list), ≤ 10 s (export) | < 1 % | Operator‑scale; bounded by row caps |

**Capacity target:** sustain **C** concurrent active attendees (C = registered × peak‑factor; pick
peak‑factor ≈ 0.6) for the event window with the SLOs above and **CPU < 75 %**, **SQL wait/lock
within budget**, and **no unbounded memory growth**.

## 3. Test types & what each proves

| Type | Question it answers | Profile |
|---|---|---|
| **Smoke (load)** | Does the system meet SLOs at light, steady load? Gate for CI. | 5–20 VUs, 2–5 min |
| **Load** | Does it meet SLOs at **expected peak**? | Ramp to peak C, hold 15–30 min |
| **Stress** | Where does it break, and **how** (graceful 429/503 vs. crash)? | Ramp past peak until SLO breach / errors |
| **Spike** | Does the **event‑day surge** (gate‑scan + agenda burst) survive? | Sharp ramp 0→peak in <60 s, hold, drop |
| **Soak / endurance** | Leaks, connection‑pool exhaustion, log/disk growth over the event day? | Peak‑ish load for 4–8 h |
| **Scalability** | Does adding resources (CPU/instance) help linearly? | Repeat load at 1×/2× resources |

Priority for SIMF: **Spike → Load → Soak** (the event shape), then Stress for headroom, Smoke in CI.

## 4. Key scenarios (mapped to the real hot paths)

Model the event‑day journey, weighted to reality:

1. **Doors‑open surge** — mass gate/badge scans (the #1 latency‑critical path).
2. **Agenda rush** — `sessions list` + `agenda` + `users/me` opened together.
3. **Sign‑in storm** — first‑login + token refresh as everyone opens the app.
4. **Session join + Q&A** — join a live session, submit questions, fetch comments.
5. **Media/avatars** — badge QR + speaker photos + media gallery.
6. **Operator load** — a handful of admins running grids/approvals/exports during the rush.

Each scenario should reuse the **data‑bearing Gherkin** scenarios already in `docs/tests/e2e/` so
the load model exercises the same real fields, permissions, and error codes.

## 5. Tooling

- **Primary: [k6](https://k6.io)** — scriptable (JS), CI‑friendly, thresholds‑as‑code, runs against
  any HTTP API, good spike/ramp modelling. A starter is committed at **`tests/perf/`**.
- **.NET‑native alternative: [NBomber](https://nbomber.com)** — for teams that prefer C#/xUnit and
  in‑process scenarios; equivalent capability.
- **Managed option: Azure Load Testing** — wraps k6/JMeter, integrates with Azure DevOps Pipelines
  if/when the infra‑restriction question (checklist #42–46) is resolved.
- **Observability during runs:** Serilog (already on), Windows perfmon / SQL Server DMVs
  (`sys.dm_os_wait_stats`, `dm_exec_requests`), and the `/health` endpoint.

Pick **one** primary (k6) and keep it; do not mix runners per run.

## 6. Environment & data

- Run against a **dedicated Staging** tier that mirrors production sizing (CPU/RAM/SQL edition),
  **never production** (load tests are destructive to capacity and skew audit/rate‑limit data).
- **Rate limits:** production limits (600/min IP, 20/min auth, 5/min email) will throttle a load
  test from a single source. For capacity testing, distribute load across source IPs **or**
  temporarily raise limits on the isolated staging tier (documented, reverted after).
- **Test data:** seed a realistic attendee population (≈ registered count) + sessions/gates; use
  the `Get‑Totp` helper / seeded codes for auth, **never literal secrets** (matches the E2E rule).
- **TLS:** test against the real cert; if staging is self‑signed, the load tool must trust it
  explicitly for the run (mirrors the known prod cert item C2/H2).

## 7. Pass/fail thresholds (gate)

A run **passes** when, at the target concurrency:
- every SLO class in §2 meets its p95 + error‑rate budget, and
- CPU < 75 %, no SQL lock/timeout storm, no memory growth trend over a soak, and
- overload degrades **gracefully** (429/503 with the `ApiResult` envelope), never 5xx crashes or
  data corruption.

Encode the thresholds in the k6 script (`thresholds: { http_req_duration: ['p(95)<400'], ... }`) so
the run **fails the build** on breach.

## 8. CI/CD integration

- **PR / nightly:** the **smoke‑load** profile runs in the pipeline (a few minutes) and gates merge
  — add it next to the re‑enabled test stage in `azure-pipelines.yml` (see the Wave‑8 SCA/test
  gates). Keep it short so it doesn't slow PRs.
- **Pre‑release / pre‑event:** the **Load + Spike + Soak** profiles run against Staging on a
  schedule and before go‑live; results attached to the release record.
- Publish the k6 summary (JSON/HTML) as a pipeline artifact alongside the SBOM.

## 9. Reporting & cadence

Each run produces a report: profile, concurrency, p50/p95/p99 per class, error rate, throughput,
resource graphs (CPU/SQL/memory), SLO pass/fail, and any bottleneck root cause + follow‑up. Reports
feed the **Test Reports** deliverable (checklist #65). Cadence: smoke every CI run; full
Load/Spike/Soak before each release and **mandatory before the live event**.

## 10. Roles

- **QA** owns the scenarios, thresholds, and run execution.
- **DevOps** owns the staging tier, CI wiring, and the load‑generation infra.
- **Solution Architect** signs off the SLOs and reviews bottleneck findings.

---

### Appendix A — getting started
A runnable k6 baseline (smoke + ramp profiles over the hot read/auth paths) is committed at
**`tests/perf/`** with a README. Set `BASE_URL` + a seeded token, then `k6 run`. Treat it as the
template the scenario suite grows from — it is intentionally minimal so it stays maintainable.
