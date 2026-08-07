# Test-Case Sheet — `Reset Password` (app screen #3c)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | إعادة تعيين كلمة المرور · Reset Password | **Doc id** | `TC-MOB-RPW` |
| **Route / screen id** | `/auth/reset-password?email=…` (`RouteNames.resetPassword`) — app screen **#3c** | **Surface** | Mobile app (Flutter) |
| **API under test** | `POST /api/v1/app/auth/reset-password` | **Audience** | Guest (anonymous), reached from Forgot Password |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Config check** | `PasswordHistoryCount` = ___ · `PasswordMaxAgeDays` = ___ (both default `0` = disabled → the reuse and expiry rows below are **N-A** unless set) | | |
| **Reference docs** | [pages/mobile/reset-password/](../../pages/mobile/reset-password/README.md) · [e2e/mobile-sign-in.md](../../tests/e2e/mobile-sign-in.md) `E2E-MOB003-008` · [FDS-001](../../SIMF-FDS-001-Authentication-and-Login.md) §5.10 · Sibling sheet: [forgot-password.md](forgot-password.md) · Figma: none (built to match its navy sibling `918:2341`) | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| Code field: **6** digits, digits-only, LTR, numeric keyboard | `src/Mobile/simf_app/lib/features/account/reset_password_screen.dart` |
| Password + confirm fields capped at **128**; live unmet-requirement list; confirm must equal password | same |
| Client password rules: **8–128** with upper, lower, digit, special — **five** checks | `src/Mobile/simf_app/lib/core/validation/password_validation.dart` |
| Server password rules: the same five **plus** no 3-in-a-row repeat, no 3-character sequential run, not the user's own identifier, not a common/leet-speak dictionary password — **nine** checks | `src/Shared/SIMF.Common/PasswordPolicy.cs` |
| **5** verify attempts per code; code lifetime **10 min**; generic `AUTH_RESET_CODE_INVALID` for every failure | `src/Backend/SIMF.Application/IdentityAccess/PasswordService.cs` |
| Rate limits **20 req / 60 s / IP** and **5 req / 60 s / email** | `RateLimitOptions.cs` + `ResetPasswordEndpoint.cs` (chains both) |
| Success path: store `lastEmail` (**not** on web), sign out if signed in, route to sign-in | `reset_password_screen.dart` (D-384, D-659) |

> **The client enforces five of the nine password rules.** A password can pass
> every on-screen check and still be refused by the server. That gap is the
> subject of `TC-MOB-RPW-D06`…`D09` and is expected behaviour, not a defect —
> but the **server's reason must reach the user**, which is what those rows test.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Account with a live reset code | Complete [forgot-password.md](forgot-password.md) `TC-MOB-FPW-C01` for `qa+rpw@example.sa` immediately before this sheet. |
| **FX-2** Expired code | A code requested **more than 10 minutes** earlier. |
| **FX-3** Consumed code | A code already used for a successful reset. |
| **FX-4** Signed-in entry | A signed-in user who reaches this screen from their profile (D-659). |
| Reset codes | Read from `SIMF_Identity.AccountCodes` or the test mailbox at run time. **Never write a code into this sheet or into an evidence file.** |
| API tool | REST client for the §D rows. |
| Passwords used | Use throwaway values only. **Do not record any password used in the run in this sheet** — reference it as "the compliant password" / "the sequential password". |
| Cleanup | Fixtures tagged `QA-`; added to the cleanup register. |

> **No literal secret appears in this document.**

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | partial — loading only | | |
| CB-04 Auth gate and account state | yes — reachable signed out | | |
| CB-05 Session expiry and token refresh | partial — applies to the signed-in entry (FX-4) | | |
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
| `TC-MOB-RPW-A01` | Screen chrome | P1 | Arrive from Forgot Password. | Navy surface; back + centred title header; gold-ringed lock mark; instruction body; three fields in order — **code**, **new password**, **confirm password**; gold CTA pinned at the bottom. Visually consistent with its Forgot Password sibling. | code; sibling `918:2341` | | | | |
| `TC-MOB-RPW-A02` | CTA gating | P1 | Fill the fields one at a time. | The CTA stays **disabled** until code, password **and** confirm all contain text; it disables again if any is cleared. | code `_canSubmit` | | | | |
| `TC-MOB-RPW-A03` | Password visibility toggle | P1 | Tap the eye toggle on the new-password field. | The toggle reveals and re-hides the password, and it applies to **both** the password and the confirm field together. | code `_obscure` (shared) | | | | |
| `TC-MOB-RPW-A04` | Live requirement list | P0 | Type one character into the new-password field. | An inline list of **unmet** requirements appears and updates on every keystroke, disappearing when all five are met. It does **not** appear before the field has been touched. | code `_passwordTouched`, `_passwordUnmet` | | | | |
| `TC-MOB-RPW-A05` | Busy state | P1 | Submit on a throttled connection. | CTA busy; fields disabled; back disabled; no second submit possible. | code `_busy` | | | | |
| `TC-MOB-RPW-A06` | Tablet width | P1 | Open on a tablet in portrait. | Content centred, capped at **560 px**; no dead side gutters. | `MaxWidthBody(maxWidth: 560)` | | | | |
| `TC-MOB-RPW-A07` | Keyboard | P1 | Focus each field on a small phone. | Every field scrolls clear of the keyboard; the CTA stays reachable. | CB-01 | | | | |

