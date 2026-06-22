// SIMF performance baseline (k6) — see docs/SIMF-PTS-001-Performance-Testing-Strategy.md
//
// Minimal, maintainable template over the hot read/auth paths. Grow the scenario
// suite from here; keep thresholds as code so a run fails the build on breach.
//
//   Smoke:  k6 run -e BASE_URL=https://staging.example tests/perf/k6-baseline.js
//   Load:   k6 run -e PROFILE=load  -e BASE_URL=... -e TOKEN=<seeded-jwt> tests/perf/k6-baseline.js
//   Spike:  k6 run -e PROFILE=spike -e BASE_URL=... tests/perf/k6-baseline.js
//   Soak:   k6 run -e PROFILE=soak  -e BASE_URL=... tests/perf/k6-baseline.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'https://localhost:5001';
const TOKEN = __ENV.TOKEN || '';
const errors = new Rate('errors');

export const options = {
  // Default profile is the CI smoke gate; override with -e PROFILE=load|spike|soak.
  scenarios: profileScenarios(__ENV.PROFILE || 'smoke'),
  thresholds: {
    // PTS-001 §2/§7 — the "Read (hot)" SLO; tighten per class as the suite grows.
    http_req_duration: ['p(95)<400'],
    http_req_failed: ['rate<0.01'],
    errors: ['rate<0.01'],
  },
  // Staging may present the known self-signed cert (item C2/H2); allow it for the run.
  insecureSkipTLSVerify: (__ENV.INSECURE_TLS || 'true') === 'true',
};

function profileScenarios(profile) {
  switch (profile) {
    case 'load': // ramp to expected peak and hold (set target = expected peak VUs)
      return { load: { executor: 'ramping-vus', startVUs: 0, stages: [
        { duration: '2m', target: 100 }, { duration: '15m', target: 100 }, { duration: '2m', target: 0 } ] } };
    case 'spike': // event-day doors-open surge
      return { spike: { executor: 'ramping-vus', startVUs: 0, stages: [
        { duration: '30s', target: 300 }, { duration: '3m', target: 300 }, { duration: '30s', target: 0 } ] } };
    case 'soak': // endurance — leaks, pool exhaustion, disk/log growth
      return { soak: { executor: 'constant-vus', vus: 60, duration: '4h' } };
    default: // smoke (CI gate)
      return { smoke: { executor: 'ramping-vus', startVUs: 1, stages: [
        { duration: '30s', target: 10 }, { duration: '1m', target: 10 }, { duration: '30s', target: 0 } ] } };
  }
}

export default function () {
  // Hot read 1 — health probe (always available, cheap).
  const health = http.get(`${BASE_URL}/health`);
  if (!check(health, { 'health 200': (r) => r.status === 200 })) {
    errors.add(1);
  }

  // Hot read 2 — authenticated profile read, when a seeded token is supplied
  // (PTS-001 §6: use a seeded/Get-Totp token, never a literal secret).
  if (TOKEN) {
    const me = http.get(`${BASE_URL}/api/v1/app/users/me`, {
      headers: { Authorization: `Bearer ${TOKEN}` },
    });
    if (!check(me, { 'me 200': (r) => r.status === 200 })) {
      errors.add(1);
    }
  }

  sleep(1);
}
