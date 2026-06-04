# SIMF — Completion Programme (P2–P5) Regression Results

| | |
|--|--|
| **Date** | 2026-06-02 |
| **Branch** | `feature/login-api` (commit `da5a959`) |
| **Scope** | Regression snapshot after the P2→P5 Completion Programme + the E2E catalogue rebuild (D-245). Records the **automated** regression layer + a **live stack** boot/serve smoke. |
| **Catalogue** | [`docs/tests/e2e/`](tests/e2e/README.md) — 74 pages, ~983 executable scenarios |

> **Honest scope.** This document records two of the three regression layers
> defined in `SIMF-TST-001` §3: the **unit + integration** layer (automated,
> deterministic) and a **live boot/serve smoke** of the running stack. The
> **full end-to-end browser pass** — an agent driving all ~983 catalogue
> scenarios through the CP + Website with the Chrome DevTools MCP runner,
> entering data and performing each CRUD action — is the ongoing operation the
> rebuilt catalogue now enables; it is **not** executed in full here. Each
> per-page catalogue file cross-references the xUnit cases that already cover its
> surface at the lower layer.

## 1. Automated regression (unit + integration) — GREEN

Full solution, Release build (0 warnings / 0 errors), all suites 0 failures:

| Project | Tests | Result |
|---------|-------|--------|
| SIMF.Api.Tests | 720 | ✅ Passed |
| SIMF.ControlPanel.Tests | 55 | ✅ Passed |
| SIMF.Web.Tests | 27 | ✅ Passed |
| SIMF.Application.Tests | 16 | ✅ Passed |
| SIMF.ApiClient.Tests | 13 | ✅ Passed |
| SIMF.Domain.Tests | 5 | ✅ Passed |
| **Total** | **836** | **✅ 0 failures** |

The Api suite uses `WebApplicationFactory` against a real SQL Server with the
`InitialCreate` + additive migrations applied and the seeder run, so it exercises
the API end-to-end at the HTTP layer (every endpoint's happy path + every error
code) without a browser. The P2–P5 features are covered here: booking approval,
speaker presentations, system config, venue map, session lifecycle + recording,
the Q&A pipeline + recorded archive, the AI question filter + session-summary
(محضر) desk, and the GPS hall-attendance chain (geofence config, arrival/
departure, FR-704 question gating, operator QR scan).

## 2. Live stack boot + serve smoke — GREEN

The API was started in `Development` on http://localhost:5175 from the Release
build:

- **Startup migration + full seed ran without error** — the API reaching
  `/health` proves the `InitialCreate` + all additive migrations applied and the
  permission catalogue + AI-prompt + content seed completed (the same live
  confirmation used in `SIMF-Issue1-E2E-Results.md`).
- `GET /health` → **200**.
- `GET /api/v1/programme/sessions` → **200** with the correct `ApiResult<T>`
  envelope (`{ "success": true, "data": { "items": [] }, "error": null, "meta": null }`).
- Background workers started cleanly (RegistrationGateAutoClose, SessionReminder).
- Hosting environment: Development; data-protection keys initialised.

(The Control Panel :5158 and Website :5115 were not driven in this snapshot —
the full browser pass over their catalogue scenarios is the next operation.)

## 3. How to run the full end-to-end browser pass

The catalogue is the executable plan. To run it:

1. Bring up the stack (API :5175, CP :5158, Website :5115) in `Development`
   against the local SQL Server (`Server=.`, `SIMF_Identity` + `SIMF_App`).
2. Sign in as the seeded super-admin (TOTP via the `Get-Totp` helper).
3. For each `docs/tests/e2e/{page}.md`, drive every scenario with the Chrome
   DevTools MCP runner — enter the data, perform each CRUD/action, assert each
   expected outcome, and capture before/after screenshots under
   `docs/screenshots/`.
4. Record pass/fail per scenario id back into each page's Coverage matrix
   `Status` column.

## 4. Conclusion

The code-level regression is **green (836/0)** and the system **boots and serves
correctly** in Development. The rebuilt E2E catalogue (D-245) now gives full
per-page scenario coverage so the live browser pass can be run on demand to
confirm production-readiness page-by-page.

---

_Last reviewed:_ 2026-06-02 by Claude (Completion Programme regression snapshot).
