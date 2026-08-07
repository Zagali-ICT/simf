# Test-Case Sheet — `{Screen Name}`

> Copy this file to `docs/test-case-plan/{cp|web|mobile}/{screen-slug}.md`.
> Author sections 1–4 **once** from the source (never guess a value); the tester
> fills the **bold** columns during the run. One sheet = one screen = one run.
>
> **A new build is a new run.** Copy the sheet (or add a dated run block) rather
> than overwriting a previous result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | `{Screen Name}` | **Doc id** | `TC-{SURFACE}-{SLUG}` |
| **Route / screen id** | `{/route or #NN screenName}` | **Surface** | Control Panel / Website / Mobile app |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Browser or device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | page doc · E2E catalogue file · FDS spec · permission code | | |

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| Account(s) needed | _(role, account state, permissions)_ |
| Data fixtures | _(rows that must exist)_ |
| Second factor | Admin TOTP via the `Get-Totp` helper. Visitor OTP read from `SIMF_Identity.AccountCodes` at run time. |
| Network / device | _(e.g. Huawei no-GMS handset for camera paths)_ |
| Cleanup | Anything created is tagged `QA-` and added to the cleanup register. |

> **No literal secret appears in this document.** Never paste a password, TOTP
> secret, OTP value or bearer token into a sheet or an evidence file.

## 3. Inherited common cases

Run the blocks below from [`_COMMON-CASES.md`](../_COMMON-CASES.md). Record one
status per block; a failure inside a block gets its own ledger row in §6.

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes / no | | |
| CB-02 Arabic RTL and English LTR | yes / no | | |
| CB-03 Loading, empty and error states | yes / no | | |
| CB-04 Auth gate and account state | yes / no | | |
| CB-05 Session expiry and token refresh | yes / no | | |
| CB-06 Network failure and retry | yes / no | | |
| CB-07 Server 500 and malformed payload | yes / no | | |
| CB-08 Accessibility baseline | yes / no | | |
| CB-09 Pull-to-refresh (mobile data screens) | yes / no | | |
| CB-10 Audit trail | yes / no | | |

## 4. Test cases

Sections A–H below. Author only the rows that apply to this screen; delete the
section heading if the screen has none. Priority: **P0** security / data loss /
golden path · **P1** important function · **P2** cosmetic or rare.

**Status vocabulary:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Type | Pri | Pre-condition | Steps | Test data | Expected result | Refs | **Actual result** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `TC-…-A01` | | render | P1 | | | | | | | | | |

### B. Field validation (client)

| ID | Field | Type | Pri | Pre-condition | Steps | Test data | Expected result | Refs | **Actual result** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `TC-…-B01` | | validation | P1 | | | | | | | | | |

### C. Functional and business rules

| ID | Action | Type | Pri | Pre-condition | Steps | Test data | Expected result | Refs | **Actual result** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `TC-…-C01` | | functional | P0 | | | | | | | | | |

### D. Server-side and NCA security

> Every row here is run **against the API**, not only through the UI. A rule
> enforced only in the client is a defect, not a pass.

| ID | Control | Type | Pri | Pre-condition | Steps | Test data | Expected result | Refs | **Actual result** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `TC-…-D01` | | security | P0 | | | | | | | | | |

### E. Error handling and resilience

| ID | Condition | Type | Pri | Pre-condition | Steps | Test data | Expected result | Refs | **Actual result** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `TC-…-E01` | | error | P1 | | | | | | | | | |

### F. Accessibility and localisation

| ID | Area | Type | Pri | Pre-condition | Steps | Test data | Expected result | Refs | **Actual result** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `TC-…-F01` | | i18n / a11y | P1 | | | | | | | | | |

### G. Data integrity and audit

| ID | Area | Type | Pri | Pre-condition | Steps | Test data | Expected result | Refs | **Actual result** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `TC-…-G01` | | audit | P1 | | | | | | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Type | Pri | Pre-condition | Steps | Test data | Expected result | Refs | **Actual result** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `TC-…-H01` | | acceptance | P0 | | | | | | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (incl. inherited blocks) | |
| PASS | |
| FAIL | |
| BLOCKED | |
| N-A | |
| NOT-RUN | |
| **Pass rate** (PASS / (PASS+FAIL)) | |

| Exit criterion | Met? | Note |
|---|---|---|
| Every **P0** case is PASS | | |
| No open **high-severity** defect | | |
| Both language runs completed | | |
| Evidence captured for every PASS and FAIL | | |
| Cleanup register drained | | |

## 6. Defect, fix and re-test ledger

One row per failed case. Reopen the same defect id if a re-test fails — never
raise a second id for the same fault.

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set**, not only the failed case,
and record the regression outcome here.

## 7. Sign-off

| Role | Name | Date | Verdict |
|---|---|---|---|
| Tester | | | Accept / Reject |
| QA Lead | | | Accept / Reject |
| Developer | | | Fixes complete: yes / no |
| Owner | | | Accepted for release: yes / no |

## 8. Revision history

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | `YYYY-MM-DD` | | First issue. |

---

_Authored:_ `YYYY-MM-DD` · _Last reviewed:_ `YYYY-MM-DD`
