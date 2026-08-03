# Test-Case Sheet — `Registration Success` (app screen #10)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | تم التسجيل بنجاح · Registration Success | **Doc id** | `TC-MOB-RGS` |
| **Route / screen id** | app screen **#10** `registrationSuccess` — the terminal sign-up confirmation | **Surface** | Mobile app (Flutter) |
| **API under test** | **none** — the screen is offline-safe and performs **no write** | **Audience** | Visitor (pending approval) |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/registration-success/](../../pages/mobile/registration-success/README.md) · [e2e/mobile-registration-success.md](../../tests/e2e/mobile-registration-success.md) · Figma `505:1451` · D-625 (clean-code freeze) | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| **Terminal** confirmation — **offline-safe**, with **no write API** | PAGE-INDEX #10, D-625 |
| A **reference card** showing the registration reference (real reference or a masked form) | PAGE-INDEX #10 |
| **Status** and **home** actions | PAGE-INDEX #10 |
| Contact tiles are **visual only** — they are not interactive affordances | PAGE-INDEX #10, D-625 |

> **This screen is reached exactly once, immediately after the profile save.**
> Its job is to give the user a reference they can quote and a clear next step.
> The most valuable rows are `C02` (the reference is real and correct) and `D01`
> (the screen genuinely writes nothing, so it cannot fail the registration it
> just confirmed).

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Freshly completed registration | complete [sign-up-interests.md](sign-up-interests.md) `TC-MOB-SUI-C01` immediately before this sheet |
| **FX-2** Registration reference | the value the backend assigned — read it from the account record to compare against the screen |
| Offline | airplane mode, for the offline-safety rows |
| Cleanup | the account created is tagged `QA-`; added to the cleanup register. |

> **Deployment note.** A missing registration-reference sequence in the database
> has previously caused account creation to fail in production. If the reference
> on this screen is blank or malformed, check the sequence before raising it as
> a screen defect.

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | **N-A** — no data load | | |
| CB-04 Auth gate and account state | yes — pending-account surface | | |
| CB-05 Session expiry and token refresh | partial — the screen itself makes no call | | |
| CB-06 Network failure and retry | yes — must work **offline** | | |
| CB-07 Server 500 and malformed payload | **N-A** — no API call | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A** — no server data | | |
| CB-10 Audit trail | **N-A** — no write | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGS-A01` | Screen chrome | P1 | Arrive after completing registration. | The success mark, the confirmation heading and body, the **reference card**, the status and home actions, and the visual contact tiles. | Figma `505:1451` | | | | |
| `TC-MOB-RGS-A02` | Reference card is legible | P0 | Read the reference card. | The reference is displayed clearly and is large enough to read and transcribe. A reference the user cannot read defeats the screen's purpose. | PAGE-INDEX #10 | | | | |
| `TC-MOB-RGS-A03` | Contact tiles are visual only | P1 | Tap each contact tile. | They are **not** interactive — nothing happens, and they do not look like tappable buttons that then do nothing. Record any tile that appears actionable but is inert. | D-625 | | | | |
| `TC-MOB-RGS-A04` | Tablet width | P1 | Open on a tablet in portrait. | The card and actions fill the frame proportionately; no dead side gutters. | responsive rule §13.7 | | | | |
| `TC-MOB-RGS-A05` | Small phone | P1 | Open on the smallest handset in the matrix. | Nothing is clipped; both actions remain reachable without scrolling past the reference. | CB-01.3 | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGS-C01` | Reached only after a successful save | P0 | Complete registration as **FX-1**. | The screen appears **only** after the profile save succeeded. It must never be reachable after a failed save — that would tell a user they registered when they did not. | `E2E-MOB7A-001` | | | | |
| `TC-MOB-RGS-C02` | **Reference is real and correct** | P0 | Compare the displayed reference against **FX-2** in the account record. | They match. If the screen shows a masked form, confirm the mask is derived from the real reference and is still sufficient for support to identify the account. | PAGE-INDEX #10 | | | | |
| `TC-MOB-RGS-C03` | Status action | P0 | Tap the status action. | Opens the **registration status** screen for this account. | PAGE-INDEX #10→#11 | | | | |
| `TC-MOB-RGS-C04` | Home action | P0 | Tap the home action. | Opens Home. As a **pending** account, no approval-gated content is reachable from there. | CB-04.4 | | | | |
| `TC-MOB-RGS-C05` | Terminal — no way back into the form | P0 | Use the back gesture, the hardware back button and the chevron if present. | The user cannot navigate back into the sign-up form. Registration is complete; re-entering the form from here would be confusing and could produce a duplicate submission. | PAGE-INDEX #10 ("terminal") | | | | |
| `TC-MOB-RGS-C06` | Not re-reachable later | P1 | Complete registration, go Home, then force-quit and relaunch. | The confirmation screen is **not** shown again. It is a one-time surface. | — | | | | |
| `TC-MOB-RGS-C07` | Double-tap an action | P1 | Tap the status action twice rapidly. | The target screen opens once, not twice. | CB-06.5 | | | | |

