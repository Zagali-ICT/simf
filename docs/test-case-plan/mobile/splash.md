# Test-Case Sheet — `Splash` (app screen #1)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | شاشة البداية · Splash | **Doc id** | `TC-MOB-SPL` |
| **Route / screen id** | app screen **#1** `splash` — the app's launch surface | **Surface** | Mobile app (Flutter) |
| **API under test** | `GET /app/version-policy` — the launch update check (D-736) | **Audience** | Guest (runs before any authentication) |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill — include a cold boot on a low-end handset)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/splash/](../../pages/mobile/splash/README.md) · [e2e/mobile-splash.md](../../tests/e2e/mobile-splash.md) · Figma `159:573` · D-736 (version policy), D-641 (clean-code freeze) | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| Server version-policy gate: forced-update and soft-update dialogs | D-736, `PAGE-INDEX.md` #1 |
| **5-second fail-open cap** — a slow or unreachable policy call must not block launch | D-736 |
| **3-day soft-update snooze** | D-736 |
| Logo precached; the loading state is pinned so no boot timers fire during the render lock | D-641 |

> **This screen is the app's front door.** Every failure here is a launch
> failure, so almost everything is **P0**. A splash that can hang is worse than
> a splash that shows a stale version notice.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Policy: up to date | the installed version satisfies the server policy |
| **FX-2** Policy: **soft** update available | an optional newer version is published |
| **FX-3** Policy: **forced** update required | the installed version is below the enforced minimum |
| **FX-4** Policy endpoint unreachable | block `\/app\/version-policy` at the network layer |
| **FX-5** Policy endpoint very slow | throttle the endpoint to respond in more than 5 seconds |
| **FX-6** First run | app freshly installed, no stored session, no snooze recorded |
| **FX-7** Returning signed-in user | a remembered session exists |
| **FX-8** Returning signed-out user | a previous install with no session |
| Cleanup | Reinstalling clears the snooze; record when you reset it. |

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | yes — this screen **is** a loading state | | |
| CB-04 Auth gate and account state | yes — it routes by session and account state | | |
| CB-05 Session expiry and token refresh | yes — a stored session may be expired at launch | | |
| CB-06 Network failure and retry | yes | | |
| CB-07 Server 500 and malformed payload | yes | | |
| CB-08 Accessibility baseline | partial — no interactive control except the dialogs | | |
| CB-09 Pull-to-refresh | **N-A** | | |
| CB-10 Audit trail | **N-A** — no write action | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SPL-A01` | Cold-start render | P0 | Force-quit the app, then launch it. | The splash renders the brand mark and its loading state immediately. **No** white or black flash, no un-styled frame, no visible layout jump before the logo appears. | Figma `159:573`; D-641 (logo precached) | | | | |
| `TC-MOB-SPL-A02` | Low-end device | P0 | Cold-start on the slowest handset in the matrix. | The splash still appears immediately and the app still reaches its destination. Record the time to first paint and the time to route. | CB-01 | | | | |
| `TC-MOB-SPL-A03` | Tablet | P1 | Cold-start on a tablet in portrait. | The brand mark is centred and correctly proportioned — not stretched, pixelated or pinned to a phone-width box. | responsive rule §13.7 | | | | |
| `TC-MOB-SPL-A04` | Orientation | P1 | Launch while the device is held in landscape. | The app is portrait-locked; the splash renders in portrait without distortion. | `main.dart` portrait lock | | | | |
| `TC-MOB-SPL-A05` | System dark / light | P2 | Launch under both system themes. | The splash renders correctly in both; the logo remains legible against its background. | CB-01 | | | | |
| `TC-MOB-SPL-A06` | No horizontal overflow | P1 | Inspect the rendered screen. | No content is clipped at any edge. | CB-01.3 | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SPL-C01` | First-run routing | P0 | Launch as **FX-6**. | Routes to **onboarding** (#2). | PAGE-INDEX #1→#2 | | | | |
| `TC-MOB-SPL-C02` | Returning signed-out routing | P0 | Launch as **FX-8**. | Routes to **sign-in** (#3) — onboarding is not shown again. | — | | | | |
| `TC-MOB-SPL-C03` | Returning signed-in routing | P0 | Launch as **FX-7**. | The stored session is restored and the app routes onward without asking for a password. | `E2E-MOB003-015` | | | | |
| `TC-MOB-SPL-C04` | Expired session at launch | P0 | Launch with a stored session whose refresh window has passed. | The app routes to **sign-in** cleanly — no crash, no infinite spinner, no half-signed-in state. | CB-05.3 | | | | |
| `TC-MOB-SPL-C05` | Account state at launch | P0 | Launch as a `PendingApproval`, then a `Rejected`, then a `Disabled` account. | Each is routed to its own state surface. **None** reaches app content. | CB-04.4 | | | | |
| `TC-MOB-SPL-C06` | Up-to-date policy | P0 | Launch as **FX-1**. | No update dialog appears; the app routes onward normally. | D-736 | | | | |
| `TC-MOB-SPL-C07` | **Soft update** dialog | P0 | Launch as **FX-2**. | A dismissible soft-update dialog appears offering the store and a "later" action. Choosing **later** proceeds into the app normally. | D-736 | | | | |
| `TC-MOB-SPL-C08` | **Soft-update snooze** | P0 | After C07 choose "later", then relaunch immediately, then again after **3 days**. | The dialog does **not** reappear during the 3-day snooze. It reappears once the snooze has elapsed. | D-736 | | | | |
| `TC-MOB-SPL-C09` | **Forced update** dialog | P0 | Launch as **FX-3**. | A **non-dismissible** dialog appears. The user **cannot** proceed into the app by tapping outside it, by the back gesture, or by the hardware back button. The only route forward is the store. | D-736 | | | | |
| `TC-MOB-SPL-C10` | Forced update cannot be bypassed | P0 | As **FX-3**, attempt every dismissal route: back gesture, back button, tapping the scrim, backgrounding and returning, and a deep link into another screen. | The gate holds in **every** case. A forced update that can be bypassed by a deep link is a defect. | D-736 | | | | |
| `TC-MOB-SPL-C11` | Store link | P1 | Tap the update action in C07 and C09. | The correct store listing for this app opens. | D-736 | | | | |
| `TC-MOB-SPL-C12` | Version comparison correctness | P0 | Exercise the policy with versions above, equal to and below the enforced minimum, including a multi-digit component (for example `1.10.0` against `1.9.0`). | The comparison is **numeric per component**, not lexical — `1.10.0` must be treated as newer than `1.9.0`. | D-736 | | | | |

### D. Server-side, resilience and NCA security

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SPL-D01` | **Fail-open cap** | P0 | Launch as **FX-5** (endpoint slower than 5 seconds). | The app waits at most **5 seconds** and then continues into the app regardless. A slow policy service must never make the app unusable. Record the measured wait. | D-736 | | | | |
| `TC-MOB-SPL-D02` | Endpoint unreachable | P0 | Launch as **FX-4**. | The app continues normally. No error dialog blocks launch and no crash occurs. | D-736 | | | | |
| `TC-MOB-SPL-D03` | Fully offline launch | P0 | Enable airplane mode, then cold-start. | The app launches and routes to a usable surface. A returning signed-in user is not signed out merely because the network is down. | CB-06.1 | | | | |
| `TC-MOB-SPL-D04` | Policy endpoint 500 | P0 | Force a 500 from the policy endpoint. | The app continues into the app; no crash; no user-visible stack trace. | CB-07.1 | | | | |
| `TC-MOB-SPL-D05` | Malformed policy payload | P0 | Return a policy body with a missing field, a wrong type, and a null. | The app degrades to "no update required" and continues. It does **not** crash and does **not** lock the user out on garbage input. | CB-07.3 | | | | |
| `TC-MOB-SPL-D06` | Policy call is anonymous and leaks nothing | P0 | Capture the policy request and response. | The request carries no credential and no personal data beyond the app version and platform. The response carries no account data. | A9-9 | | | | |
| `TC-MOB-SPL-D07` | Transport | P0 | Capture the request. | TLS only. | A5 | | | | |
| `TC-MOB-SPL-D08` | Stored session is protected | P0 | Inspect the app's local storage after a remembered sign-in. | Tokens are held in secure storage, not in plain preferences. | A2, A11 | | | | |
| `TC-MOB-SPL-D09` | No secret at launch | P0 | Capture the device log across a cold start. | No token, refresh token or credential is printed. | A9-9 | | | | |
| `TC-MOB-SPL-D10` | Repeated rapid relaunch | P1 | Force-quit and relaunch five times in quick succession. | No crash; no duplicate policy dialogs stacking; no duplicate route push. | CB-06.5 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SPL-F01` | Arabic RTL | P0 | Launch with the app language set to Arabic. | The splash and both update dialogs render RTL with correct Arabic text. | CB-02 | | | | |
| `TC-MOB-SPL-F02` | Language persists across launch | P0 | Set Arabic, force-quit, relaunch. | The app relaunches **in Arabic** — the splash and everything after it. The preference survives a cold start. | `E2E-MOB003-016` | | | | |
| `TC-MOB-SPL-F03` | No hardcoded string | P0 | Read both update dialogs in each language. | Titles, bodies and every button are translated. | CB-02.3 | | | | |
| `TC-MOB-SPL-F04` | Dialog accessibility | P1 | Screen reader on; trigger C07 and C09. | The dialog is announced, its buttons carry labels, and focus is trapped inside the forced-update dialog. | CB-08.1 | | | | |
| `TC-MOB-SPL-F05` | Text scaling | P2 | Largest supported font size; trigger both dialogs. | Dialog text wraps rather than clipping; the actions stay reachable. | CB-08.5 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SPL-H01` | The app always launches | P0 | Run D01 → D05. | Under every policy-service failure mode the app still reaches a usable screen. | **NFR-01** (availability) | | | | |
| `TC-MOB-SPL-H02` | Update policy is enforceable | P0 | Run C07 → C12. | The operator can both suggest and **enforce** an app version, and the enforcement cannot be bypassed. | D-736 | | | | |
| `TC-MOB-SPL-H03` | Design parity | P1 | Compare the live render against Figma `159:573`. | Brand mark, colour and layout match. | DoD-Gate-4 | | | | |
| `TC-MOB-SPL-H04` | Live-render gate | P0 | Capture a screenshot and the device log for a cold start. | Screenshot captured; **zero** console errors; **zero** failed assets. | DoD-Gate-4 | | | | |
| `TC-MOB-SPL-H05` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-SPL-H06` | Device matrix | P0 | Run C01 → C03 and D01 on a standard phone, a **Huawei / no-GMS** handset, a low-end phone and a tablet. | Launch and routing behave identically on every class. | SIMF-MAA-001 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (39 authored + 7 applicable inherited blocks) | |
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
| Device matrix completed | | |
| Evidence captured for every PASS and FAIL | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set.** Splash owns routing at
launch, so the regression pass must also cover [onboarding.md](onboarding.md)
and [sign-in.md](sign-in.md).

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
| 1.0 | 2026-08-01 | SIMF Team | First issue. Grounded in `PAGE-INDEX.md` #1, D-736 and D-641. |

---

_Authored:_ 2026-08-01 · _Last reviewed:_ 2026-08-01
