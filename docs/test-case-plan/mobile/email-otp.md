# Test-Case Sheet — `Email Verification` — sign-up step 2 (app screen #6)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | تأكيد البريد الإلكتروني · Email Verification | **Doc id** | `TC-MOB-EOT` |
| **Route / screen id** | `/sign-up/otp?email=…` (`RouteNames.emailOtp`) — app screen **#6** | **Surface** | Mobile app (Flutter) |
| **APIs under test** | `POST /app/auth/verify-email` — `{ email, code }` → `{ email, emailVerified }` · `POST /app/auth/resend-code` — `{ email }` → `{ email, codeExpiresInSeconds }` | **Audience** | Anonymous — mid sign-up, **no token exists yet** |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/sign-up-email-verify/](../../pages/mobile/sign-up-email-verify/README.md) · [e2e/mobile-email-otp.md](../../tests/e2e/mobile-email-otp.md) `E2E-MOB006-001…008` · [FDS-001](../../SIMF-FDS-001-Authentication-and-Login.md) `T-04…T-06` · Figma `505:837` · D-364 / D-553 / D-695 / D-742 | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| **Six segmented code boxes** over one invisible capture field; gold-ringed mail mark; `mm:ss` countdown | D-364, Figma `505:837` |
| Verify is disabled until **exactly six** digits are entered | `E2E-MOB006-001`, `-006` |
| **2-minute countdown starts on entry** and gates the first resend — a fixed **client** cooldown, deliberately **not** the `codeExpiresInSeconds` the endpoint returns | D-695, `E2E-MOB006-004` |
| Verification code lifetime **10 minutes** (`codeExpiresInSeconds` = 600) | [FDS-001](../../SIMF-FDS-001-Authentication-and-Login.md) §5 |
| Wrong code → **400 `AUTH_CODE_INVALID`**, one attempt consumed, field cleared, screen kept | `E2E-MOB006-002` |
| Expired code → **400 `AUTH_CODE_EXPIRED`**; attempt cap → **400 `AUTH_CODE_INVALID`** | `E2E-MOB006-003` |
| Resend cap → **429 `RATE_LIMIT_EXCEEDED`**, wire-identical to the per-IP limiter | `E2E-MOB006-005` (Page_006 L-5 / API E2) |
| Success: account moves `Registered` → `EmailVerified`, toast, then routes to **sign-in** | `E2E-MOB006-001` |
| Verify writes `lastEmail`, so sign-in pre-fills the **corrected** address | D-742, `E2E-MOB003-018` |
| Both endpoints are `AllowAnonymous` on the `auth` limiter (**20 req / 60 s / IP**) | `RateLimitOptions.cs`; catalogue header |

> **Identity here is asserted by email plus the emailed code — nothing else.**
> There is no bearer token and no ticket binding the request to the person who
> started sign-up. That makes §D the most important section on this sheet.

> ### ⚠ CONFIRMED OPEN DEFECT — account enumeration on both endpoints
>
> Verified in `RegistrationService.cs` on 2026-08-03. Both `verify-email` and
> `resend-code` return **three distinguishable responses**:
>
> | Address state | Response |
> |---|---|
> | Not registered | **404 `AUTH_ACCOUNT_NOT_FOUND`** — *"No account was found for this email address."* |
> | Registered, already verified | **400 `AUTH_CODE_INVALID`** — *"This account's email address is already verified."* |
> | Registered, unverified | **200** with the code lifetime |
>
> Both endpoints are `AllowAnonymous`. An unauthenticated caller can therefore
> determine, for any address, whether it has an account and whether that account
> is verified — which **defeats the enumeration resistance deliberately built
> into sign-up** (D-198: always a generic 201, never a 409).
>
> `TC-MOB-EOT-D03` and `D04` state the **required** behaviour. Until the fix
> lands they are **expected to FAIL** — record them as FAIL against this known
> defect rather than re-raising them as new findings. `SIMF-FDS-001` §5.2 and
> §5.3 currently document the 404, so **the spec must change with the code.**

