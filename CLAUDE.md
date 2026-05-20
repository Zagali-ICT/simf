# SIMF — Project Instructions

Last updated: 2026-05-20

The global rule set at `~/.claude/CLAUDE.md` (§0–§20) applies in full. This file
adds SIMF-specific pointers only. It does not restate or override the global rules.

## Authoritative source of truth

SIMF's binding rules live in the controlled documents under `docs/`. Read the
relevant document before writing code or design. A controlled document overrides
any older draft, prompt, chat note, or assumption.

| Document | Governs |
|----------|---------|
| `docs/SIMF-SES-001-Software-Engineering-Standards.md` | Engineering rulebook — structure, DDD layering, conventions, naming, source control, code review, testing, security baseline, Definition of Done, freeze |
| `docs/SIMF-API-001-API-Specification.md` | API contract — `ApiResult<T>` envelope, standard headers, error model, HTTP status codes, pagination, authentication endpoints |
| `docs/SIMF-SAD-001-Software-Architecture-Document.md` | Architecture — modular monolith, bounded contexts, security, integration, deployment |
| `docs/SIMF-MAA-001-Mobile-Application-Architecture.md` | Flutter app architecture (Android + iOS) |
| `docs/SIMF-DMP-001-Documentation-Management-Plan.md` | Documentation management |
| `docs/SIMF-Program-Plan.md` | Programme plan, stages and gates |

If two documents disagree, the more specific one wins for its area; if it is still
unclear, ask — do not guess.

## Superseded material — do NOT use

The files under `D:\SIMF\System\15-04-2024\` (`final-prompt.md`, `my-style (1).md`,
`professional-coding-agent-prompt.md`) are an early draft. Several of their rules
contradict the current controlled docs — for example HTTP-200-always, the old
response envelope, phone-OTP registration, a `Smif*` component library, and
Flutter-on-Web. They are NOT a source of truth. Use only the `docs/` controlled
documents. The conflict list is recorded in `SIMF-OLD-DRAFT-CONFLICTS.md`.

## Status

SIMF V1.0.0 currently holds documentation only — no code yet. The build starts at
the Login API per the programme plan.
