# Test-Case Sheet — `Registration Status` (app screen #11)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | حالة التسجيل · Registration Status | **Doc id** | `TC-MOB-RGT` |
| **Route / screen id** | app screen **#11** `registrationStatus` | **Surface** | Mobile app (Flutter) |
| **API under test** | `GET /app/users/me` | **Audience** | Visitor (pending / rejected / disabled) — the gate surface for a non-approved account |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/registration-status/](../../pages/mobile/registration-status/README.md) · [e2e/mobile-registration-status.md](../../tests/e2e/mobile-registration-status.md) · Figma `1701:3789` · D-623 (clean-code freeze) | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| Status is read from `GET /app/users/me` — the **server** decides, not the client | PAGE-INDEX #11 |
| **Pull-to-refresh is deliberately absent.** This is the documented exception to the app-wide pull-to-refresh rule: it is a gate screen whose explicit **Re-check** button already polls. | app rules §13.6 exception |
| Figma `1701:3789`; decomposed to 5 widgets under D-623 | PAGE-INDEX #11 |

> **This is a containment screen.** Its entire purpose is to hold a non-approved
> account somewhere safe while it waits. The rows that matter are `D01`–`D04`:
> a pending account must reach **nothing** beyond this surface.

> **Do not raise the missing pull-to-refresh as a defect.** It is a recorded,
> deliberate exception — the Re-check button is the refresh mechanism.
> `TC-MOB-RGT-C04` verifies the button works instead.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** `PendingApproval` account | a completed registration awaiting approval |
| **FX-2** `Rejected` account with a reason | rejected from the Control Panel with a stated reason |
| **FX-3** `Disabled` account | disabled from the Control Panel |
| **FX-4** Account approved **while the screen is open** | approve **FX-1** from the Control Panel mid-run, for the transition rows |
| Control Panel access | a second tester or a second device is needed to flip the account state during C05 and C06. |
| Cleanup | accounts tagged `QA-`; added to the cleanup register. |

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | yes | | |
| CB-04 Auth gate and account state | yes — this screen **is** the account-state gate | | |
| CB-05 Session expiry and token refresh | yes | | |
| CB-06 Network failure and retry | yes | | |
| CB-07 Server 500 and malformed payload | yes | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A — deliberate exception** (§13.6); see C04 | | |
| CB-10 Audit trail | partial — no write from this screen | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGT-A01` | Screen chrome | P1 | Sign in as **FX-1**. | The status screen renders with its state indicator, explanatory copy, the **Re-check** action and a sign-out action. | Figma `1701:3789` | | | | |
| `TC-MOB-RGT-A02` | Pending presentation | P0 | Open as **FX-1**. | The screen clearly says the account is **awaiting approval** and what happens next. A user must not be left guessing whether something went wrong. | CB-04.4 | | | | |
| `TC-MOB-RGT-A03` | Rejected presentation | P0 | Open as **FX-2**. | The screen states the account was **rejected** and shows the **reason verbatim**, plus the rejection timestamp if provided. | `T-10` | | | | |
| `TC-MOB-RGT-A04` | Disabled presentation | P0 | Open as **FX-3**. | The screen states the account is **disabled**. It does not present a pending or rejected message instead. | CB-04.4 | | | | |
| `TC-MOB-RGT-A05` | Loading state | P1 | Open on a throttled connection. | A loading state shows while `GET /app/users/me` is in flight — never a blank screen and never a stale status presented as current. | CB-03.1 | | | | |
| `TC-MOB-RGT-A06` | Tablet width | P1 | Open on a tablet in portrait. | Content fills the frame proportionately; no dead side gutters. | responsive rule §13.7 | | | | |
| `TC-MOB-RGT-A07` | Long rejection reason | P1 | Open as **FX-2** with a long, multi-line reason. | The reason wraps and is fully readable; nothing is truncated or clipped. | CB-01.3 | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGT-C01` | Status comes from the server | P0 | Open as **FX-1** and inspect the network traffic. | The state shown is derived from `GET /app/users/me`. The client does not decide the account state from a cached or token-embedded value. | PAGE-INDEX #11 | | | | |
| `TC-MOB-RGT-C02` | Each state routes here | P0 | Sign in as **FX-1**, **FX-2** and **FX-3** in turn. | All three land on this surface (or their dedicated state surface) and **none** reaches app content. | CB-04.4, `T-09`, `T-10` | | | | |
| `TC-MOB-RGT-C03` | Sign-out is available | P0 | Tap the sign-out action. | The session ends and the user returns to sign-in. **A user must never be trapped on this screen** with no way out. | CB-05.4 | | | | |
| `TC-MOB-RGT-C04` | **Re-check polls** | P0 | Tap **Re-check**. | A fresh `GET /app/users/me` fires and the displayed state updates. This is the screen's refresh mechanism in place of pull-to-refresh. | §13.6 exception | | | | |
| `TC-MOB-RGT-C05` | **Approval is picked up** | P0 | With **FX-1** open, approve the account from the Control Panel, then tap **Re-check**. | The screen recognises the approval and the user is routed **into the app**. This is the single most important transition on the screen. | CB-04.4 | | | | |
| `TC-MOB-RGT-C06` | Rejection is picked up | P0 | With **FX-1** open, reject the account from the Control Panel, then tap **Re-check**. | The screen switches to the rejected presentation with the reason. | `T-10` | | | | |
| `TC-MOB-RGT-C07` | No pull-to-refresh — by design | P1 | Pull down on the screen. | Nothing happens. This is the **documented exception** to the app-wide pull-to-refresh rule, not a defect. Confirm the Re-check button is discoverable enough to compensate. | §13.6 exception | | | | |
| `TC-MOB-RGT-C08` | Repeated Re-check | P1 | Tap **Re-check** several times in quick succession. | One request per tap at most, no request storm, and the button disables while a check is in flight. | CB-06.5 | | | | |
| `TC-MOB-RGT-C09` | Survives an app restart | P0 | Force-quit and relaunch as **FX-1**. | The user lands back on this screen — not on Home and not on a blank surface. | `TC-MOB-SPL-C05` | | | | |
| `TC-MOB-RGT-C10` | Back gesture does not escape | P0 | Use the OS back gesture and the hardware back button. | The user cannot back out of the gate into app content. | CB-04.2 | | | | |

