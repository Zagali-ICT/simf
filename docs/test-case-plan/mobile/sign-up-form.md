# Test-Case Sheet — `Sign Up — credentials` (app screen #5)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | إنشاء حساب · Sign Up (step 1 — credentials) | **Doc id** | `TC-MOB-SUF` |
| **Route / screen id** | `/sign-up` (`RouteNames.signUpForm`) — app screen **#5** | **Surface** | Mobile app (Flutter) |
| **API under test** | `POST /api/v1/app/auth/sign-up` — `{ email, password, confirmPassword }` → **generic 201** `{ email, codeExpiresInSeconds }` | **Audience** | Guest — creates the account, does **not** sign in |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/sign-up-form/](../../pages/mobile/sign-up-form/README.md) · [e2e/mobile-sign-up-form.md](../../tests/e2e/mobile-sign-up-form.md) `E2E-MOB005-001…014` · [FDS-001](../../SIMF-FDS-001-Authentication-and-Login.md) `T-01…T-03` · Figma `168:3454` · D-198 / D-270 / D-370 / D-719 | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| Fields: email, password, confirm password — **no name field** at this step | `src/Mobile/simf_app/lib/features/account/sign_up_form_screen.dart` |
| Email cap **50** (LTR-pinned); password and confirm cap **128** | `widgets/account_form_field.dart` |
| Live password requirement list — length, uppercase, lowercase, digit, special (**five** checks) | `core/validation/password_validation.dart` |
| **Mandatory** terms-and-conditions checkbox; the terms link opens the terms screen in consent mode | D-719, `E2E-MOB005-011…014` |
| **Generic 201 always** — never a 409, never a different body for an existing address | D-198 / D-270; `E2E-MOB005-005` |
| Server re-validates `confirmPassword == password` | `SignUpRequestValidator`; D-270 |
| Server password policy: nine rules | `src/Shared/SIMF.Common/PasswordPolicy.cs` |
| Success: "check your email" toast → email-OTP screen carrying the **trimmed, lower-cased** email | `sign_up_form_screen.dart`; `E2E-MOB005-001` |
| Rate limits **20 req / 60 s / IP** and **5 req / 60 s / email** | `RateLimitOptions.cs` |

> **HARD RULE (Page_005 Logic L-4).** The endpoint **never** returns 409 and
> **never** varies its body between a new and an existing address. The Flutter
> "already registered" branch is dead code and must not be reintroduced.
> `TC-MOB-SUF-D01` is the guard for that rule.

> **The client mirrors five of the nine password rules.** The note under
> `E2E-MOB005-004` previously said the mirror was *"length ≥ 8 + a letter + a
> digit"*; it was **corrected on 2026-08-03** to the five structural rules in
> `unmetPasswordRequirements`. The server enforces four more, so a password can
> clear every on-screen check and still be refused — `TC-MOB-SUF-D07`…`D10` test
> that the server's reason reaches the user.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Unused address | `qa+suf-new@example.sa` — guaranteed **not** registered |
| **FX-2** Already-registered address | an address that already has an account |
| **FX-3** Address registered but **unverified** | signed up, code never entered |
| **FX-4** Address registered and **approved** | a fully live account |
| **FX-5** Uppercase / padded variant of FX-2 | `"  TAKEN@EXAMPLE.SA  "` — for the normalisation rows |
| Verification codes | Read from `SIMF_Identity.AccountCodes` or the test mailbox **at run time**. |
| API tool | REST client for §D. |
| Passwords | Throwaway values only. **Do not record any password used in the run** — refer to them as "the compliant password" / "the sequential password". |
| Cleanup | Every account created is tagged `QA-` and added to the cleanup register. This screen creates real accounts — **drain the register before sign-off.** |

