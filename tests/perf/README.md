# SIMF performance tests (k6)

The runnable companion to **`docs/SIMF-PTS-001-Performance-Testing-Strategy.md`** (the strategy
authored for checklist item #58). Start here and grow the scenario suite from the baseline.

## Install k6
- Windows: `winget install k6` (or `choco install k6`)
- macOS: `brew install k6`
- Docs: <https://grafana.com/docs/k6/latest/>

> k6 is JavaScript‑scripted but is **not** a Node project — it has its own runtime. The .NET‑native
> alternative is **NBomber** (PTS‑001 §5); pick one and keep it.

## Run

```bash
# Smoke (CI gate) — default profile
k6 run -e BASE_URL=https://staging.simf.example tests/perf/k6-baseline.js

# Load (ramp to peak; supply a seeded token for the authed path)
k6 run -e PROFILE=load  -e BASE_URL=https://staging.simf.example -e TOKEN=<seeded-jwt> tests/perf/k6-baseline.js

# Spike (event-day doors-open surge)
k6 run -e PROFILE=spike -e BASE_URL=https://staging.simf.example tests/perf/k6-baseline.js

# Soak (endurance)
k6 run -e PROFILE=soak  -e BASE_URL=https://staging.simf.example tests/perf/k6-baseline.js
```

### Environment variables
| Var | Default | Notes |
|---|---|---|
| `BASE_URL` | `https://localhost:5001` | The API base URL of the **staging** tier (never production — PTS‑001 §6). |
| `PROFILE` | `smoke` | `smoke` \| `load` \| `spike` \| `soak`. |
| `TOKEN` | _(empty)_ | A **seeded/Get‑Totp** JWT for the authed read; never a literal secret. |
| `INSECURE_TLS` | `true` | Trust the staging self‑signed cert for the run (item C2/H2). |

## Notes
- The script **fails** (non‑zero exit) when a threshold is breached — wire the smoke profile into
  the pipeline next to the test gate (PTS‑001 §8).
- Set the real `load`/`spike` VU targets from the event's registered‑attendee count before the
  first capacity run.
- Production rate limits (600/min IP, 20/min auth, 5/min email) will throttle a single‑source run —
  distribute load or raise limits on the isolated staging tier (documented, reverted after).