### B. Field validation (client)

| ID | Field | Pri | Test data | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RPW-B01` | Code — empty | P0 | _(empty)_ | Inline **required** error; no network request. | validator | | | | |
| `TC-MOB-RPW-B02` | Code — length boundary | P0 | 5 digits · 6 digits · attempt a 7th | 5 digits → error. 6 digits → accepted. The **7th digit cannot be entered** (field capped at 6). | `maxLength: 6` | | | | |
| `TC-MOB-RPW-B03` | Code — non-digits rejected at input | P0 | Type `abc`, `12a4b6`, `12 34 56`, `١٢٣٤٥٦` (Arabic-Indic digits) | Only ASCII digits are accepted into the field; other characters never appear. Record the behaviour for Arabic-Indic digits explicitly — if they are silently dropped a user typing on an Arabic keypad cannot enter their code, which is a defect. | `FilteringTextInputFormatter.digitsOnly` | | | | |
| `TC-MOB-RPW-B04` | Code — wrong-length message quality | P1 | Enter 3 digits and submit. | An error appears. **Known weakness:** the message reused is the generic *required-field* text, not "enter the 6-digit code" — the code comment records this as reported to the owner. Confirm the wording and raise it if a tester finds it misleading. | code comment in `reset_password_screen.dart` | | | | |
| `TC-MOB-RPW-B05` | Code — direction | P1 | Run in Arabic. | The code renders **left-to-right** and digits are not reversed. | `textDirection: TextDirection.ltr` | | | | |
| `TC-MOB-RPW-B06` | Password — empty | P0 | _(empty)_ | Inline **required** error; no network request. | validator | | | | |
| `TC-MOB-RPW-B07` | Password — length boundary | P0 | 7 · 8 · 128 characters · attempt a 129th | 7 → the **length** requirement stays listed and submit is blocked. 8 → length satisfied. 128 → accepted. The **129th character cannot be entered**. | `maxLength: 128`; policy 8–128 | | | | |
| `TC-MOB-RPW-B08` | Password — each rule listed individually | P0 | Enter a value missing **only** the upper-case; then only the lower-case; then only a digit; then only a special character. | In each case exactly the **one** unmet requirement is listed, naming the missing class. The list is not an all-or-nothing message. | `unmetPasswordRequirements` | | | | |
| `TC-MOB-RPW-B09` | Password — submit blocked while unmet | P0 | Fill all three fields but leave one password rule unmet, then tap the CTA. | **No** network request is made. **Check what the user sees:** the code returns silently when a requirement is unmet, so if the requirement list is the only feedback, confirm it is visible on screen at that moment. A tap that appears to do nothing is a defect. | code `if (unmetPasswordRequirements(...).isNotEmpty) return;` | | | | |
| `TC-MOB-RPW-B10` | Confirm — mismatch | P0 | Password and confirm differ by one character. | Inline **"passwords do not match"** error on the confirm field. **No** network request. | validator → `l10n.passwordsDoNotMatch` | | | | |
| `TC-MOB-RPW-B11` | Confirm — match | P0 | Identical values. | No error; submit proceeds. | — | | | | |
| `TC-MOB-RPW-B12` | Confirm — case sensitivity | P1 | Same letters, different case. | Treated as a **mismatch** — the comparison is exact. | validator `value == _password.text` | | | | |
| `TC-MOB-RPW-B13` | Confirm — reacts to a later password edit | P1 | Make both fields match, then edit the **password** field so they differ, then submit. | The mismatch is caught before any request. | — | | | | |
| `TC-MOB-RPW-B14` | Password — leading/trailing space is significant | P1 | Enter a compliant password with a trailing space in the password field and without it in confirm. | Treated as a mismatch — password values are **not** trimmed. Confirm the same value round-trips to a working sign-in in `C04`. | code (no `.trim()` on password) | | | | |
| `TC-MOB-RPW-B15` | Password — submit from the keyboard | P2 | Complete all three fields, press the keyboard submit action on Confirm. | Submits exactly as the CTA does. | `onFieldSubmitted` | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RPW-C01` | Golden path | P0 | 1. Arrive with **FX-1**. 2. Enter the emailed code. 3. Enter a compliant new password twice. 4. Submit. | Exactly **one** `POST /app/auth/reset-password`. On success the app routes to **sign-in**. | `E2E-MOB003-008`, FR-107 | | | | |
| `TC-MOB-RPW-C02` | Email pre-filled afterwards | P1 | Complete C01, observe the sign-in screen. | The email is pre-filled with the just-reset address. | code `StorageKeys.lastEmail` | | | | |
| `TC-MOB-RPW-C03` | Web exception | P1 | Repeat C01 on the **web** build, if in scope. | The email is **not** stored — on web, preferences are shared-browser storage and the address would surface to the next kiosk user (D-384). | D-384 | | | | |
| `TC-MOB-RPW-C04` | New password actually works | P0 | After C01, sign in with the **new** password, then attempt the **old** one. | New password signs in. Old password is refused. | FR-107 | | | | |
| `TC-MOB-RPW-C05` | Signed-in entry signs the user out | P0 | As **FX-4**, reach this screen from the profile and complete a reset. | The local session is signed out so the sign-in screen is a genuine fresh login, not a stale signed-in state. | D-659 | | | | |
| `TC-MOB-RPW-C06` | Back navigation | P1 | Tap back. | Returns to the previous screen; with nothing to pop, lands on **forgot-password**. No request fired. | code `_back()` | | | | |
| `TC-MOB-RPW-C07` | Double submit | P0 | Tap the CTA twice rapidly. | **One** request; the password changes once; the attempt counter increments by 1. | CB-06.5, A4-10 | | | | |
| `TC-MOB-RPW-C08` | Email carried from the previous screen | P0 | Arrive from forgot-password and inspect the request payload. | The request carries the **same trimmed address** that was submitted on the previous screen — the user never retypes it. | code `widget.email` | | | | |