> **No literal secret appears in this document.**

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | partial — loading only | | |
| CB-04 Auth gate and account state | yes — anonymous surface | | |
| CB-05 Session expiry and token refresh | **N-A** — no session is created | | |
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
| `TC-MOB-SUF-A01` | Screen chrome | P1 | Open sign-in → "create account". | Navy surface with the sweep; back chevron top-left; **globe** language toggle top-right; logo header; beige card holding **email**, **password**, **confirm password**; the terms checkbox with its underlined link; gold "create account" CTA; "have an account? sign in" foot. | Figma `168:3454`, D-370 | | | | |
| `TC-MOB-SUF-A02` | No name field at this step | P1 | Inspect the form. | The form collects **credentials only**. Name and profile data are collected later on the profile screen — a name field here would be a scope regression. | `sign_up_form_screen.dart` | | | | |
| `TC-MOB-SUF-A03` | Live requirement list | P0 | Type one character into the password field. | The list of **unmet** requirements appears and updates on every keystroke, clearing when all five are met. It does not appear before the field is touched. | `unmetPasswordRequirements` | | | | |
| `TC-MOB-SUF-A04` | Terms checkbox present and unchecked | P0 | Open the screen fresh. | The accept box is present and **unchecked** by default. A pre-ticked consent box would be a compliance defect. | D-719 | | | | |
| `TC-MOB-SUF-A05` | CTA gating | P1 | Fill fields one at a time. | The CTA reflects completeness; a submit attempt with anything missing produces an inline error rather than a silent no-op. | code | | | | |
| `TC-MOB-SUF-A06` | Busy state | P1 | Submit on a throttled connection. | CTA busy; fields disabled; no second submit possible. | code | | | | |
| `TC-MOB-SUF-A07` | Tablet width | P1 | Open on a tablet in portrait. | The card fills the frame proportionately; no dead side gutters. | responsive rule §13.7 | | | | |
| `TC-MOB-SUF-A08` | Keyboard | P1 | Focus each field on a small phone. | Every field, the requirement list, the checkbox and the CTA remain reachable above the keyboard. | CB-01 | | | | |

### B. Field validation (client)

