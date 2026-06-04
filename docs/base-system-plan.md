# SIMF — Base System Plan

> **Status:** Approved 2026-05-21 — part of the V1.0.0 documentation baseline.
> Base technical decisions locked; the domain is consolidated in `SIMF-Concept-Summary.md`.
> **Version:** V1.0.0
> **Created:** 2026-05-20
> **Updated:** 2026-05-20
> **Owner:** tech@ammn.com.sa

This document records the foundational decisions for the SIMF system.

**SIMF = Saudi International Maritime Forum** — an event platform for the Royal
Saudi Naval Forces' maritime defense forum (SIMF 2026, Riyadh). The full domain
concept is consolidated in **`docs/SIMF-Concept-Summary.md`**, synthesised from
the 13-document client set under `D:\SIMF\System\15-04-2024`.

---

## 1. Project Overview

- **Name:** SIMF
- **Root path:** `D:\SIMF\System\V1.0.0`
- **Architecture approach:** Domain-Driven Design (DDD)
- **Delivery process:** Agile (iterative, vertical slices)
- **Surfaces (3 client apps over one backend):**
  - **Web** — public/customer-facing web application
  - **CP (Control Panel)** — internal admin application
  - **Flutter App** — mobile application

---

## 2. Locked Technical Decisions

| Area | Decision | Notes |
|------|----------|-------|
| Backend framework | **.NET 10** | Latest release |
| API style | **FastEndpoints** | `Configure()` + `HandleAsync()`, `ApiResult<T>` wrapper |
| ORM | **EF Core** | Code-first migrations |
| Database | **SQL Server 2022** | |
| Web front-end | **Blazor + MudBlazor** | |
| Control Panel front-end | **Blazor + MudBlazor** | Same stack as Web |
| Mobile | **Flutter** | |
| Tenancy | **Single-tenant** | One organization; no tenant isolation |

---

## 3. Architecture Principles (carried from global rules)

- DDD layering: Domain → Application → Infrastructure → API/Presentation.
- Domain layer holds entities, aggregates, value objects, domain events,
  invariants — no framework dependencies.
- Human-readable, maintainable code over clever code; no deep nesting; early returns.
- `DataValidationException` for validation; domain-specific exceptions for
  illegal state transitions; never swallow exceptions.
- Every behavior change requires tests (unit + integration).
- CSS: `theme.tokens.css` is the single source of truth; no inline styles;
  no raw CSS colors; BEM naming.
- Authorization enforced consistently; `AllowAnonymous` only for
  SignIn / SignUp / ForgotPassword.

---

## 4. First Deliverable Scope

**Architecture plan only** — no code is written until the plan is approved.
The plan will cover:

1. Bounded contexts and ubiquitous language
2. Solution / project layout (`.sln`, project graph)
3. Aggregate / entity sketch per bounded context
4. API surface outline
5. The three client apps (Web, CP, Flutter) and how they consume the API
6. Agile backlog (epics → stories → first sprint)

---

## 5. OPEN — Before the DDD Plan Is Finalised

The business domain is understood and consolidated in `SIMF-Concept-Summary.md`,
whose **authoritative baseline is the 2026-05-20 client meeting**. Remaining open
items: the **deferred items in §15** and the **open confirmations in §16** of the
Concept Summary (Cognitive-AI provider, live-broadcast provider, WhatsApp
provider, SQL Server 2022 licensing, plus the per-type screen/permission detail).

---

## 6. Decision Log

| Date | Decision | Status |
|------|----------|--------|
| 2026-05-20 | Backend = .NET 10 + FastEndpoints + EF Core | Locked |
| 2026-05-20 | Database = SQL Server 2022 | Locked |
| 2026-05-20 | Web + CP front-end = Blazor + MudBlazor | Locked |
| 2026-05-20 | Mobile = Flutter | Locked |
| 2026-05-20 | Single-tenant | Locked |
| 2026-05-20 | First step = architecture plan only (no code) | Locked |
| 2026-05-20 | Domain = SIMF maritime-forum management system (concept documented) | Resolved |
| 2026-05-20 | Authoritative requirements baseline = 2026-05-20 client meeting | Locked |
| 2026-05-20 | Login = email + password only (no Nafath, no Face ID); no phone validation | Locked |
| 2026-05-20 | Deferred items & open confirmations — see Concept Summary §15–§16 | **Pending** |