> **Routing note.** Verifying does **not** sign the user in. The flow returns to
> **sign-in**, because the profile step needs a token. A build that jumps
> straight to the profile screen has changed the contract.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Freshly signed-up, unverified | created by [sign-up-form.md](sign-up-form.md) `TC-MOB-SUF-C01` immediately before this sheet |
| **FX-2** Expired code | a code issued more than 10 minutes earlier |
| **FX-3** Already-verified account | verified once already |
| **FX-4** Address that was never signed up | for the resend enumeration row |
| **FX-5** Second unverified account | for the cross-account binding row |
| Codes | Read from `SIMF_Identity.AccountCodes` (`Purpose = EmailVerification`, latest unconsumed) or the test mailbox **at run time**. **Never write a code into this sheet or into an evidence file.** |
| API tool | REST client for §D. |
| Timing | The 2-minute cooldown and the 10-minute expiry make this sheet slow. Allow roughly 45 minutes per language run. |
| Cleanup | Every account created is tagged `QA-` and added to the cleanup register. |

> **No literal secret appears in this document.**

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | partial — loading only | | |
| CB-04 Auth gate and account state | yes — anonymous surface, account state changes here | | |
| CB-05 Session expiry and token refresh | **N-A** — no session exists | | |
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
| `TC-MOB-EOT-A01` | Screen chrome | P1 | Arrive from the sign-up form. | Navy chrome; gold-ringed **mail** mark; title and subtitle; the **echoed email address**; **six** segmented code boxes; the gold `mm:ss` countdown with its muted label; the verify CTA; the "didn't get the code? resend" footer. | Figma `505:837`, D-364 | | | | |
| `TC-MOB-EOT-A02` | Echoed address is the one submitted | P0 | Sign up with a padded, mixed-case address, then read this screen. | The echoed address is the **trimmed, lower-cased** value — the same one that will be verified. A mismatch here means the user verifies a different address than they think. | `E2E-MOB005-001` | | | | |
| `TC-MOB-EOT-A03` | **Countdown starts on entry** | P0 | Arrive on the screen and watch the countdown. | A **2-minute** countdown is showing **immediately on arrival** — not after the first resend — and the resend action is disabled until it reaches `00:00`. | D-695, `E2E-MOB006-004` | | | | |
| `TC-MOB-EOT-A04` | Countdown is the client cooldown, not the code lifetime | P1 | Compare the countdown to the `codeExpiresInSeconds` value in the response. | The countdown is a fixed **2 minutes**; the response advertises **600 seconds**. These are deliberately different — the countdown gates resend, not expiry. | D-695 | | | | |
| `TC-MOB-EOT-A05` | Verify gating | P0 | Enter 0, then 1–5, then 6 digits. | The verify CTA stays **disabled** until **exactly six** digits are present. | `E2E-MOB006-001` | | | | |
| `TC-MOB-EOT-A06` | Box focus advance | P1 | Type six digits in sequence, then backspace. | Focus advances box to box; backspace moves back and clears. The invisible capture field is not visible or focusable as a stray control. | D-364 | | | | |
| `TC-MOB-EOT-A07` | Busy state | P1 | Verify on a throttled connection. | CTA busy; boxes disabled; no second submit possible. | code | | | | |
| `TC-MOB-EOT-A08` | Tablet width | P1 | Open on a tablet in portrait. | The six boxes stay proportionate and centred — not stretched into strips; no dead side gutters. | responsive rule §13.7 | | | | |
| `TC-MOB-EOT-A09` | Keyboard | P1 | Focus a box on a small phone. | The numeric keypad opens; the boxes, countdown and CTA stay visible above it. | CB-01 | | | | |

### B. Field validation (client)