| ID | Field | Pri | Test data | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUF-B01` | Email — empty | P0 | _(empty)_ | Inline **required** error; **no** request. | validator | | | | |
| `TC-MOB-SUF-B02` | Email — malformed | P0 | `not-an-email` · `admin@` · `@example.sa` · `admin example.sa` · `admin@example` | Inline **invalid email** for each; submit blocked; **no** request. | `E2E-MOB005-003` | | | | |
| `TC-MOB-SUF-B03` | Email — normalisation | P0 | `"  Visitor@Example.SA  "` | Accepted. The value **sent** and the value **carried to the email-OTP screen** are both **trimmed and lower-cased** (`visitor@example.sa`). | `E2E-MOB005-001` | | | | |
| `TC-MOB-SUF-B04` | Email — length boundary | P1 | 49 / 50 / attempt 51 characters | 49 and 50 type fully; the **51st cannot be entered**. | `AccountEmailField maxLength = 50` | | | | |
| `TC-MOB-SUF-B05` | Email — direction in Arabic | P1 | Switch to Arabic, type an address. | The address renders **left-to-right** inside the RTL layout. | `E2E-MOB005-008` | | | | |
| `TC-MOB-SUF-B06` | Password — empty | P0 | _(empty)_ | Inline **required** error; **no** request. | validator | | | | |
| `TC-MOB-SUF-B07` | Password — each rule listed individually | P0 | Values missing **only** the upper-case; only the lower-case; only a digit; only a special character; and one of 7 characters. | In each case exactly the **one** unmet requirement is named. The feedback is specific, not an all-or-nothing message. | `unmetPasswordRequirements` | | | | |
| `TC-MOB-SUF-B08` | Password — length boundary | P0 | 7 / 8 / 128 / attempt 129 characters | 7 keeps the length requirement listed and blocks submit; 8 satisfies it; 128 is accepted; the **129th cannot be entered**. | policy 8–128; `maxLength: 128` | | | | |
| `TC-MOB-SUF-B09` | Password — weak value blocks submit | P0 | A short value such as five letters | Inline failure; **no** request is sent. Confirm the user sees why — a tap that appears to do nothing is a defect. | `E2E-MOB005-004` | | | | |
| `TC-MOB-SUF-B10` | Confirm — mismatch | P0 | Password and confirm differing by one character | Inline **"the passwords do not match"**; **no** request. | `E2E-MOB005-002` | | | | |
| `TC-MOB-SUF-B11` | Confirm — case sensitivity | P1 | Same letters, different case | Treated as a **mismatch** — the comparison is exact. | validator | | | | |
| `TC-MOB-SUF-B12` | Confirm — reacts to a later password edit | P1 | Make them match, then edit the password field, then submit. | The mismatch is caught before any request. | — | | | | |
| `TC-MOB-SUF-B13` | Passwords are masked | P0 | Inspect both password fields. | Both masked by default; revealing is an explicit user action. | A7-2 | | | | |
| `TC-MOB-SUF-B14` | Whitespace-only inputs | P1 | `"   "` in each field in turn | Treated as empty → required error; **no** request. | `isBlank` | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUF-C01` | Golden path | P0 | 1. Enter **FX-1**, a compliant password and a matching confirm. 2. Tick the terms box. 3. Submit. | Exactly **one** `POST /app/auth/sign-up`; a generic **201**; the "check your email" toast; navigation to the **email-OTP** screen carrying the trimmed, lower-cased address. A verification code arrives. | `E2E-MOB005-001`, **FR-101**, **FR-102** | | | | |
| `TC-MOB-SUF-C02` | The user is **not** signed in | P0 | Complete C01, then inspect the auth state. | No tokens are issued and no session exists. Sign-up creates an account; it does not authenticate. | `E2E-MOB005-001` | | | | |
| `TC-MOB-SUF-C03` | **Terms gate blocks submit** | P0 | Fill every field validly, leave the terms box **unchecked**, submit. | The terms message is shown and **no** request is sent to `/app/auth/sign-up`. | `E2E-MOB005-011` (D-719) | | | | |
| `TC-MOB-SUF-C04` | Ticking the box clears the gate | P0 | After C03, tick the box, submit again. | The terms error clears immediately and the submit posts. | `E2E-MOB005-012` | | | | |
| `TC-MOB-SUF-C05` | Terms link opens consent mode | P1 | Tap the underlined terms link. | The terms screen opens in **consent mode**. Tapping accept returns and **auto-checks** the box; "create account" then proceeds. | `E2E-MOB005-013` | | | | |
| `TC-MOB-SUF-C06` | Declining does not accept | P0 | Open the terms link, then decline or back out. | The accept box stays **unchecked** and submit is still blocked with the terms error. Backing out must never be read as consent. | `E2E-MOB005-014` | | | | |
| `TC-MOB-SUF-C07` | Back chevron | P2 | Tap back; then repeat with no navigation history. | Pops to the previous screen; with no history it falls back to **sign-in**. | `E2E-MOB005-009` | | | | |
| `TC-MOB-SUF-C08` | Globe language toggle | P2 | Tap the globe in Arabic, then again. | Switches language and **persists** the choice; form values are not lost. | `E2E-MOB005-010` | | | | |
| `TC-MOB-SUF-C09` | "Have an account? Sign in" | P1 | Tap the foot link. | Navigates to sign-in, leaving the sign-up flow. | `E2E-MOB005-007` | | | | |
| `TC-MOB-SUF-C10` | Double submit | P0 | Tap the CTA twice rapidly. | **One** request and **one** account. Two accounts, or two codes emailed, is a defect. | CB-06.5, A4-10 | | | | |
| `TC-MOB-SUF-C11` | End-to-end continuation | P0 | Complete C01, then verify the emailed code on the next screen. | The account moves to the verified state and the flow continues to the profile step. | **FR-102**, `T-04` | | | | |

