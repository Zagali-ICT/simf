# SIMF — Project Instructions

Last updated: 2026-05-25

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

Sprint 1 — Login API + frontend login + visitor lifecycle + hardening — has shipped on
`feature/login-api`. Backend (FastEndpoints + EF Core + SQL Server), Control Panel
(Blazor Server), Website (Blazor SSR + interactive auth islands), and shared component
library + typed API client are all in. Decisions log: `docs/decisions/DECISIONS_LOG.md`
runs D-001 through D-072. Sprint completion artefact:
`docs/SIMF-Sprint1-Login-API-Completion.md`. Outstanding items the sprint accepted /
deferred (committed secrets rotation, architectural refactor, Website skip-link, full
bUnit harness, end-to-end lifecycle test, no-IP rate-limit hardening) are listed there.

Next stage per the programme plan is the User Management module increment on top of
the closed Login API foundation.

## FREEZE — D-110 baseline (2026-05-26)

The following surface is **frozen** as of commit `67e2263` and must NOT be
changed without explicit owner approval:

- **EF schema** — the `InitialCreate` migrations on both `SimfIdentityDbContext`
  and `SimfAppDbContext` capture the final schema. No more schema changes;
  any future column / table / index addition must be argued for as a breaking
  change.
- **Enum names + values** — every enum in `src/Shared/SIMF.Common/Enums/`
  (SignInAudience, AccountState, AccountCodePurpose, SecondFactorKind,
  UserType, AuditOutcome, RowAuditOperation, NotificationKind,
  NotificationSeverity) is frozen by name AND by underlying integer value.
  Renaming a case or reordering numeric values is a breaking change.
- **Migration history** — only one `InitialCreate` per context. No further
  migrations land without owner approval.

Frontend additions, new resx strings (more languages), new endpoints,
non-schema bug fixes, and additive Options-section keys remain in scope
for normal development. The freeze applies to the persistence and enum
contract surface only.