| ID | Field | Pri | Test data | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-EOT-B01` | Code — empty | P0 | _(empty)_ | Verify is disabled; no request can be made. | `E2E-MOB006-006` | | | | |
| `TC-MOB-EOT-B02` | Code — partial | P0 | 1–5 digits | Verify stays disabled; **no** request fires. | `E2E-MOB006-006` | | | | |
| `TC-MOB-EOT-B03` | Code — non-digits rejected at input | P0 | `abcdef` · `1a2b3c` · spaces · punctuation | Non-digits never appear in the boxes. | `E2E-MOB006-006` | | | | |
| `TC-MOB-EOT-B04` | Code — Arabic-Indic digits | P1 | `١٢٣٤٥٦` on an Arabic keypad | Record the behaviour precisely. If Arabic-Indic digits are silently dropped, an Arabic-keypad user cannot enter their code at all — raise it as a defect. | — | | | | |
| `TC-MOB-EOT-B05` | Code — paste | P1 | Paste a 6-digit code from the clipboard or an OS code suggestion. | The paste distributes across the six boxes correctly rather than landing entirely in the first. | D-364 | | | | |
| `TC-MOB-EOT-B06` | Code — overflow | P1 | Attempt a 7th digit | Not accepted; the field is capped at six. | `maxLength: 6` | | | | |
| `TC-MOB-EOT-B07` | Code — direction | P1 | Run in Arabic. | The boxes read **left-to-right** so the code is entered in the order it was emailed; digits are not reversed. | `E2E-MOB006-008` | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-EOT-C01` | Golden path | P0 | Enter the emailed code as **FX-1** and verify. | One `POST /app/auth/verify-email`; the account moves **`Registered` → `EmailVerified`**; the "email verified" toast appears; the app routes to **sign-in**. | `E2E-MOB006-001`, **FR-102**, `T-04` | | | | |
| `TC-MOB-EOT-C02` | Verification does **not** sign the user in | P0 | Complete C01, then inspect the auth state. | **No** tokens are issued. The user lands on sign-in and must authenticate — the profile step needs a token. | `E2E-MOB006-001` | | | | |
| `TC-MOB-EOT-C03` | Sign-in pre-fills the verified address | P1 | Complete C01, then look at the sign-in screen. | The email field is pre-filled with the **just-verified** address — including when the user corrected a typo during sign-up. | D-742, `E2E-MOB003-018` | | | | |
| `TC-MOB-EOT-C04` | Wrong code | P0 | Enter a wrong 6-digit code and verify. | **400 `AUTH_CODE_INVALID`**; a **bilingual inline** error; the field is **cleared** for re-entry; the screen is kept; **one attempt is consumed**. | `E2E-MOB006-002` | | | | |
| `TC-MOB-EOT-C05` | Resend after the countdown | P0 | Wait for `00:00`, tap resend. | `POST /app/auth/resend-code` fires; the **previous code is invalidated**; a fresh code is emailed; a fresh **2-minute** cooldown restarts. | `E2E-MOB006-004`, `T-06` | | | | |
| `TC-MOB-EOT-C06` | Old code dies, new code works | P0 | After C05, try the **old** code, then the **new** one. | The old code is refused; the new one verifies. | `T-06` | | | | |
| `TC-MOB-EOT-C07` | Resend before the countdown | P0 | Tap resend while the countdown is running. | The action is disabled — **no** request is made. | D-695 | | | | |
| `TC-MOB-EOT-C08` | Retrying the same wrong code cannot succeed | P0 | Enter the same wrong code repeatedly. | It never succeeds, and the user is steered to **resend** rather than left retrying. | `E2E-MOB006-003` (Page_006 L-7) | | | | |
| `TC-MOB-EOT-C09` | Back navigation | P1 | Tap back. | Leaves the screen cleanly. The account remains unverified and can be verified later by returning through sign-up or sign-in. Record the exact recovery path a real user has. | — | | | | |
| `TC-MOB-EOT-C10` | Double submit | P0 | Tap verify twice rapidly. | **One** request; **one** attempt consumed, not two. | CB-06.5 | | | | |
| `TC-MOB-EOT-C11` | Double resend | P0 | Tap resend twice rapidly at `00:00`. | **One** request and **one** email. | CB-06.5 | | | | |
| `TC-MOB-EOT-C12` | Already-verified account | P1 | Verify **FX-3** again with a fresh code. | Handled gracefully — no crash and no misleading state. Record the exact behaviour and whether it reveals that the address is already verified (see `TC-MOB-EOT-D03`). | — | | | | |

