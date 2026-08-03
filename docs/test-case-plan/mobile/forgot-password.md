# Test-Case Sheet — `Forgot Password` (app screen #3b)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | نسيت كلمة المرور · Forgot Password | **Doc id** | `TC-MOB-FPW` |
| **Route / screen id** | `/auth/forgot-password` (`RouteNames.forgotPassword`) — app screen **#3b** | **Surface** | Mobile app (Flutter) |
| **API under test** | `POST /api/v1/app/auth/forgot-password` | **Audience** | Guest (anonymous) |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill — include one Huawei / no-GMS handset in the matrix)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Config check** | `PasswordMaxAgeDays` = ___ · `PasswordHistoryCount` = ___ (both default `0` = disabled) | | |
| **Reference docs** | [pages/mobile/forgot-password/](../../pages/mobile/forgot-password/README.md) · [e2e/mobile-sign-in.md](../../tests/e2e/mobile-sign-in.md) `E2E-MOB003-007` · [FDS-001](../../SIMF-FDS-001-Authentication-and-Login.md) §5.10, §9, `T-17` · Figma `918:2341` | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| Email field cap **50**, hint `example@email.com`, validators, always-navigate-on-success | `src/Mobile/simf_app/lib/features/account/forgot_password_screen.dart` |
| Reset code lifetime **10 min** (response returns **600** seconds) | `src/Backend/SIMF.Application/IdentityAccess/PasswordService.cs` — `ResetCodeLifetime` |
| **5** reset codes per account per **1 hour**, silently dropped over the cap | same — `MaxResetCodesPerWindow`, `ResetRequestWindow` |
| **5** verify attempts per code | same — `MaxResetAttempts` |
| Rate limits **20 req / 60 s / IP** (`auth`) and **5 req / 60 s / email** (`auth-email`) | `src/Shared/SIMF.Common/Options/RateLimitOptions.cs` + `src/Backend/SIMF.Api/Program.cs`; endpoint chains both in `ForgotPasswordEndpoint.cs` |
| Account lockout **5 failed sign-ins → 15 min** (sign-in only) | `src/Backend/SIMF.Infrastructure/DependencyInjection.cs` |
| Enumeration-safe: identical response on every branch | `PasswordService.cs` — same `ForgotPasswordResponse` returned for unknown email, disabled/rejected account and over-cap |

> **Rate-limit reference.** `docs/manuals/Developer-Guide.md` §16.4 previously
> stated the `auth` limit was *5 requests / 5 minutes / IP*; it was **corrected
> on 2026-08-03** to **20 / 60 s per IP**, and the `auth-email` partition
> (**5 / 60 s per email**) was documented alongside it. Both figures on this
> sheet come from `RateLimitOptions.cs`.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Existing approved account | `qa+fpw-exists@example.sa` — registered, email verified, approved |
| **FX-2** Non-existent email | `qa+fpw-nobody@example.sa` — guaranteed **not** in `SIMF_Identity` |
| **FX-3** Disabled account | an account in `AccountState = Disabled` |
| **FX-4** Rejected account | an account in `AccountState = Rejected` |
| **FX-5** Long address | a valid address **longer than 50 characters** (for `TC-MOB-FPW-B07`) |
| Reset codes | Read from `SIMF_Identity.AccountCodes` at run time, or from the test mailbox. **Never write a code into this sheet or into an evidence file.** |
| API tool | A REST client (curl / Postman) for the §D rows — those are run against the API, not only through the app. |
| Rate-limit reset | Rate-limit windows are 60 s; the per-account cap window is **1 hour**. Plan §D-03/04/05 so they do not poison each other — run them last, or on separate fixture emails. |
| Cleanup | Fixtures are tagged `QA-` and added to the cleanup register. |

> **No literal secret appears in this document.**

## 3. Inherited common cases

