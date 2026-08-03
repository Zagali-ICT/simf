# Test-Case Sheet — `Verify OTP` — sign-in second factor (app screen #3a)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | رمز التحقق · Verify OTP (sign-in second factor) | **Doc id** | `TC-MOB-OTP` |
| **Route / screen id** | `/auth/verify-otp` (`RouteNames.verifyOtp`) — app screen **#3a** | **Surface** | Mobile app (Flutter) |
| **APIs under test** | `POST /app/auth/verify-otp` · `POST /app/auth/resend-otp` | **Audience** | Mid-sign-in (holds an OTP ticket, no tokens yet) |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/email-otp-verify/](../../pages/mobile/email-otp-verify/README.md) · [e2e/mobile-sign-in.md](../../tests/e2e/mobile-sign-in.md) `E2E-MOB003-003`, `-020` · [FDS-001](../../SIMF-FDS-001-Authentication-and-Login.md) · Sibling: [sign-in.md](sign-in.md) · Figma `758:2616` | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| **6** code boxes, digits-only, numeric keypad | `src/Mobile/simf_app/lib/features/account/widgets/otp_code_boxes.dart` |
| OTP lifetime **10 minutes** | `src/Backend/SIMF.Application/IdentityAccess/SignInService.cs` — `OtpLifetime` |
| Ticket lifetime **5 minutes** | same — `TicketLifetime` |
| **5** second-factor attempts per ticket | same — `MaxSecondFactorAttempts` |
| **5** OTP requests per hour → **429 `RATE_LIMIT_EXCEEDED`** (loud, unlike forgot-password) | same — `MaxOtpRequestsPerWindow`, `OtpRequestWindow` |
| Resend cooldown **120 seconds** | same — `ResendCooldownSeconds` (D-695) |
| Resend re-issues **in place**, keyed by the ticket, no password | `E2E-MOB003-020` |
| `resend-otp` carries the `auth` rate limit only (no per-email partition) | `ResendOtpEndpoint.cs` |
| Visitor second factor is **email OTP**; admin is TOTP | D-033 |

> **Note the asymmetry with forgot-password.** Exceeding the hourly cap here
> returns a **loud 429**; on forgot-password the equivalent cap is **silently
> dropped** (to stay enumeration-safe). Both are correct by design — this screen
> is already past authentication, so there is nothing left to enumerate.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Approved visitor with 2FA on | reached by completing the password step on [sign-in.md](sign-in.md) `TC-MOB-SGI-C03` |
| **FX-2** Visitor with `profileComplete = false` and 2FA on | for the routing row |
| **FX-3** A stale ticket | a ticket held for more than 5 minutes |
| **FX-4** A stale code | a code held for more than 10 minutes |
| Codes | Read from `SIMF_Identity.AccountCodes` or the test mailbox **at run time**. **Never record a code in this sheet or in an evidence file.** |
| API tool | REST client for §D. |
| Timing | The 120-second cooldown and the 5-minute ticket make this sheet slow. Plan roughly 45 minutes per language run. |
| Cleanup | Fixtures tagged `QA-`; added to the cleanup register. |

