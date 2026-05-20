# SIMF — Agent Prompt: Apply the 2026-05-20 Design Decisions

| Field | Value |
|-------|-------|
| Prepared | 2026-05-20 |
| Purpose | Ready-to-paste task for an AI coding agent to apply the agreed design decisions to the SIMF controlled documents |
| Inputs | `SIMF-OLD-DRAFT-CONFLICTS.md` (conflict analysis); the memory file `simf-design-decisions.md` |
| Scope | Edits the controlled documents in `docs/` — a reviewed task governed by SIMF-DMP-001 |

Copy everything inside the block below into the agent.

---

```text
TASK — Apply the 2026-05-20 SIMF design decisions to the controlled documents.

WORKING CONTEXT
- Repo: D:\SIMF\System\V1.0.0. Read and obey CLAUDE.md (project) and the global
  ~/.claude/CLAUDE.md before doing anything.
- The controlled documents are in docs/. They are governed by SIMF-DMP-001:
  every change bumps the document Version and adds a Revision-history row.
- Background: see SIMF-OLD-DRAFT-CONFLICTS.md for the full conflict analysis.
- Follow the §11 mandatory pre-approval format — present a per-file plan
  (ADD/UPDATE, before/after, Risk tag) and STOP for approval before any edit.
- Read the whole target document before proposing an edit to it.
- Do NOT invent. Anything not decided below is raised as an open item, not guessed.

CHANGES TO APPLY

1) Password reset — SIMF-API-001 §12.7 (resolves open item OI-3)
   Specify the flow in the same style as the other §12 auth endpoints:
   - POST /auth/forgot-password  { email } -> emails a 6-digit numeric OTP;
     always returns success (no account enumeration).
   - POST /auth/reset-password   { email, code, newPassword, confirmPassword }
     -> verifies the 6-digit OTP, sets the new password.
   - Built on ASP.NET Core Identity.
   - OTP rules consistent with verify-email (6 digits, expiry, resend throttling).
   - Add new error codes to §12.6 (e.g. AUTH_RESET_CODE_INVALID,
     AUTH_RESET_CODE_EXPIRED). Remove OI-3 from §15.

2) Configuration & environment — SIMF-SES-001 (new subsection under §4)
   Document the strategy:
   - appsettings.json — shared, common, NON-sensitive settings only. No secrets.
   - appsettings.Development.json — Development overrides.
   - appsettings.E2E.json — E2E test overrides.
   - There is NO appsettings.Production.json.
   - Production config — production overrides AND every secret (connection
     strings, JWT key, provider keys) — is applied as Machine-scope environment
     variables by a per-service script set-env-<service>.ps1 (e.g. set-env-api.ps1),
     using the ASP.NET Core double-underscore (__) convention.
   - The set-env script committed to the repo is a PLACEHOLDER TEMPLATE only;
     real secret values are never committed (consistent with SES-001 §12 and
     SAD-001 §8.4).

3) Smif* component library — SIMF-SES-001 §6 (cross-reference MAA-001)
   Document a shared wrapper-component library:
   - Control Panel (Blazor) and mobile app (Flutter) both compose UI from
     Smif*-prefixed wrapper components (SmifButton, SmifInputText, SmifInputNumber,
     SmifInputCheck, SmifInputTabs, SmifInputDropdownList, SmifError, SmifBanner,
     SmifTable, SmifPager, SmifPopup, SmifConfirm, SmifLoader, ...).
   - Pages never place raw HTML inputs or framework primitives directly.
   - A new UI primitive is added to the library before it is used in a page.
   - In the Control Panel, Smif* components wrap MudBlazor; in Flutter they wrap
     the design-system widgets.
   - MAA-001 §12 says mobile components come from the external designer —
     reconcile by stating the Smif* layer wraps the designer's components; flag
     any residual tension for the Solution Architect rather than guessing.

4) Date & number format — SIMF-MAA-001 §10 (make the ambiguous wording explicit)
   - Dates display as dd-MM-yyyy.
   - Numbers are always rendered in Latin/English digits, regardless of UI
     language. This overrides the generic "formatted for the active locale"
     wording for digits and date format.

5) Confirmed correct — verify, do NOT change
   Real HTTP status codes (API-001 §8), ApiResult<T> envelope (§6),
   one-language-per-Accept-Language messages (§11), 6-digit email verification
   (§12), Flutter mobile-only / website is Blazor (MAA-001 §2, SAD-001 §4),
   Accept-Language header (§5). Confirm each still reads correctly; make no edit.

6) Undecided — do NOT act; raise as open items for the Solution Architect
   STS (Security Token Service), a lookup-endpoint convention, enum mirroring
   (DB/API/UI + GetEnumList), named base abstractions (BaseEndpoint<T> etc.),
   the CRUD page shape (popup add/edit, select-all, 20-row grid), the /simplified
   step, and the Arabic translation-quality rule. List them; invent nothing.

DELIVERABLE
A per-document §11 change plan -> STOP for approval -> after approval, the edits
with Version bumps and Revision-history rows -> a short summary of what changed.
```