Run from [`_COMMON-CASES.md`](../_COMMON-CASES.md).

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | partial — no list; loading only | | |
| CB-04 Auth gate and account state | yes — must be reachable **signed out** | | |
| CB-05 Session expiry and token refresh | **N-A** — anonymous screen | | |
| CB-06 Network failure and retry | yes | | |
| CB-07 Server 500 and malformed payload | yes | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A** — form, no server list | | |
| CB-10 Audit trail | yes | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-FPW-A01` | Screen chrome | P1 | Open sign-in → tap "نسيت كلمة المرور؟ / Forgot password". | Navy surface; header = back chevron + centred title; gold-ringed **lock** mark; instruction body; single email field with a mail glyph; gold CTA pinned at the bottom; foot row "remembered? **sign in**". | Figma `918:2341` | | | | |
| `TC-MOB-FPW-A02` | CTA disabled state | P1 | Open the screen without typing. | The send CTA is **disabled**. Typing any character enables it; clearing the field disables it again. | code `_canSubmit` | | | | |
| `TC-MOB-FPW-A03` | Busy state | P1 | Submit a valid email on a throttled connection. | While the request is in flight the CTA shows its busy state, the field is disabled, and the back control is disabled. No second request can be fired. | code `_busy` | | | | |
| `TC-MOB-FPW-A04` | Pre-filled email | P1 | Open the screen from a signed-in user's profile. | The email field is pre-filled with the account's address (D-659); the user does not retype it. | D-659 | | | | |
| `TC-MOB-FPW-A05` | Tablet width | P1 | Open on a tablet in portrait. | Content is centred and capped at **560 px**; no dead side gutters and no edge-to-edge stretch. | `MaxWidthBody(maxWidth: 560)` | | | | |
| `TC-MOB-FPW-A06` | Keyboard | P1 | Focus the email field on a small phone. | The field stays visible above the keyboard; the CTA remains reachable; nothing is clipped. | CB-01 | | | | |

### B. Field validation (client)

| ID | Field | Pri | Test data | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-FPW-B01` | Email — empty | P0 | _(leave empty, force submit via the keyboard action)_ | Inline **required** error under the field. **No** network request is made. | `_validateEmail` → `l10n.requiredField` | | | | |
| `TC-MOB-FPW-B02` | Email — whitespace only | P0 | `"   "` | Treated as empty → **required** error. No network request. | `isBlank` | | | | |
| `TC-MOB-FPW-B03` | Email — malformed | P0 | `admin@` · `@example.sa` · `admin example.sa` · `admin@@example.sa` · `admin@example` | Inline **invalid email** error for each. **No** network request fires for any of them. | `isValidEmail` → `l10n.invalidEmail`; `E2E-MOB003-018` | | | | |
| `TC-MOB-FPW-B04` | Email — valid | P0 | `qa+fpw-exists@example.sa` | No inline error; submit proceeds. | — | | | | |
| `TC-MOB-FPW-B05` | Email — surrounding spaces trimmed | P1 | `"  qa+fpw-exists@example.sa  "` | Accepted. The value sent to the server and carried to the reset screen is **trimmed** — no leading or trailing space. | code `_email.text.trim()` | | | | |
| `TC-MOB-FPW-B06` | Email — length boundary | P1 | Addresses of **49 / 50 / 51** characters | 49 and 50 type fully. The **51st character cannot be entered** — the field is capped at 50. | `maxLength: 50` | | | | |
| `TC-MOB-FPW-B07` | Email — client cap vs server cap | P1 | **FX-5**, a valid address longer than 50 characters | The client stops at 50, so a legitimate address longer than 50 characters **cannot be entered at all**, although the server accepts up to 256. Record as a defect if any real user address is affected. | client `maxLength: 50` vs server 256; `E2E-MOB003-006` | | | | |
| `TC-MOB-FPW-B08` | Email — case | P2 | `QA+FPW-EXISTS@EXAMPLE.SA` | Accepted; the flow behaves identically to the lower-case form. | — | | | | |
| `TC-MOB-FPW-B09` | Email — direction in Arabic | P1 | Switch to Arabic, type an address | The address renders **left-to-right** and reads correctly inside the RTL layout — no reversed or scrambled characters. | `E2E-MOB003-011` | | | | |
| `TC-MOB-FPW-B10` | Email — paste | P2 | Paste an address from the clipboard | Pasting works; validation runs on the pasted value; the CTA enables. | — | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-FPW-C01` | Golden path | P0 | 1. Enter **FX-1**. 2. Tap the send CTA. | Exactly **one** `POST /app/auth/forgot-password`. The app navigates to the **reset-password** screen with the email carried forward in the query. A reset code arrives at FX-1's mailbox. | `E2E-MOB003-007`, FR-107 | | | | |
| `TC-MOB-FPW-C02` | Unknown email behaves identically | P0 | Repeat C01 with **FX-2**. | **Byte-identical** outcome from the user's point of view: same response shape, same navigation to the reset screen, same on-screen text, no error. No email is sent. | `PasswordService.cs`; `T-17` | | | | |
| `TC-MOB-FPW-C03` | Submit from the keyboard | P1 | Enter a valid email, press the keyboard's submit/done action. | Submits exactly as the CTA does — one request. | `onFieldSubmitted` | | | | |
| `TC-MOB-FPW-C04` | Back navigation | P1 | Tap the back chevron. | Returns to the previous screen; if there is nothing to pop, lands on **sign-in**. No request is fired. | code `_back()` | | | | |
| `TC-MOB-FPW-C05` | "Remembered? Sign in" link | P1 | Tap the foot link. | Navigates to the sign-in screen. Disabled while a request is in flight. | code | | | | |
| `TC-MOB-FPW-C06` | Double submit | P0 | Tap the CTA twice in rapid succession. | **One** request only — the control disables while busy. The account's hourly code count increases by **1**, not 2. | CB-06.5, A4-10 | | | | |
| `TC-MOB-FPW-C07` | Email carried into the reset screen | P0 | Complete C01, then inspect the reset screen. | The reset screen shows / uses the **same** address that was submitted, trimmed. The user does not retype it. | code `queryParameters: {'email': …}` | | | | |
| `TC-MOB-FPW-C08` | Code actually works end to end | P0 | Complete C01, read the emailed code, complete the reset on the next screen, then sign in with the new password. | Sign-in succeeds with the new password. The **old** password no longer works. | FR-107, `E2E-MOB003-008` | | | | |

### D. Server-side and NCA security

> **Run every row here against the API directly** (curl / Postman), not only
> through the app. A rule enforced only in the client is a defect, not a pass.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-FPW-D01` | **Account enumeration** | P0 | POST the endpoint twice — once with **FX-1**, once with **FX-2**. Compare status code, body, headers. | **Identical** HTTP status and **identical** response body. Nothing in the response, the headers or the app's behaviour reveals whether the account exists. | A7 (enumeration), FDS-001 §5.10, `T-17` | | | | |
| `TC-MOB-FPW-D02` | Enumeration — disabled / rejected account | P0 | POST with **FX-3** and **FX-4**. | Same success-shaped response as FX-1 and FX-2. A disabled or rejected state is not disclosed. | `PasswordService.cs` | | | | |
| `TC-MOB-FPW-D03` | Enumeration — response timing | P1 | Time 10 requests for FX-1 and 10 for FX-2. | No consistent, exploitable timing difference between the existing and non-existing address. Record both medians. | A7 | | | | |
| `TC-MOB-FPW-D04` | Reset-code lifetime | P0 | Request a code, wait **more than 10 minutes**, then attempt the reset with it. | The code is rejected. The response advertises the lifetime as **600 seconds**. | `ResetCodeLifetime = 10 min` | | | | |
| `TC-MOB-FPW-D05` | **Max codes per account** | P0 | Send **6** forgot-password requests for FX-1 within one hour (space them to avoid tripping D06/D07 first). | The first **5** issue a code. The **6th is silently dropped** — the response is unchanged (still success-shaped) and **no 6th email arrives**. An audit row is written with `ForgotPasswordRequested / Failure / RateLimitExceeded`. | `MaxResetCodesPerWindow = 5`, `ResetRequestWindow = 1 h`; A7-8 | | | | |
| `TC-MOB-FPW-D06` | Per-**email** rate limit | P0 | Send **6** requests for the same address within **60 seconds** from any IPs. | The 6th returns **429** with the standard rate-limit error code. | `auth-email` = 5 / 60 s | | | | |
| `TC-MOB-FPW-D07` | Per-**IP** rate limit | P0 | Send **21** requests from one IP within **60 seconds** (vary the email so D06 does not trip first). | The 21st returns **429**. | `auth` = 20 / 60 s | | | | |
| `TC-MOB-FPW-D08` | Forgot-password does **not** lock the account | P0 | Trip D05 for FX-1, then sign in with FX-1's **correct** password. | Sign-in **succeeds**. Requesting reset codes never locks an account — lockout is 5 failed **sign-ins** → 15 minutes and applies to sign-in only. A denial-of-service against another user's account by spamming reset requests must not be possible. | `DependencyInjection.cs`; A7-8 | | | | |
| `TC-MOB-FPW-D09` | Max verify attempts per code | P0 | Request a code, then submit **6** wrong codes on the reset screen. | Attempt 6 is refused. The code cannot be brute-forced. | `MaxResetAttempts = 5` | | | | |
| `TC-MOB-FPW-D10` | Server re-validates the email | P0 | POST the endpoint directly with `""`, `"admin@"`, a 300-character string, and a non-string JSON type. | Each is rejected by the **server** with the standard validation envelope. The client-side check is not the only gate. | A3 | | | | |
| `TC-MOB-FPW-D11` | Response leaks no account data | P0 | Inspect the full success response body and headers for FX-1. | The body carries only the code lifetime. **No** name, account state, role, id, masked email or "user exists" flag. | A7, A9-9 | | | | |
| `TC-MOB-FPW-D12` | No code or token in a URL | P0 | Inspect the app's navigation URL after C01, plus device logs and any deep link. | The reset **code** never appears in a URL, a log line or a deep link. (The email address is carried in the query — confirm no code accompanies it.) | A7-36 | | | | |
| `TC-MOB-FPW-D13` | Used code cannot be replayed | P0 | Complete a reset with a code, then attempt the same code again. | Rejected with the generic `AUTH_RESET_CODE_INVALID`. | `PasswordService.cs` | | | | |
| `TC-MOB-FPW-D14` | Generic failure code on reset | P0 | Attempt a reset with (a) an unknown email, (b) a valid email but no code issued, (c) a wrong code. | **All three** return **400 `AUTH_RESET_CODE_INVALID`** — the same generic code. Nothing distinguishes "no such user" from "wrong code". | `PasswordService.cs` | | | | |
| `TC-MOB-FPW-D15` | Successful reset revokes sessions | P0 | Sign in on device A, complete a password reset from device B, then use device A. | Device A's session is invalid — it is signed out or refused on its next call. Prior refresh tokens are revoked. | A7; `E2E-AUTH-008` | | | | |
| `TC-MOB-FPW-D16` | Transport | P0 | Capture the request. | Sent over TLS only. The email address is never sent over plain HTTP. | A5 | | | | |
| `TC-MOB-FPW-D17` | No credential caching | P1 | Background the app after submitting, then reopen. | No reset code and no password is retained in a screenshot, in the recents thumbnail, or in unencrypted local storage. | A11 | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-FPW-E01` | 429 rate limited | P0 | Trip D06, then submit through the app. | An inline error appears **in the app's current language**, taken from the server envelope. The app **stays** on the forgot-password screen and does **not** navigate to reset. The email stays in the field. | `E2E-MOB003-019` | | | | |
| `TC-MOB-FPW-E02` | Server 500 | P1 | Force a 500 on the endpoint, submit. | Localized fallback error inline; no crash; no navigation; email preserved; no stack trace or internal detail shown. | CB-07.1, CB-07.2 | | | | |
| `TC-MOB-FPW-E03` | Offline submit | P0 | Turn the network off, submit a valid email. | A failure is surfaced. The app **must not** navigate to the reset screen and **must not** imply a code was sent. | CB-06.2 | | | | |
| `TC-MOB-FPW-E04` | Recovery after failure | P1 | After E03, restore the network and submit again. | Succeeds and navigates normally. Exactly one code is issued. | CB-06.3 | | | | |
| `TC-MOB-FPW-E05` | Slow network | P2 | Throttle to a very slow connection, submit. | Busy state holds; no duplicate request; no timeout into a blank screen. | CB-06.4 | | | | |
| `TC-MOB-FPW-E06` | Navigate away mid-request | P1 | Submit, then immediately background the app or tap back. | No crash. No stale error painted on a disposed screen. Returning to the app leaves a consistent state. | code `if (!mounted) return` | | | | |
| `TC-MOB-FPW-E07` | Malformed server response | P2 | Return a success envelope with a missing field. | The screen degrades gracefully rather than crashing. | CB-07.3 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-FPW-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | Layout mirrors: header, body, field, CTA and foot row all read RTL. The back chevron points the correct way (check for the double-mirror fault). | `E2E-MOB003-011`, CB-02 | | | | |
| `TC-MOB-FPW-F02` | No hardcoded string | P0 | Compare every visible string in both languages. | Title, body, field label, hint, both validation errors, CTA label, foot text and the sign-in link are all translated. No English word inside the Arabic run. | CB-02.3 | | | | |
| `TC-MOB-FPW-F03` | Server error language | P0 | Trigger E01 in Arabic, then in English. | The server's message renders in the app's current language — the envelope carries both, and the data layer picks by locale. | `E2E-MOB003-019` | | | | |
| `TC-MOB-FPW-F04` | Accessible names | P1 | Enable the screen reader; traverse the screen. | The email box announces its own caption (not "edit box"); the CTA and the sign-in link announce their labels; validation errors are announced when they appear. | `E2E-MOB003-021`, CB-08.1 | | | | |
| `TC-MOB-FPW-F05` | Text scaling | P2 | Set the largest supported font size. | Nothing is clipped or overlapping; the CTA stays reachable; the body text wraps. | CB-08.5 | | | | |
| `TC-MOB-FPW-F06` | Error is not colour-only | P1 | Trigger B01. | The failure is conveyed by **text**, not only by a red border. | CB-08.4 | | | | |

### G. Data integrity and audit

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-FPW-G01` | Success is audited | P1 | Complete C01, inspect the audit trail. | A row records `ForgotPasswordRequested` with outcome **Success**, the actor, the timestamp and the source IP. | A9-7 | | | | |
| `TC-MOB-FPW-G02` | Unknown account is audited | P1 | Complete C02, inspect the audit trail. | A row records the attempt with outcome **Failure** / `AuthAccountNotFound` — the request is not silently discarded. | A9-15, A1-12 | | | | |
| `TC-MOB-FPW-G03` | Over-cap is audited | P1 | Complete D05, inspect the audit trail. | A row records **Failure** / `RateLimitExceeded`. This is the **only** externally invisible signal, and it must exist. | A9-15 | | | | |
| `TC-MOB-FPW-G04` | Audit content is safe | P0 | Inspect the rows written by G01–G03. | No reset code, password, token or other secret is stored in the audit row. | A9-9 | | | | |
| `TC-MOB-FPW-G05` | Audit is not user-editable | P0 | Attempt to alter or delete an audit row through any app or API path. | Not possible. | A8, CB-10.4 | | | | |
| `TC-MOB-FPW-G06` | No secret in the client log | P0 | Capture the device log across a full run. | No reset code, password or bearer token is printed. | A9-9 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-FPW-H01` | Requirement satisfied | P0 | Run C01 → C08. | *"The system shall provide a password-reset flow by email verification code."* is demonstrably met end to end. | **FR-107** | | | | |
| `TC-MOB-FPW-H02` | Security requirement satisfied | P0 | Run all of §D. | The screen meets the NCA Secure Application Development Standard for this surface: enumeration-safe, rate-limited, attempt-capped, expiring codes, audited. | **NFR-01**, A7-8, A7, A9 | | | | |
| `TC-MOB-FPW-H03` | Design parity | P1 | Compare the live render against Figma `918:2341`. | Strings, typography, colour, spacing and radii match the node. Record any deliberate deviation. | DoD-Gate-4 | | | | |
| `TC-MOB-FPW-H04` | Live-render gate | P0 | Capture a full screenshot, the console/device log and the network list for a complete run. | Screenshot captured; **zero** console errors; **zero** failed or broken assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-FPW-H05` | Both languages completed | P0 | Confirm the sheet has been run twice. | Arabic (RTL) and English (LTR) runs are both recorded. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-FPW-H06` | Catalogue alignment | P1 | Cross-check against `E2E-MOB003-007`. | Every assertion in the E2E scenario is covered by a row on this sheet, and none contradicts it. | DoD-SES-7 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (52 authored + 8 applicable inherited blocks) | |
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
| Rate-limit fixtures cleaned; cleanup register drained | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set** — not only the failed case —
and record the regression outcome here. A failed re-test **reopens the same
defect id**; never raise a second id for the same fault.

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
| 1.0 | 2026-08-01 | SIMF Team | First issue. Grounded in `forgot_password_screen.dart`, `PasswordService.cs`, `RateLimitOptions.cs` and `DependencyInjection.cs`. |

---

_Authored:_ 2026-08-01 · _Last reviewed:_ 2026-08-01