> **No literal secret appears in this document.**

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | partial — loading only | | |
| CB-04 Auth gate and account state | yes — no tokens exist yet on this screen | | |
| CB-05 Session expiry and token refresh | partial — the **ticket**, not a session | | |
| CB-06 Network failure and retry | yes | | |
| CB-07 Server 500 and malformed payload | yes | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A** — form | | |
| CB-10 Audit trail | yes | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-OTP-A01` | Screen chrome | P1 | Complete the password step as **FX-1**. | Navy chrome; back + centred title; the code mark; instruction body naming the address the code was sent to; **six** code boxes; the verify CTA; the resend row with its countdown. | Figma `758:2616` | | | | |
| `TC-MOB-OTP-A02` | Masked destination | P0 | Read the instruction body. | If the destination address is shown it is **masked**; a full address must not be exposed to a party who only reached this screen with a password. Record exactly what is displayed. | A9-9 | | | | |
| `TC-MOB-OTP-A03` | Box focus advance | P1 | Type six digits in sequence. | Focus advances box to box automatically; backspace moves back and clears. | `otp_code_boxes.dart` | | | | |
| `TC-MOB-OTP-A04` | Countdown starts on arrival | P0 | Arrive on the screen and watch the resend row. | A countdown runs and the resend action is **disabled** until it reaches zero — the cooldown is **120 seconds**. | `ResendCooldownSeconds = 120` (D-695) | | | | |
| `TC-MOB-OTP-A05` | CTA gating | P1 | Enter fewer than six digits. | The verify CTA stays disabled until all six boxes are filled. | code | | | | |
| `TC-MOB-OTP-A06` | Busy state | P1 | Verify on a throttled connection. | CTA busy; boxes disabled; back disabled; no second submit. | code | | | | |
| `TC-MOB-OTP-A07` | Tablet width | P1 | Open on a tablet in portrait. | The six boxes stay proportionate and centred; no dead side gutters; boxes are not stretched into strips. | responsive rule §13.7 | | | | |
| `TC-MOB-OTP-A08` | Keyboard | P1 | Focus a box on a small phone. | The numeric keypad opens; the boxes and the CTA stay visible above it. | CB-01 | | | | |

### B. Field validation (client)

| ID | Field | Pri | Test data | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-OTP-B01` | Code — empty | P0 | _(empty)_ | The CTA is disabled; no request can be made. | code | | | | |
| `TC-MOB-OTP-B02` | Code — partial | P0 | 1–5 digits | The CTA stays disabled; no request. | code | | | | |
| `TC-MOB-OTP-B03` | Code — non-digits rejected at input | P0 | Type `abcdef`, `1a2b3c`, spaces | Non-digits never appear in the boxes. | `keyboardType: number`, digits-only | | | | |
| `TC-MOB-OTP-B04` | Code — Arabic-Indic digits | P1 | Type `١٢٣٤٥٦` on an Arabic keypad | Record the behaviour precisely. If Arabic-Indic digits are silently dropped, an Arabic-keypad user cannot enter their code at all — raise it as a defect. | — | | | | |
| `TC-MOB-OTP-B05` | Code — paste | P1 | Paste a 6-digit code from the clipboard / SMS-style suggestion. | The paste distributes across the six boxes correctly rather than landing entirely in the first box. | `otp_code_boxes.dart` | | | | |
| `TC-MOB-OTP-B06` | Code — overflow | P1 | Attempt a 7th digit | Not accepted; the field is capped at 6. | `maxLength: 6` | | | | |
| `TC-MOB-OTP-B07` | Code — direction | P1 | Run in Arabic. | The boxes read **left-to-right** so the code is entered in the order it was emailed; digits are not reversed. | CB-02 | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-OTP-C01` | Golden path | P0 | Enter the emailed code as **FX-1** and verify. | One `POST /app/auth/verify-otp`; tokens issued; the app hydrates the role from `GET /app/users/me` and routes onward. | `E2E-MOB003-003` | | | | |
| `TC-MOB-OTP-C02` | Profile-completion routing | P0 | Complete C01 as **FX-1**, then as **FX-2**. | The server-computed `profileComplete` flag decides the destination — Home when complete, the profile-completion screen when not. **The same rule as the password path.** | `E2E-MOB003-013` (D-374) | | | | |
| `TC-MOB-OTP-C03` | Wrong code allows retry | P0 | Enter a wrong 6-digit code. | A localized error; the app **stays** on this screen; the boxes clear for a retry; the **ticket remains valid** for the rest of its 5-minute life. The user is not thrown back to sign-in. | `E2E-MOB003-003` | | | | |
| `TC-MOB-OTP-C04` | **Resend re-issues in place** | P0 | Wait out the 120-second countdown, tap resend. | `POST /app/auth/resend-otp` fires, keyed by the **ticket** — **no password is re-entered**. A fresh code is emailed, the countdown restarts, and a confirmation toast appears. The app **does not** bounce back to sign-in. | `E2E-MOB003-020` (#12) | | | | |
| `TC-MOB-OTP-C05` | New code works, old code dies | P0 | After C04, try the **old** code, then the **new** one. | The old code is refused; the new one verifies. | `T-06` | | | | |
| `TC-MOB-OTP-C06` | Resend before the cooldown | P0 | Tap resend while the countdown is still running. | The action is disabled — no request is made. | D-695 | | | | |
| `TC-MOB-OTP-C07` | Back navigation | P1 | Tap back. | Returns to sign-in. The partially-completed sign-in leaves **no** tokens behind. Re-entering requires the password again. | code | | | | |
| `TC-MOB-OTP-C08` | Double submit | P0 | Tap verify twice rapidly. | One request; the attempt counter moves by 1, not 2. | CB-06.5 | | | | |
| `TC-MOB-OTP-C09` | Post-verify biometric nudge | P1 | Complete C01 on a biometric-capable device with Face-ID not yet enabled. | The enrol nudge appears after the **OTP** path too, not only after the password path, and routes to the step-up screen. | `E2E-MOB003-017` | | | | |

### D. Server-side and NCA security

> **Run every row here against the API directly**, not only through the app.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-OTP-D01` | **Attempt cap** | P0 | Submit **6** wrong codes against one ticket. | Attempt 6 is refused. A 6-digit code cannot be brute-forced. | `MaxSecondFactorAttempts = 5`; A7-8 | | | | |
| `TC-MOB-OTP-D02` | Attempt cap is per ticket, not resettable by resend | P0 | Burn 4 attempts, resend a new code, then submit wrong codes again. | Confirm whether resending resets the attempt counter. If it does, the cap is defeatable by repeated resend — record the observed behaviour and raise it if the counter resets. | A7-8 | | | | |
| `TC-MOB-OTP-D03` | **Ticket lifetime** | P0 | Hold **FX-3** past **5 minutes**, then submit a valid code. | The ticket is expired; the code is refused; the user must restart sign-in. | `TicketLifetime = 5 min` | | | | |
| `TC-MOB-OTP-D04` | **Code lifetime** | P0 | Use **FX-4**, older than **10 minutes**, within a fresh ticket. | The code is refused as expired. | `OtpLifetime = 10 min` | | | | |
| `TC-MOB-OTP-D05` | Code is single-use | P0 | Verify successfully, then replay the same code. | Refused. | A7 | | | | |
| `TC-MOB-OTP-D06` | **Hourly OTP request cap** | P0 | Request **6** OTPs for one account within an hour. | The 6th returns **429 `RATE_LIMIT_EXCEEDED`** — loud, not silent. | `MaxOtpRequestsPerWindow = 5`, `OtpRequestWindow = 1 h` | | | | |
| `TC-MOB-OTP-D07` | Per-IP rate limit on resend | P0 | Send **21** resend requests from one IP within 60 seconds. | The 21st returns **429**. Note `resend-otp` carries the `auth` partition **only** — there is **no per-email partition** on this endpoint, unlike sign-in and forgot-password. Record whether the hourly per-account cap (D06) is sufficient compensation. | `ResendOtpEndpoint.cs`; A7-8 | | | | |
| `TC-MOB-OTP-D08` | Invalid ticket | P0 | POST verify-otp with a forged, malformed and already-consumed ticket. | Each is refused with `AUTH_OTP_TOKEN_INVALID`. No tokens are issued. | `E2E-MOB003-020` | | | | |
| `TC-MOB-OTP-D09` | Ticket is not a bearer token | P0 | Attempt to call a protected endpoint using the OTP ticket as a bearer token. | Refused. The ticket authorises only the second-factor exchange. | A1 | | | | |
| `TC-MOB-OTP-D10` | Ticket is bound to its account | P0 | Take account A's ticket and submit account B's valid code, and vice versa. | Refused. A ticket cannot be used to complete sign-in for a different account. | A1, A7 | | | | |
| `TC-MOB-OTP-D11` | Resend requires the ticket, not a password | P0 | POST resend-otp without a ticket, and with another account's ticket. | Refused in both cases. Resend must not be an unauthenticated mail-sending primitive. | `E2E-MOB003-020` | | | | |
| `TC-MOB-OTP-D12` | Server re-validates the code shape | P0 | POST with `""`, `"12345"`, `"1234567"`, `"abcdef"`, a null and a non-string JSON type. | Each is rejected by the **server**. | A3 | | | | |
| `TC-MOB-OTP-D13` | No code, ticket or token in a URL or log | P0 | Inspect navigation URLs, deep links and the device log across a run. | None appear. | A7-36, A9-9 | | | | |
| `TC-MOB-OTP-D14` | Response leaks nothing | P0 | Inspect the failure response body. | It does not reveal the correct code, how many digits matched, how many attempts remain in a way that aids an attacker, or whether the account exists. | A7, A9-9 | | | | |
| `TC-MOB-OTP-D15` | Correct factor per audience | P0 | Confirm the second factor offered to a **visitor** account. | Visitors get an **email OTP**; TOTP is the Control Panel factor. A visitor must not be asked for TOTP and vice versa. | D-033 | | | | |
| `TC-MOB-OTP-D16` | Transport | P0 | Capture the request. | TLS only. | A5 | | | | |
| `TC-MOB-OTP-D17` | Nothing sensitive survives backgrounding | P1 | Enter a code, background the app, inspect the recents thumbnail. | The code is not visible in the recents snapshot and is not retained in unencrypted local storage. | A11 | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-OTP-E01` | Wrong code message | P0 | Trigger C03 in Arabic, then English. | A localized error renders inline; the boxes clear; the screen is usable for a retry. | `E2E-MOB003-019` | | | | |
| `TC-MOB-OTP-E02` | 429 on resend | P0 | Trip D06 through the app. | The **429** surfaces **inline** in the app's language, telling the user to wait — not a silent no-op and not a crash. | `E2E-MOB003-020` | | | | |
| `TC-MOB-OTP-E03` | Expired ticket | P0 | Trip D03 through the app. | A clear localized message and a path back to sign-in. The user is not stranded on a dead screen. | — | | | | |
| `TC-MOB-OTP-E04` | Server 500 | P1 | Force a 500, verify. | Localized fallback; no crash; no navigation; no tokens; no stack trace shown. | CB-07 | | | | |
| `TC-MOB-OTP-E05` | Offline verify | P0 | Network off, submit a valid code. | A failure is surfaced. The app **must not** proceed to Home and must not imply success. | CB-06.2 | | | | |
| `TC-MOB-OTP-E06` | Offline resend | P1 | Network off, tap resend after the cooldown. | A failure is surfaced and the countdown does **not** restart as though a code had been sent. | CB-06.2 | | | | |
| `TC-MOB-OTP-E07` | Recovery | P1 | Restore the network and retry. | Verifies normally. | CB-06.3 | | | | |
| `TC-MOB-OTP-E08` | Backgrounded across the ticket expiry | P1 | Background the app for more than 5 minutes, return, submit a code. | The expiry is handled cleanly with a message and a route back to sign-in — no infinite spinner and no crash. | — | | | | |
| `TC-MOB-OTP-E09` | Countdown across backgrounding | P1 | Background the app mid-countdown, return after it should have elapsed. | The countdown reflects real elapsed time rather than freezing or resetting. | D-695 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-OTP-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | Chrome mirrors; the six boxes stay **left-to-right**; the back chevron points the correct way. | CB-02 | | | | |
| `TC-MOB-OTP-F02` | No hardcoded string | P0 | Compare every visible string in both languages. | Title, body, CTA, resend label, countdown text and every error are translated. | CB-02.3 | | | | |
| `TC-MOB-OTP-F03` | Accessible names | P1 | Screen reader on; traverse the screen. | Each code box announces its position (for example "digit 3 of 6"); the CTA and resend announce their labels; the countdown state is announced or is at least not misleading. | CB-08.1 | | | | |
| `TC-MOB-OTP-F04` | Errors announced | P1 | Trigger C03 with the screen reader on. | The error is announced when it appears. | CB-08.3 | | | | |
| `TC-MOB-OTP-F05` | Text scaling | P2 | Largest supported font size. | The six boxes still fit on one row without clipping the digits. | CB-08.5 | | | | |

### G. Data integrity and audit

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-OTP-G01` | Second factor issued is audited | P1 | Complete the password step; inspect the audit trail. | A row records that a second factor was issued. | A9-7 | | | | |
| `TC-MOB-OTP-G02` | Successful verification is audited | P1 | Complete C01. | A sign-in success row is written with actor, timestamp and IP. | A9-7 | | | | |
| `TC-MOB-OTP-G03` | Failed attempts are audited | P1 | Complete D01. | Every wrong-code attempt is recorded, including the one that hits the cap. | A9-15, A1-12 | | | | |
| `TC-MOB-OTP-G04` | Resend is audited | P1 | Complete C04. | The resend is recorded. | A9-7 | | | | |
| `TC-MOB-OTP-G05` | Audit content is safe | P0 | Inspect the rows from G01–G04. | No OTP code, ticket or token is stored. | A9-9 | | | | |
| `TC-MOB-OTP-G06` | No secret in the client log | P0 | Capture the device log across a full run. | No code, ticket or token is printed. | A9-9 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-OTP-H01` | Second factor works end to end | P0 | Run C01 → C05. | A 2FA visitor can complete sign-in, and can recover with resend if the first code is lost. | **FR-102**, **FR-104** | | | | |
| `TC-MOB-OTP-H02` | Brute-force protection | P0 | Run D01 → D07. | Attempt cap, ticket expiry, code expiry, hourly cap and the IP limit all hold. | **NFR-01**, A7-8 | | | | |
| `TC-MOB-OTP-H03` | Ticket cannot be abused | P0 | Run D08 → D11. | The ticket is single-purpose, account-bound, and not a bearer token. | **NFR-01**, A1, A7 | | | | |
| `TC-MOB-OTP-H04` | Design parity | P1 | Compare the live render against Figma `758:2616`. | Strings, typography, colour, spacing and radii match. | DoD-Gate-4 | | | | |
| `TC-MOB-OTP-H05` | Live-render gate | P0 | Capture a full screenshot, the console/device log and the network list for a complete run. | Screenshot captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-OTP-H06` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-OTP-H07` | Catalogue alignment | P1 | Cross-check against `E2E-MOB003-003` and `-020`. | Every assertion is covered here and none contradicts the catalogue. | DoD-SES-7 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (61 authored + 9 applicable inherited blocks) | |
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
| No OTP code recorded anywhere in the evidence | | |
| Cleanup register drained | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set** and record the regression
outcome here. A change to the second factor also affects
[sign-in.md](sign-in.md) and the badge-password and biometric-step-up screens
(sheets pending in batch C) — include their P0 sets once authored.

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
| 1.0 | 2026-08-01 | SIMF Team | First issue. Grounded in `otp_code_boxes.dart`, `SignInService.cs`, `ResendOtpEndpoint.cs` and `e2e/mobile-sign-in.md`. |

---

_Authored:_ 2026-08-01 · _Last reviewed:_ 2026-08-01