### D. Server-side and NCA security

> **Run every row here against the API directly**, not only through the app.
> Both endpoints are **anonymous** and identity rests on email plus code alone.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-EOT-D01` | **Attempt cap** | P0 | Submit wrong codes repeatedly against one issued code. | The attempts are capped and further tries return **400 `AUTH_CODE_INVALID`**. A 6-digit code cannot be brute-forced. Record the observed cap. | `E2E-MOB006-003`, A7-8 | | | | |
| `TC-MOB-EOT-D02` | Attempt cap is not reset by resend | P0 | Burn most of the attempts, resend a fresh code, then submit wrong codes again. | Confirm whether resending resets the attempt counter. If it does, the cap is defeatable by repeated resend — record the observed behaviour and raise it. | A7-8 | | | | |
| `TC-MOB-EOT-D03` | **Resend enumeration** | P0 | POST `resend-code` for **FX-1** (real, unverified), **FX-3** (real, already verified) and **FX-4** (never registered). Compare status, body, headers and timing. | The three responses must be **indistinguishable**. `resend-code` must not become an address-existence oracle — sign-up itself is deliberately enumeration-safe (D-198), and this endpoint must not undo that. | A7, D-198 | | | | |
| `TC-MOB-EOT-D04` | **Verify enumeration** | P0 | POST `verify-email` with a valid-format code for **FX-4** (never registered) and for **FX-1** (real, wrong code). | Both return the **same** generic failure. Nothing distinguishes "no such account" from "wrong code". | A7 | | | | |
| `TC-MOB-EOT-D05` | **Code is bound to its account** | P0 | Issue codes for **FX-1** and **FX-5**. Submit FX-5's valid code against FX-1's address, and the reverse. | Refused in both directions. A code valid for one account must never verify another. | A1, A7 | | | | |
| `TC-MOB-EOT-D06` | Code expiry | P0 | Use **FX-2**, older than **10 minutes**. | **400 `AUTH_CODE_EXPIRED`**. | `E2E-MOB006-003`, FDS-001 §5 | | | | |
| `TC-MOB-EOT-D07` | Code is single-use | P0 | Verify successfully, then replay the same code. | Refused. | A7 | | | | |
| `TC-MOB-EOT-D08` | Superseded code is dead | P0 | Resend, then submit the **previous** code. | Refused. Only the latest issued code is live. | `T-06` | | | | |
| `TC-MOB-EOT-D09` | **Resend cap** | P0 | Resend repeatedly for one address until the cap trips. | **429 `RATE_LIMIT_EXCEEDED`** with the bilingual cap message; the resend action stays disabled. Record the observed cap. | `E2E-MOB006-005` | | | | |
| `TC-MOB-EOT-D10` | Cap is wire-indistinguishable from the IP limiter | P1 | Trip the account cap, then the per-IP limiter. | Both produce the **same 429 / `RATE_LIMIT_EXCEEDED`** signature, so a client cannot tell them apart. This is deliberate. | `E2E-MOB006-005` (Page_006 L-5 / API E2) | | | | |
| `TC-MOB-EOT-D11` | Per-**IP** rate limit | P0 | Send **21** requests to either endpoint from one IP within 60 seconds. | The 21st returns **429**. | `auth` = 20 / 60 s | | | | |
| `TC-MOB-EOT-D12` | **Mailbox flooding** | P0 | Attempt to drive repeated resends at a third party's address from several IPs. | The account-scoped cap holds regardless of source IP, so an attacker who knows an address cannot flood that mailbox. | A4-10, A7-8 | | | | |
| `TC-MOB-EOT-D13` | Server re-validates the code shape | P0 | POST `""`, `"12345"`, `"1234567"`, `"abcdef"`, a null and a non-string JSON type. | Each rejected by the **server**. | A3 | | | | |
| `TC-MOB-EOT-D14` | Server re-validates the email | P0 | POST `verify-email` and `resend-code` with `""`, `admin@`, a 300-character address and a non-string type. | Each rejected by the **server**. | A3 | | | | |
| `TC-MOB-EOT-D15` | Over-posting is refused | P0 | POST `verify-email` with extra fields — `accountState`, `isApproved`, `emailVerified: true`, `role`. | Ignored. The account is **not** verified, approved or elevated by a crafted field. | A4 | | | | |
| `TC-MOB-EOT-D16` | Verification does not grant approval | P0 | Complete C01, then attempt to reach app content. | The account is `EmailVerified` but still **not approved**. Verifying an email must not bypass the approval gate. | `T-09`, CB-04.4 | | | | |
| `TC-MOB-EOT-D17` | Response leaks nothing | P0 | Inspect the success and failure bodies of both endpoints. | Only the address and the verified flag / code lifetime. No account id, state, role, name or existence hint. | A7, A9-9 | | | | |
| `TC-MOB-EOT-D18` | No code in a URL or log | P0 | Inspect navigation URLs, deep links and the device log across a run. | The **code** never appears. The email in the route query is expected — confirm nothing else accompanies it. | A7-36, A9-9 | | | | |
| `TC-MOB-EOT-D19` | Transport | P0 | Capture both requests. | TLS only. | A5 | | | | |
| `TC-MOB-EOT-D20` | Nothing sensitive survives backgrounding | P1 | Enter a code, background the app, inspect the recents thumbnail. | The code is not visible in the recents snapshot and is not retained in unencrypted local storage. | A11 | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-EOT-E01` | Wrong-code message | P0 | Trigger C04 in Arabic, then English. | A **bilingual** inline error renders in the app's language; the field clears; the screen stays usable. | `E2E-MOB006-002` | | | | |
| `TC-MOB-EOT-E02` | Expired-code message steers to resend | P0 | Trigger D06 through the app. | The message tells the user the code expired and points them at **resend** — not a bare generic failure that leaves them retrying. | `E2E-MOB006-003` | | | | |
| `TC-MOB-EOT-E03` | Resend cap message | P0 | Trigger D09 through the app. | The **429** surfaces inline in the app's language and the resend action stays disabled. | `E2E-MOB006-005` | | | | |
| `TC-MOB-EOT-E04` | Network / 5xx | P0 | Force a network failure and a 500 on each endpoint. | The generic bilingual message is shown and **the entered digits are kept** so the user can retry. No navigation. | `E2E-MOB006-007` | | | | |
| `TC-MOB-EOT-E05` | Offline verify | P0 | Network off, submit a valid code. | A failure is surfaced. The app **must not** report the email as verified. | CB-06.2 | | | | |
| `TC-MOB-EOT-E06` | Offline resend | P1 | Network off, tap resend after the cooldown. | A failure is surfaced and the countdown does **not** restart as though a code had been sent. | CB-06.2 | | | | |
| `TC-MOB-EOT-E07` | Recovery | P1 | Restore the network and retry. | Verifies normally. | CB-06.3 | | | | |
| `TC-MOB-EOT-E08` | Countdown across backgrounding | P1 | Background the app mid-countdown, return after it should have elapsed. | The countdown reflects real elapsed time rather than freezing or resetting. | D-695 | | | | |
| `TC-MOB-EOT-E09` | Deep-link with no email | P1 | Open the route without the `email` query parameter. | Handled gracefully — no crash and no request with an empty address. | code | | | | |
| `TC-MOB-EOT-E10` | Server 500 body | P1 | Force a 500. | No stack trace, SQL, file path or internal id reaches the user. | CB-07.2 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-EOT-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | Title, subtitle, error and buttons mirror; the **OTP boxes and the echoed email stay LTR**. | `E2E-MOB006-008` | | | | |
| `TC-MOB-EOT-F02` | No hardcoded string | P0 | Compare every visible string in both languages. | Title, subtitle, countdown label, CTA, resend label, toast and every error are translated. | CB-02.3 | | | | |
| `TC-MOB-EOT-F03` | Countdown format in Arabic | P1 | Watch the countdown in Arabic. | The `mm:ss` value renders correctly and does not reverse or lose its separator. | CB-02.5 | | | | |
| `TC-MOB-EOT-F04` | Accessible names | P1 | Screen reader on; traverse the screen. | Each code box announces its position (for example "digit 3 of 6"); the CTA and resend announce their labels; the countdown state is announced or at least not misleading. | CB-08.1 | | | | |
| `TC-MOB-EOT-F05` | Errors announced | P1 | Trigger C04 with the screen reader on. | The error is announced when it appears, and the cleared field is not left silently empty. | CB-08.3 | | | | |
| `TC-MOB-EOT-F06` | Text scaling | P2 | Largest supported font size. | The six boxes still fit on one row without clipping the digits; the countdown and footer stay readable. | CB-08.5 | | | | |