### D. Server-side and NCA security

> **Run every row here against the API directly**, not only through the app.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RPW-D01` | Wrong code | P0 | Submit a valid-format but incorrect 6-digit code. | **400 `AUTH_RESET_CODE_INVALID`**. The password is unchanged. | `PasswordService.cs` | | | | |
| `TC-MOB-RPW-D02` | Expired code | P0 | Use **FX-2** (older than 10 minutes). | Rejected with the same generic `AUTH_RESET_CODE_INVALID`. | `ResetCodeLifetime = 10 min` | | | | |
| `TC-MOB-RPW-D03` | Consumed code cannot be replayed | P0 | Use **FX-3**. | Rejected with the same generic code. A code is single-use. | `PasswordService.cs` | | | | |
| `TC-MOB-RPW-D04` | **Generic failure — no enumeration** | P0 | Submit (a) an unknown email with any code, (b) a known email with no code issued, (c) a known email with a wrong code. | **All three** return **400 `AUTH_RESET_CODE_INVALID`**. Nothing distinguishes "no such user" from "wrong code". Status, body and timing are equivalent. | A7, `PasswordService.cs` | | | | |
| `TC-MOB-RPW-D05` | **Max verify attempts** | P0 | Submit **6** wrong codes for one issued code. | Attempt 6 is refused. The code cannot be brute-forced within its 10-minute life. | `MaxResetAttempts = 5`, A7-8 | | | | |
| `TC-MOB-RPW-D06` | Server rule — **sequential run** | P0 | Submit a password that satisfies all five client rules but contains a 3-character ascending or descending run (letters or digits). | The **client accepts** it (the on-screen list clears) but the **server rejects** it, and the server's reason is shown to the user in their language. A silent or generic failure here is a defect. | `PasswordPolicy.HasSequentialRun`, A7-29 | | | | |
| `TC-MOB-RPW-D07` | Server rule — **repeat run** | P0 | Submit a client-valid password containing three identical characters in a row. | Rejected server-side with a reason the user can act on. Two identical in a row is allowed. | `PasswordPolicy.HasRepeatRun`, A7-29 | | | | |
| `TC-MOB-RPW-D08` | Server rule — **common password** | P0 | Submit a client-valid password that is a well-known password or a leet-speak spelling of one (the policy folds `0→o`, `1→i`, `3→e`, `4→a`, `5→s`, `7→t`, `8→b`, `9→g`, `@→a`, `$→s` and strips the rest before matching a dictionary base word). | Rejected server-side. Leet substitution does **not** defeat the check. | `PasswordPolicy.IsCommon`, A7-28 | | | | |
| `TC-MOB-RPW-D09` | Server rule — **resembles the identifier** | P0 | Submit a password equal to the account's email address, and one equal to its local part (3+ characters). | Both rejected server-side. | `PasswordPolicy.ResemblesIdentifier`, A7-29 | | | | |
| `TC-MOB-RPW-D10` | Server re-validates structure | P0 | Bypass the app and POST a 5-character password, a 200-character password, and one with no digit. | Each is rejected by the **server**. The client checks are not the only gate. | A3, A7-29 | | | | |
| `TC-MOB-RPW-D11` | Server re-validates the confirmation | P0 | POST with `newPassword` and `confirmPassword` **different**. | Rejected server-side — the match is not a client-only rule. | A3 | | | | |
| `TC-MOB-RPW-D12` | Per-**email** rate limit | P0 | POST 6 times for one address within 60 seconds. | The 6th returns **429**. | `auth-email` = 5 / 60 s | | | | |
| `TC-MOB-RPW-D13` | Per-**IP** rate limit | P0 | POST 21 times from one IP within 60 seconds (vary the email). | The 21st returns **429**. | `auth` = 20 / 60 s | | | | |
| `TC-MOB-RPW-D14` | **All sessions revoked on success** | P0 | Sign in on device A. Complete a reset from device B. Then use device A. | Device A is signed out or refused on its next call. Every prior refresh token for the account is revoked. | A7, `E2E-AUTH-008` | | | | |
| `TC-MOB-RPW-D15` | Password history | P1 | Reset to a password the account used previously. | **N-A** while `PasswordHistoryCount = 0`. If the environment sets a non-zero value, the reuse must be refused. Record the configured value in §1. | A7-20 | | | | |
| `TC-MOB-RPW-D16` | Password masked and not autofilled into plain text | P1 | Inspect both password fields. | Both are masked by default; the reveal is an explicit user action; the value is not written to any plain-text store. | A7-2 | | | | |
| `TC-MOB-RPW-D17` | No code, password or token in a URL | P0 | Inspect the navigation URL, deep links and the device log across a run. | None of them appear. (The email appears in the route query — confirm no code or password accompanies it.) | A7-36, A9-9 | | | | |
| `TC-MOB-RPW-D18` | Response leaks nothing | P0 | Inspect the success and failure response bodies. | Neither carries account data, a token, a role, an account state, or a hint about why the code failed. | A7, A9-9 | | | | |
| `TC-MOB-RPW-D19` | Transport | P0 | Capture the request. | TLS only. The password is never transmitted over plain HTTP. | A5 | | | | |
| `TC-MOB-RPW-D20` | Nothing sensitive survives backgrounding | P1 | Type a password, background the app, reopen; check the recents thumbnail. | No password or code is visible in the recents snapshot or retained in unencrypted local storage. | A11 | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RPW-E01` | Invalid-code error surface | P0 | Submit a wrong code through the app. | A localized inline error appears. The app **stays** on the reset screen. The code field is usable for a retry; the user is not thrown back to sign-in. | `E2E-MOB003-019` | | | | |
| `TC-MOB-RPW-E02` | Server rejects the password | P0 | Trigger D06, D07, D08 or D09 through the app. | The **server's** reason is rendered inline in the app's language — specific enough for the user to fix the password. A bare "something went wrong" is a defect. | A9, `E2E-MOB003-019` | | | | |
| `TC-MOB-RPW-E03` | 429 rate limited | P0 | Trip D12, submit through the app. | Localized "too many attempts" inline. No navigation. Fields preserved. | — | | | | |
| `TC-MOB-RPW-E04` | Server 500 | P1 | Force a 500, submit. | Localized fallback inline; no crash; no navigation; no stack trace or internal detail shown. | CB-07.1, CB-07.2 | | | | |
| `TC-MOB-RPW-E05` | Offline submit | P0 | Network off, submit a valid form. | A failure is surfaced. The app **must not** route to sign-in and **must not** imply the password changed. | CB-06.2 | | | | |
| `TC-MOB-RPW-E06` | Recovery | P1 | Restore the network after E05 and submit again. | Succeeds. The password changes exactly once. | CB-06.3 | | | | |
| `TC-MOB-RPW-E07` | Navigate away mid-request | P1 | Submit then immediately background or tap back. | No crash; no error painted on a disposed screen; state stays consistent. | code `if (!mounted) return` | | | | |
| `TC-MOB-RPW-E08` | Deep-link with no email | P1 | Open the route without the `email` query parameter. | The screen handles the missing value gracefully — it does not crash and does not submit an empty address. | code `required this.email` | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RPW-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | Layout mirrors correctly; the code field stays LTR; the back chevron points the right way (watch for the double-mirror fault). | CB-02 | | | | |
| `TC-MOB-RPW-F02` | No hardcoded string | P0 | Compare every visible string in both languages. | Title, body, three field labels, every validation message, all five requirement messages, the mismatch message and the CTA are translated. | CB-02.3 | | | | |
| `TC-MOB-RPW-F03` | Requirement list is readable in Arabic | P1 | Trigger A04 in Arabic. | All five requirement messages render in Arabic, wrap rather than clip, and show no missing glyphs. | CB-02.4 | | | | |
| `TC-MOB-RPW-F04` | Accessible names | P1 | Screen reader on; traverse the screen. | Code, password and confirm each announce their own caption; the reveal toggle announces its state; errors and the requirement list are announced when they change. | CB-08.1 | | | | |
| `TC-MOB-RPW-F05` | Text scaling | P2 | Largest supported font size. | The three fields, the requirement list and the CTA all remain usable and unclipped. | CB-08.5 | | | | |
| `TC-MOB-RPW-F06` | Errors are not colour-only | P1 | Trigger B10. | Failure is conveyed by text, not only by a red border. | CB-08.4 | | | | |