### D. Server-side and NCA security

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGT-D01` | **Pending reaches no content** | P0 | As **FX-1**, call the protected app endpoints directly with the account's token — sessions, seat reservation, questions, contacts, badge. | Every one is **refused**. The client-side gate is not the only protection. | CB-04.3, `T-09` | | | | |
| `TC-MOB-RGT-D02` | Rejected reaches no content | P0 | Repeat D01 as **FX-2**. | Every one refused. | `T-10` | | | | |
| `TC-MOB-RGT-D03` | Disabled cannot even hold a session | P0 | Disable an account that is currently signed in, then use its token. | The token stops working. A disabled account must not continue operating on a token issued before it was disabled. | A7, A1-19 | | | | |
| `TC-MOB-RGT-D04` | Deep links do not bypass the gate | P0 | As **FX-1**, open a deep link straight into a protected screen. | Refused or redirected to this gate. A deep link must not be a way around the account-state check. | CB-04.2 | | | | |
| `TC-MOB-RGT-D05` | Response leaks nothing extra | P0 | Inspect the `GET /app/users/me` response for **FX-1** and **FX-2**. | It carries the caller's own state and profile only — no other user's data, no internal identifiers beyond what the app needs. | A9-9 | | | | |
| `TC-MOB-RGT-D06` | Rejection reason is the operator's text | P1 | Compare the displayed reason against the value recorded in the Control Panel. | Shown **verbatim** — not paraphrased, truncated or replaced with a generic message. | `T-10` | | | | |
| `TC-MOB-RGT-D07` | Rejection reason cannot inject | P0 | Reject an account with a reason containing markup, script tags and control characters, then view it here. | Rendered as **text**. No markup is executed and the layout is not broken. | A3 | | | | |
| `TC-MOB-RGT-D08` | Session expiry on the gate | P0 | Sit on the screen past the access-token lifetime, then tap Re-check. | The token refreshes silently and the check succeeds. At the absolute session cap the user is signed out cleanly to sign-in. | CB-05.1, CB-05.2 | | | | |
| `TC-MOB-RGT-D09` | Transport | P0 | Capture the request. | TLS only. | A5 | | | | |
| `TC-MOB-RGT-D10` | No secret in the log | P0 | Capture the device log across a run. | No token or personal identifier is printed. | A9-9 | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGT-E01` | Offline | P0 | Network off, open the screen and tap Re-check. | A clear offline state with a retry. **The user is not signed out** merely because the network is down, and is not shown a misleading state. | CB-06.1 | | | | |
| `TC-MOB-RGT-E02` | Recovery | P1 | Restore the network and tap Re-check. | The current state loads correctly. | CB-06.3 | | | | |
| `TC-MOB-RGT-E03` | Server 500 | P1 | Force a 500 on `GET /app/users/me`. | An error state with a retry; no crash; no stack trace shown; the user is not silently let through the gate. | CB-07.1, CB-07.2 | | | | |
| `TC-MOB-RGT-E04` | **Fail-closed** | P0 | Force the status call to fail in every way — 500, timeout, malformed body. | Under **no** failure mode does the screen let the user into app content. A gate that fails open is a high-severity defect. | CB-04, A1 | | | | |
| `TC-MOB-RGT-E05` | Malformed payload | P1 | Return a response with a missing or unknown state value. | Degrades to a safe state with an error — never to "approved". | CB-07.3 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGT-F01` | Arabic RTL | P0 | Run the sheet in Arabic. | The state indicator, copy, reason block and both actions mirror correctly. | CB-02.1 | | | | |
| `TC-MOB-RGT-F02` | No hardcoded string | P0 | Compare every visible string across all three states in both languages. | Every state message, the Re-check label, the sign-out label and every error are translated. | CB-02.3 | | | | |
| `TC-MOB-RGT-F03` | Rejection reason language | P1 | View **FX-2** in both languages. | Record how a reason entered in one language is presented in the other. An operator-entered reason will not be translated — confirm it is at least clearly attributed rather than looking like a broken translation. | CB-02.3 | | | | |
| `TC-MOB-RGT-F04` | Accessible names | P1 | Screen reader on; traverse the screen. | The state is announced as text; Re-check and sign-out announce their labels; a state change after Re-check is announced. | CB-08.1, CB-08.3 | | | | |
| `TC-MOB-RGT-F05` | State is not colour-only | P0 | Compare the pending, rejected and disabled presentations. | Each is distinguished by **text and icon**, not only by colour. | CB-08.4 | | | | |
| `TC-MOB-RGT-F06` | Text scaling | P2 | Largest supported font size. | The copy and a long rejection reason reflow; both actions stay reachable. | CB-08.5 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RGT-H01` | The gate holds | P0 | Run C02, C10, D01 → D04, E04. | No non-approved account reaches any app content by any route, and the gate fails closed. | **NFR-01**, A1 | | | | |
| `TC-MOB-RGT-H02` | The user is informed and not trapped | P0 | Run A02 → A04, C03, C04. | Each state is explained, the user can re-check, and can always sign out. | **FR-2xx** | | | | |
| `TC-MOB-RGT-H03` | Approval is honoured promptly | P0 | Run C05. | An approved account gets into the app on the next check. | **FR-2xx** | | | | |
| `TC-MOB-RGT-H04` | Design parity | P1 | Compare the live render against Figma `1701:3789`. | Typography, spacing, state presentation and actions match. | DoD-Gate-4 | | | | |
| `TC-MOB-RGT-H05` | Live-render gate | P0 | Capture a screenshot of all three states, the device log and the network list. | Screenshots captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-RGT-H06` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (38 authored + 8 applicable inherited blocks) | |
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
| All three account states exercised | | |
| The approval transition (C05) verified end to end | | |
| Both language runs completed | | |
| Evidence captured for every PASS and FAIL | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set.** This screen enforces the
account-state gate, so a change here can affect every role-gated route —
include [sign-in.md](sign-in.md) and [splash.md](splash.md) in the regression pass.

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
| 1.0 | 2026-08-03 | SIMF Team | First issue. Grounded in `PAGE-INDEX.md` #11, D-623 and the §13.6 pull-to-refresh exception. |

---

_Authored:_ 2026-08-03 · _Last reviewed:_ 2026-08-03
