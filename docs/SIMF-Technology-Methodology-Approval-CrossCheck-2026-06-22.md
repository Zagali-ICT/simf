# SIMF — Technology & Methodology Approval — Build Cross‑Check

**Reference:** `15-04-2024/Technology-Methodology-Approval-Checklist.xlsx` — the customer /
cyber **Technology & Methodology Approval Checklist** (63 items + Inquiries + Sign‑Off).
That spreadsheet is a controlled document.

**Purpose:** verify the SIMF system **as built** matches each checklist verdict — especially the
**Rejected** items (binding constraints) and the **Approved‑with‑comments** items (cyber
conditions). Verdicts grounded in the codebase at the date below.

**Date:** 2026‑06‑22 · **Companion:** `docs/security/SIMF-NCA-AppSec-Standard-GapAnalysis-2026-06-20.md`
(the OWASP/NCA review the checklist's #2/#5 require).

**Checklist summary (from the Sign‑Off sheet):** 63 items — 41 Approved · 4 Approved‑with‑comments
· **4 Rejected** · 14 Needs‑discussion · 0 pending. **The Sign‑Off sheet is blank — the checklist
is not yet executed/signed.**

---

## 1. Rejected items — the binding constraints

| # | Item | Customer ruling | Build status | Evidence |
|---|---|---|---|---|
| 12 | JWT access token | **Rejected** → "not more than 5 minutes" | ✅ **Matches** | `appsettings.json` `Jwt:AccessTokenMinutes = 5` (D‑443) |
| 13 | Session lifetime | **Rejected** → "not more than 1 day" | ✅ **Matches** | `Jwt:SessionLifetimeHours = 24`; absolute cap enforced on refresh (D‑443) |
| 23 | WhatsApp Business API | **Rejected** (infra/cyber) | ✅ **Matches** | No WhatsApp sender exists in `src/` |
| 26 | Google Gemini AI | **Rejected** (infra/cyber) | ⚠️ **Divergence** | `GeminiAiProvider.cs` (+ `OpenAiProvider`, `AnthropicAiProvider`) exist, but **dormant**: `Ai:DefaultProvider = "Echo"`, all provider `ApiKey` blank, and the M4 prompt‑hash boot guard. **Action:** see §5.1 — hard‑disable or remove the external providers to fully honour the rejection. |

> Note (Inquiries sheet #2): the vendor stated Gemini would be "read‑only against audit log
> streams, no user PII". The customer **still Rejected** #26 pending cyber approval, so the
> integration must not be active.

## 2. Approved‑with‑comments — cyber conditions

| # | Item | Condition | Build status |
|---|---|---|---|
| 2 | FastEndpoints | "Need Cyber Review for OWASP T10" | ✅ **Done** — full OWASP/NCA cross‑walk + 19‑commit remediation (gap‑analysis doc) |
| 5 | Blazor (Web) | "Need Cyber Review for OWASP T10" | ✅ **Done** — CSP, security headers, charset, no‑store on PII, etc. |
| 19 | Password & email policy | "Cyber Compliance must be met" | ✅ **Largely done** — NCA password policy (complexity + breached‑list + expiry + history) + email‑format validation. ⚠️ disposable‑domain blocking **not verified** — confirm or add. |
| 58 | End‑to‑End tests | "Performance Testing Strategy needs to be included" | ✅ **Addressed by this change** — `docs/SIMF-PTS-001-Performance-Testing-Strategy.md` + `tests/perf/` starter. |

## 3. Needs‑discussion — status & divergences

| # | Item | Build status |
|---|---|---|
| 6 / 7 | Flutter Android / iOS (native only) | ✅ Matches — native Flutter app; no Flutter‑on‑web |
| 8 | "Smif\*" component library | ⚠️ Naming — actual prefix is **`Simf*`** (`src/Shared/SIMF.Components`), not `Smif*` (the superseded‑draft name). Library exists + used across Web/CP |
| 11 | Request headers / App Key | ✅ Present (`X‑App‑Key`) — the "explain usage" ask is documentation, not a mismatch |
| 14 | Admin MFA (TOTP) | ✅ Present (`ITotpVerifier`; admin TOTP enrolment) |
| 15 | User OTP / Activation | ⚠️ **Intentional divergence** — system uses an **emailed 6‑digit code**, *not* phone‑OTP‑at‑registration and *not* a magic link (phone‑OTP + magic‑link were superseded by the controlled docs). Reduces the attack surface but doesn't match the row wording |
| 17 | Rate limiting | ✅ Present — 600/min per‑IP global, 20/min per‑IP auth, 5/min per‑email (the req/min figures they asked for) |
| 18 | RBAC (+ IDOR review) | ✅ Per‑page/per‑action permission on every admin endpoint; IDOR structurally prevented (actor resolved from JWT `sub`) — verified in the review |
| 27 | Smart search (AI) | Tied to #26 — external AI dormant |
| 42–46 | Azure DevOps (Repos/Boards/Pipelines/Test Plans/Environments) | ⚠️ **Open** — the project **does** use Azure DevOps (`dev.azure.com/Zagali-KSA`); the "infrastructure restrictions for external repos" (data‑residency) is unresolved and ties to the source‑handover deadline |

## 4. Approved features proposed‑but‑not‑built (not a compliance issue, but "doesn't match yet")

- **#20 SignalR real‑time hub** (live chat / presence / typing) — **not built**; it is a V2 feature.
- **#21 SMS channel** — **no SMS sender** in code; the system uses email codes.
- **#25 "Unified abstraction — all four channels"** — **partial**: Email + In‑app built; SMS + WhatsApp not (WhatsApp is Rejected anyway).

The remaining ~38 Approved items match the build: .NET 10, SQL Server, Windows Server 2022,
AR/EN `.resx` + RTL, `dd‑MM‑yyyy` dates / Latin digits, Scrum / 2‑week sprints / ceremonies,
team roles, zero‑warning + DRY + peer‑review + freeze governance, unit/integration tests, and the
deliverables list.

## 5. Actions to close before sign‑off

1. **#26 Gemini (Rejected) — decide remove‑vs‑hard‑disable** for the external AI providers and
   document it. Today the rejected integration ships (dormant: Echo default + blank keys) in the
   binary. *Recommendation:* keep the seam, but gate Gemini/OpenAI/Anthropic behind an explicit
   "cyber‑approved" switch that defaults to refusing to start if a real external provider is
   selected without sign‑off.
2. **#58 Performance testing** — strategy now authored (this change); execution + a baseline run
   on staging are the next step (owner/QA).
3. **#42–46 external‑repo / data‑residency** — owner/cyber decision (Azure DevOps vs on‑prem).
4. **#19 disposable‑email domains** — confirm whether disposable domains are blocked; add if not.
5. **#15** — record the email‑code (vs phone‑OTP/magic‑link) divergence on the checklist so cyber
   signs the *as‑built* behaviour.

> This cross‑check is point‑in‑time. Re‑run it before the customer executes the Sign‑Off sheet.