### G. Data integrity and audit

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-EOT-G01` | Verification is audited | P1 | Complete C01; inspect the audit trail. | A row records the state change with a timestamp and source IP. | A9-7 | | | | |
| `TC-MOB-EOT-G02` | Failed attempts are audited | P1 | Complete C04 and D01. | Every wrong-code attempt is recorded, including the one that hits the cap. | A9-15, A1-12 | | | | |
| `TC-MOB-EOT-G03` | Resend is audited | P1 | Complete C05 and D09. | Each resend, and the capped resend, are recorded. | A9-7, A9-15 | | | | |
| `TC-MOB-EOT-G04` | Audit content is safe | P0 | Inspect the rows from G01–G03. | No verification code is stored. | A9-9 | | | | |
| `TC-MOB-EOT-G05` | Code row is consumed | P1 | After C01, inspect `AccountCodes` for the address. | The used code is marked consumed and cannot be reused. | A7 | | | | |
| `TC-MOB-EOT-G06` | No secret in the client log | P0 | Capture the device log across a full run. | No code is printed. | A9-9 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-EOT-H01` | Requirement satisfied | P0 | Run C01, C05, C06. | *"The system shall send a six-digit verification code to the email and shall require the code to be entered before the account proceeds"* is met, and a lost code is recoverable by resend. | **FR-102**, `T-04`, `T-06` | | | | |
| `TC-MOB-EOT-H02` | Anonymous surface is safe | P0 | Run D03 → D05, D12. | The two anonymous endpoints cannot be used to enumerate addresses, verify someone else's account, or flood a third party's mailbox. | **NFR-01**, A7, A7-8 | | | | |
| `TC-MOB-EOT-H03` | Verification does not over-grant | P0 | Run D15, D16. | Verifying an email grants **only** verification — not approval and not elevation. | **NFR-01**, A4 | | | | |
| `TC-MOB-EOT-H04` | Design parity | P1 | Compare the live render against Figma `505:837`. | Boxes, mail mark, countdown, footer, typography, colour and spacing match. | DoD-Gate-4 | | | | |
| `TC-MOB-EOT-H05` | Live-render gate | P0 | Capture a full screenshot, the console/device log and the network list for a complete run. | Screenshot captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-EOT-H06` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-EOT-H07` | Catalogue alignment | P1 | Cross-check against `E2E-MOB006-001…008`. | Every scenario is covered here and none contradicts the catalogue. | DoD-SES-7 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (69 authored + 8 applicable inherited blocks) | |
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
| No verification code recorded anywhere in the evidence | | |
| Every account created has been removed — cleanup register drained | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set.** This screen sits in the
middle of the registration chain — include [sign-up-form.md](sign-up-form.md)
and [sign-in.md](sign-in.md) in the regression pass.

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
| 1.0 | 2026-08-03 | SIMF Team | First issue. Grounded in `e2e/mobile-email-otp.md`, `otp_code_boxes.dart`, D-364 / D-695 / D-742 and FDS-001 §5. |

---

_Authored:_ 2026-08-03 · _Last reviewed:_ 2026-08-03
