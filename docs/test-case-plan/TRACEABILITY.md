# Traceability rollup — screens ↔ controls ↔ requirements

> **Purpose.** Two-way traceability, per [`SIMF-TST-001`](../SIMF-TST-001-Test-Plan.md) §6:
> *"Every requirement (`FR-`, `NFR-`) and every use case (`UC-`) is traceable to
> the tests that cover it… A test gap against an accepted requirement is itself
> a defect."*
>
> Each sheet carries its own refs per case. This file is the **rollup** in the
> other direction: given a control or a requirement, which screens prove it.
> It is filled as sheets are authored and is gap-checked at the end of each phase.

**Filled through:** Phase 1 batches A (authentication and onboarding) and B
(sign-up and registration) — 14 mobile sheets.

---

## 1. NCA control → screens

Control ids follow the SIMF NCA AppSec gap analysis
([`docs/security/SIMF-NCA-AppSec-Standard-GapAnalysis-2026-06-20.md`](../security/SIMF-NCA-AppSec-Standard-GapAnalysis-2026-06-20.md)),
domains `A1`–`A11` plus `§1`–`§3`. There is no `ECC-1-2-3` scheme in this repo.

| Control | Requirement (short) | Proven by | Where |
|---|---|---|---|
| A1 (access control) | Authorization enforced server-side, not only in the client | every sheet | `CB-04.3` |
| A1-12 | Log all failed access-control decisions | every sheet | `CB-04.5`, `CB-10.2` |
| A3 (input validation) | Injection / input validation | every form sheet | §B + §D rows |
| A4-10 | Per-user business limits, no duplicate submission | every write sheet | `CB-06.5` |
| A7-2 | Password masked, autocomplete disabled | sign-in, reset-password, badge-password | §D |
| A7-8 | Brute force: account **and** IP protection | sign-in, forgot-password, verify-otp, badge-password | §D |
| A7-10, A7-28, A7-29 | Password strength, no common / repeated / sequential | reset-password, sign-up-form, badge-activation | §B boundary rows |
| A7-13, A7-20 | Password expiry, previous-password reuse | reset-password | §D (**N-A unless configured** — see §3) |
| A7-22 | Step-up before sensitive operations | biometric-step-up, change-email | §D |
| A7-31 | Notify last account use on login | sign-in | §D |
| A7-36 | Session identifier never in a URL | every signed-in sheet | `CB-05.5` |
| A7 (enumeration) | Response must not reveal whether an account exists | forgot-password, sign-in, badge-password | §D |
| A8 | Software / data integrity; audit not user-editable | every write sheet | `CB-10.4` |
| A9-7 | Each event logged with timestamp, identity, IP, description | every write sheet | `CB-10.1` |
| A9-9 | Do not log protected information | every sheet | `CB-10.3` |
| A9-15 | Validation failures are audited | every form sheet | `CB-10.2` |
| A11 | Mobile verification requirements | every mobile sheet | §D + §F |

## 2. Requirement → screens

Requirement ids from [`SIMF-SRS-001`](../SIMF-SRS-001-Software-Requirements-Specification.md) v1.1 (87 FR, 11 NFR, 5 EIR).

| Requirement | Short text | Proven by |
|---|---|---|
| FR-101 | Create an account with email, password and confirmation | `mobile/sign-up-form.md` |
| FR-102 | Six-digit verification code emailed; required before proceeding | `mobile/email-otp.md` |
| FR-104 | Authenticate by email and password; no Nafath / Face-ID sign-in | `mobile/sign-in.md` |
| FR-105 | TOTP second factor for every Control Panel sign-in | Phase 2 — `cp/login-totp.md` |
| FR-107 | Password reset by emailed verification code | `mobile/forgot-password.md`, `mobile/reset-password.md` |
| NFR-01 | Meet the NCA Secure Application Development Standard | §1 above, across all sheets |

_Remaining FR / NFR rows are added as their screens are authored._

## 3. Known configuration caveats — do not raise a false defect

| Setting | Default | Effect on testing |
|---|---|---|
| `PasswordMaxAgeDays` | `0` (disabled) | Password-expiry cases are `N-A` unless the environment sets a non-zero value. |
| `PasswordHistoryCount` | `0` (disabled) | Previous-password-reuse cases are `N-A` unless configured. |
| `DormantAccountDisableDays` | `0` (disabled) | Dormant-account cases are `N-A` unless configured. |

Confirm the environment's values before the run and record them in each sheet's §1.

## 4. Gap check

Run at the end of each phase. Any requirement or control in scope for an
authored screen that has **zero** cases is listed here as a defect against the
test pack itself.

| Gap | Screen(s) affected | Raised | Status |
|---|---|---|---|
| _(none recorded yet)_ | | | |

---

_Authored:_ 2026-08-01 · _Last reviewed:_ 2026-08-01