### G. Data integrity and audit

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RPW-G01` | Success is audited | P1 | Complete C01, inspect the audit trail. | A password-reset row records the actor, the timestamp and the source IP. | A9-7 | | | | |
| `TC-MOB-RPW-G02` | Failed attempts are audited | P1 | Complete D01 and D05, inspect the audit trail. | Each failed attempt is recorded — failures are not silently discarded. | A9-15, A1-12 | | | | |
| `TC-MOB-RPW-G03` | Audit content is safe | P0 | Inspect the rows from G01–G02. | No password, code or token is stored in the audit row. | A9-9 | | | | |
| `TC-MOB-RPW-G04` | Password stored only as a hash | P0 | Inspect the account row after C01. | The password is stored as a hash. No plaintext and no reversible form exists anywhere. | A2 | | | | |
| `TC-MOB-RPW-G05` | No secret in the client log | P0 | Capture the device log across a full run. | No password, code or token is printed. | A9-9 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-RPW-H01` | Requirement satisfied | P0 | Run C01 → C05. | *"The system shall provide a password-reset flow by email verification code."* is met end to end, together with [forgot-password.md](forgot-password.md). | **FR-107** | | | | |
| `TC-MOB-RPW-H02` | Password policy enforced | P0 | Run D06 → D11. | All nine server rules hold and the client mirrors five of them for instant feedback. | **NFR-01**, A7-10, A7-28, A7-29 | | | | |
| `TC-MOB-RPW-H03` | Session invalidation on credential change | P0 | Run D14. | A password change ends every existing session. | **NFR-01**, A7 | | | | |
| `TC-MOB-RPW-H04` | Design parity | P1 | Compare the live render against the Forgot Password sibling. | Chrome, spacing, typography and colour are consistent with `918:2341`. This screen has **no dedicated Figma node** — record any divergence for the owner rather than assuming a defect. | DoD-Gate-4 | | | | |
| `TC-MOB-RPW-H05` | Live-render gate | P0 | Capture a full screenshot, the console/device log and the network list for a complete run. | Screenshot captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-RPW-H06` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-RPW-H07` | Catalogue alignment | P1 | Cross-check against `E2E-MOB003-008`. | Every assertion in the E2E scenario is covered here and none contradicts it. | DoD-SES-7 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (63 authored + 9 applicable inherited blocks) | |
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
| No password or code recorded anywhere in the evidence | | |
| Cleanup register drained | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set** and record the regression
outcome here. A failed re-test **reopens the same defect id**.

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
| 1.0 | 2026-08-01 | SIMF Team | First issue. Grounded in `reset_password_screen.dart`, `password_validation.dart`, `PasswordPolicy.cs` and `PasswordService.cs`. |

---

_Authored:_ 2026-08-01 · _Last reviewed:_ 2026-08-01