### D. Server-side and NCA security

> **Run every row here against the API directly**, not only through the app.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUF-D01` | **Enumeration — the hard rule** | P0 | POST valid credentials for **FX-1** (new), **FX-2** (registered), **FX-3** (registered unverified) and **FX-4** (registered approved). Compare status, body and headers. | **All four** return the **same generic 201** with the same body shape. **Never** a 409. Nothing distinguishes a new address from an existing one, and no "you already have an account" message appears anywhere in the app. | D-198 / D-270, `E2E-MOB005-005`, A7 | | | | |
| `TC-MOB-SUF-D02` | Enumeration — normalisation cannot leak | P0 | POST **FX-5** (uppercase and padded form of the registered FX-2). | Same generic 201. Case and padding do not produce a different outcome that would reveal existence. | A7 | | | | |
| `TC-MOB-SUF-D03` | Enumeration — timing | P1 | Time 10 requests for FX-1 and 10 for FX-2. | No consistent exploitable timing difference. Record both medians. | A7 | | | | |
| `TC-MOB-SUF-D04` | No duplicate account is created | P0 | POST valid credentials for **FX-2** (already registered). | The generic 201 is returned but **no second account** is created and the existing account's password is **not** changed. Verify both in the database. | D-198 | | | | |
| `TC-MOB-SUF-D05` | An existing account is not hijacked | P0 | POST for **FX-4** with a password the attacker chooses, then attempt to sign in with it. | Sign-in with the attacker's password **fails**. A sign-up request against an existing address must never overwrite its credentials. | A7, A4 | | | | |
| `TC-MOB-SUF-D06` | Server re-validates the confirmation | P0 | POST with `newPassword` and `confirmPassword` different. | Rejected server-side. The match is not a client-only rule. | `SignUpRequestValidator`, D-270 | | | | |
| `TC-MOB-SUF-D07` | Server rule — sequential run | P0 | POST a password passing all five client rules but containing a 3-character ascending or descending run. | Rejected server-side, and the reason reaches the user in their language. | `PasswordPolicy.HasSequentialRun`, A7-29 | | | | |
| `TC-MOB-SUF-D08` | Server rule — repeat run | P0 | POST a client-valid password with three identical characters in a row. | Rejected server-side with an actionable reason. | `PasswordPolicy.HasRepeatRun`, A7-29 | | | | |
| `TC-MOB-SUF-D09` | Server rule — common password | P0 | POST a client-valid password that is a common password or a leet-speak spelling of one. | Rejected server-side; leet substitution does not defeat the check. | `PasswordPolicy.IsCommon`, A7-28 | | | | |
| `TC-MOB-SUF-D10` | Server rule — resembles the identifier | P0 | POST a password equal to the submitted email, and one equal to its local part. | Both rejected server-side. | `PasswordPolicy.ResemblesIdentifier`, A7-29 | | | | |
| `TC-MOB-SUF-D11` | Server re-validates the email | P0 | POST `""`, `admin@`, a 300-character address, a null and a non-string JSON type. | Each rejected by the **server** with the standard envelope. | A3 | | | | |
| `TC-MOB-SUF-D12` | Over-posting is refused | P0 | POST the body with extra fields — `accountState`, `role`, `isApproved`, `userType`, `id`. | The extra fields are ignored. The created account is **not** approved, **not** elevated, and lands in the normal initial state. | A4, over-posting | | | | |
| `TC-MOB-SUF-D13` | New account starts unverified and unapproved | P0 | Complete C01, then inspect the account row and attempt to use the app. | The account is unverified and unapproved. It **cannot** sign in to content until the email is verified and, where required, approved. | `T-01`, `T-08`, CB-04.4 | | | | |
| `TC-MOB-SUF-D14` | Per-**IP** rate limit | P0 | 21 sign-up requests from one IP within 60 seconds, varying the email. | The 21st returns **429**. Mass account creation from one source is throttled. | `auth` = 20 / 60 s, A7-8 | | | | |
| `TC-MOB-SUF-D15` | Per-**email** rate limit | P0 | 6 sign-up requests for one address within 60 seconds. | The 6th returns **429**. | `auth-email` = 5 / 60 s | | | | |
| `TC-MOB-SUF-D16` | Verification-code cap | P0 | Trigger sign-up repeatedly for one address across an hour. | The number of codes emailed is capped; the mailbox is not floodable by an attacker who knows the address. Record the observed cap. | A4-10, A7-8 | | | | |
| `TC-MOB-SUF-D17` | Response leaks nothing | P0 | Inspect the 201 body and headers. | Only the address and the code lifetime. No account id, state, role or "already exists" hint. | A7, A9-9 | | | | |
| `TC-MOB-SUF-D18` | Password stored only as a hash | P0 | Inspect the created account row. | Stored as a hash. No plaintext and no reversible form. | A2 | | | | |
| `TC-MOB-SUF-D19` | No credential in a URL or log | P0 | Inspect navigation URLs, deep links and the device log across a run. | No password or code appears. The email in the route query is expected — confirm nothing else accompanies it. | A7-36, A9-9 | | | | |
| `TC-MOB-SUF-D20` | Transport | P0 | Capture the request. | TLS only. | A5 | | | | |
| `TC-MOB-SUF-D21` | Consent is recorded appropriately | P1 | Complete C01 with the box ticked. | Consent is client-side only by decision (D8) — **nothing** is added to the frozen sign-up wire contract. Confirm no consent field appears in the request body, and record how consent is evidenced. | D-719 (D8) | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUF-E01` | Wire failure keeps the form | P0 | Force a network failure, a 5xx and a 429 in turn, then submit. | The failure message (or the offline message) is shown; **the form keeps its values** so the user can retry; the app does **not** navigate to the email-OTP screen. | `E2E-MOB005-006` | | | | |
| `TC-MOB-SUF-E02` | Server error language | P0 | Trigger E01 in Arabic, then in English. | The server's message renders in the app's current language. | `E2E-MOB003-019` | | | | |
| `TC-MOB-SUF-E03` | Offline submit | P0 | Network off, submit a valid form. | A failure is surfaced. The app **must not** navigate onward and must not imply an account was created. | CB-06.2 | | | | |
| `TC-MOB-SUF-E04` | Recovery | P1 | Restore the network and submit again. | Succeeds, and creates exactly **one** account. | CB-06.3 | | | | |
| `TC-MOB-SUF-E05` | Server 500 | P1 | Force a 500. | Localized fallback; no crash; no navigation; no stack trace shown. | CB-07 | | | | |
| `TC-MOB-SUF-E06` | 400 the client missed | P1 | Force a server validation failure the client does not catch. | The server's field errors are surfaced against the right fields where possible, not swallowed into a generic banner. | A9 | | | | |
| `TC-MOB-SUF-E07` | Navigate away mid-request | P1 | Submit then immediately background or tap back. | No crash; no error painted on a disposed screen. Returning leaves a consistent state — and if the account was created, the user can still verify it. | code | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUF-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | Labels, inline errors and the CTA mirror; the **email field stays LTR**; the back chevron points the correct way. | `E2E-MOB005-008` | | | | |
| `TC-MOB-SUF-F02` | No hardcoded string | P0 | Compare every visible string in both languages. | Title, three labels, every validation error, all five requirement messages, the terms text and link, the terms error, the CTA, the toast and the foot link are all translated. | CB-02.3 | | | | |
| `TC-MOB-SUF-F03` | Requirement list readable in Arabic | P1 | Trigger A03 in Arabic. | All five messages render in Arabic, wrap rather than clip, and show no missing glyphs. | CB-02.4 | | | | |
| `TC-MOB-SUF-F04` | Accessible names | P1 | Screen reader on; traverse the screen. | Email, password, confirm and the **terms checkbox** each announce their own caption; the terms link is distinguishable from the checkbox; errors are announced. | `E2E-MOB003-021`, CB-08.1 | | | | |
| `TC-MOB-SUF-F05` | Text scaling | P2 | Largest supported font size. | Fields, the requirement list, the terms row and the CTA remain usable. | CB-08.5 | | | | |
| `TC-MOB-SUF-F06` | Errors are not colour-only | P1 | Trigger B10 and C03. | Failure is conveyed by text, not only by colour. | CB-08.4 | | | | |

