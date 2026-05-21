# SIMF Test Guide

| Field | Value |
|-------|-------|
| Document | SIMF Test Guide |
| Status | Living document — extended each increment |
| Last updated | 2026-05-21 (Sprint 1, increment 1) |
| Related | SIMF-TST-001, SIMF-SES-001 |

How SIMF tests are organised and run. It grows one section per increment.

## 1. Test projects

| Project | Covers |
|---------|--------|
| `SIMF.Domain.Tests` | Domain entities and rules |
| `SIMF.Application.Tests` | Use-case handlers and validators |
| `SIMF.Api.Tests` | API endpoints — unit and integration |

Framework: xUnit. API integration tests host the API in-memory with
`WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing`).

## 2. Running the tests

    dotnet test SIMF.slnx -c Release

Every behaviour change ships with tests (SIMF-SES-001 section 11); a fixed bug
ships with a regression test.

## 3. Increment 1 — the scaffold

| Test | Type | Verifies |
|------|------|----------|
| `ApiResultTests` | Unit | `ApiResult<T>.Ok` and `.Fail` build the correct envelope |
| `HealthEndpointTests` | Integration | `GET /health` returns 200 and `Healthy` |

`SIMF.Domain.Tests` and `SIMF.Application.Tests` hold no tests yet — the Domain
and Application layers gain code from increment 2.