### D. Resilience and security

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGS-D01` | **No write API** | P0 | Capture the network traffic while the screen is open. | The screen issues **no** write request. It only presents the outcome of the save that already happened. | D-625 | | | | |
| `TC-MOB-RGS-D02` | **Offline-safe** | P0 | Complete registration, then enable airplane mode before the screen renders — or open it with the network down. | The screen renders fully. It has no network dependency and must never show an error or a spinner. | D-625, CB-06.1 | | | | |
| `TC-MOB-RGS-D03` | Reference is not sensitive in the log | P0 | Capture the device log. | The registration reference may be logged only if the audit policy permits it; no token, password or identity document appears. | A9-9 | | | | |
| `TC-MOB-RGS-D04` | Pending state is enforced downstream | P0 | From C04, attempt to reach approval-gated content. | Refused. Reaching this screen confirms **registration**, not **approval**. | CB-04.4, `T-09` | | | | |
| `TC-MOB-RGS-D05` | Screen is account-scoped | P0 | Confirm the reference shown belongs to the signed-in account. | The screen never shows another account's reference, including after a fast account switch. | A1 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGS-F01` | Arabic RTL | P0 | Run the sheet in Arabic. | Heading, body, reference card, actions and tiles mirror correctly. | CB-02.1 | | | | |
| `TC-MOB-RGS-F02` | Reference direction | P0 | Read the reference in Arabic. | The reference renders **left-to-right** and is not reversed or reordered — a transposed reference is useless to support. | CB-02.5 | | | | |
| `TC-MOB-RGS-F03` | No hardcoded string | P0 | Compare every visible string in both languages. | Heading, body, reference label, both action labels and the contact tiles are translated. | CB-02.3 | | | | |
| `TC-MOB-RGS-F04` | Accessible names | P1 | Screen reader on; traverse the screen. | The reference is announced clearly (ideally character by character); both actions announce their labels; the inert tiles are not announced as buttons. | CB-08.1 | | | | |
| `TC-MOB-RGS-F05` | Success is not colour-only | P1 | Inspect the success indication. | Success is conveyed by text and icon, not only by a green fill. | CB-08.4 | | | | |
| `TC-MOB-RGS-F06` | Text scaling | P2 | Largest supported font size. | The reference stays fully visible and both actions remain reachable. | CB-08.5 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGS-H01` | The user leaves with a usable reference and a next step | P0 | Run C02 → C04. | The reference is correct and readable, and both onward routes work. | **FR-2xx** | | | | |
| `TC-MOB-RGS-H02` | The screen cannot mislead | P0 | Run C01, C05, D01, D02, D04. | It appears only on real success, writes nothing, works offline, cannot be re-entered into the form, and does not imply approval. | **NFR-01** | | | | |
| `TC-MOB-RGS-H03` | Design parity | P1 | Compare the live render against Figma `505:1451`. | Success mark, card, actions, tiles, typography and spacing match. | DoD-Gate-4 | | | | |
| `TC-MOB-RGS-H04` | Live-render gate | P0 | Capture a full screenshot, the device log and the network list. | Screenshot captured; **zero** console errors; **zero** failed assets; **zero** network requests; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-RGS-H05` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (28 authored + 5 applicable inherited blocks) | |
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
| Offline render verified | | |
| Evidence captured for every PASS and FAIL | | |
| Every account created has been removed | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set**, and include
[sign-up-interests.md](sign-up-interests.md) and
[registration-status.md](registration-status.md) — this screen sits between them.

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
| 1.0 | 2026-08-03 | SIMF Team | First issue. Grounded in `PAGE-INDEX.md` #10 and D-625. |

---

_Authored:_ 2026-08-03 · _Last reviewed:_ 2026-08-03