### G. Data integrity and audit

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUF-G01` | Account creation is audited | P1 | Complete C01; inspect the audit trail. | A row records the creation with a timestamp and source IP. | A9-7 | | | | |
| `TC-MOB-SUF-G02` | The existing-address branch is audited | P1 | Complete D04; inspect the audit trail. | The attempt is recorded — this is the **only** externally invisible signal, and it must exist. | A9-15 | | | | |
| `TC-MOB-SUF-G03` | Validation failures are audited | P1 | Complete D07 or D11; inspect the audit trail. | The rejection is recorded. | A9-15 | | | | |
| `TC-MOB-SUF-G04` | Audit content is safe | P0 | Inspect the rows from G01–G03. | No password or verification code is stored. | A9-9 | | | | |
| `TC-MOB-SUF-G05` | No secret in the client log | P0 | Capture the device log across a full run. | No password or code is printed. | A9-9 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUF-H01` | Requirement satisfied | P0 | Run C01 and C11. | *"The system shall let a person create an account with an email address, a password and a password confirmation"* and *"shall send a six-digit verification code to the email"* are both met. | **FR-101**, **FR-102** | | | | |
| `TC-MOB-SUF-H02` | Enumeration resistance holds | P0 | Run D01 → D05. | A new and an existing address are indistinguishable, no duplicate account is created, and no existing account can be hijacked. | **NFR-01**, A7, D-198 | | | | |
| `TC-MOB-SUF-H03` | Password policy enforced | P0 | Run D06 → D10. | All nine server rules hold; the client mirrors five for instant feedback. | **NFR-01**, A7-29 | | | | |
| `TC-MOB-SUF-H04` | Consent is explicit and revocable at the point of entry | P0 | Run C03 → C06. | Registration cannot proceed without an explicit, unticked-by-default accept, and declining is honoured. | D-719 | | | | |
| `TC-MOB-SUF-H05` | Design parity | P1 | Compare the live render against Figma `168:3454`. | Strings, typography, colour, spacing and radii match. Record any deliberate deviation. | DoD-Gate-4 | | | | |
| `TC-MOB-SUF-H06` | Live-render gate | P0 | Capture a full screenshot, the console/device log and the network list for a complete run. | Screenshot captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-SUF-H07` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-SUF-H08` | Catalogue alignment | P1 | Cross-check against `E2E-MOB005-001…014`. | Every scenario is covered here and none contradicts it. (The password-mirror note under `E2E-MOB005-004` was corrected on 2026-08-03.) | DoD-SES-7 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (72 authored + 8 applicable inherited blocks) | |
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
| **Every account created has been removed** — cleanup register drained | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set.** Sign-up feeds the whole
registration chain — include the email-OTP, profile and interests sheets in the
regression pass.

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
| 1.0 | 2026-08-03 | SIMF Team | First issue. Grounded in `sign_up_form_screen.dart`, `account_form_field.dart`, `password_validation.dart`, `PasswordPolicy.cs` and `e2e/mobile-sign-up-form.md`. |

---

_Authored:_ 2026-08-03 · _Last reviewed:_ 2026-08-03
