# Test-Case Sheet — `Terms and Conditions` (app screen #9)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | الشروط والأحكام · Terms and Conditions | **Doc id** | `TC-MOB-TRM` |
| **Route / screen id** | `/terms` — and `/terms?consent=1` in **consent mode** (`RouteNames.terms`) — app screen **#9** | **Surface** | Mobile app (Flutter) |
| **API under test** | `GET /app/content/terms` | **Audience** | Guest — reachable signed out |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/terms/](../../pages/mobile/terms/README.md) · [e2e/mobile-terms.md](../../tests/e2e/mobile-terms.md) · Figma `505:1553` · D-375 (consent mode) · D-639 (clean-code freeze) · D-719 (the sign-up gate that consumes it) | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| Two modes — **read-only** (from the profile / More menu) and **consent** (`?consent=1`, opened from sign-up) | D-375, D-719 |
| In consent mode the accept action pops **true**; the back chevron pops **false** (a decline) | D-375, `E2E-MOB005-013` / `-014` |
| Content is served from `GET /app/content/terms` — it is **not** hardcoded in the app | PAGE-INDEX #9 |
| Empty and error states use the shared `SimfErrorState` | D-639 |

> **This screen carries legal weight.** If the terms fail to load, the user must
> not be able to accept them — `TC-MOB-TRM-C06` is the row that matters most.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Terms content published | the CMS/content source has terms in both languages |
| **FX-2** Terms content empty | the content source returns no body |
| **FX-3** Terms content in one language only | for the fallback row |
| Entry points | (a) sign-up form terms link → **consent mode**; (b) More menu / profile → **read-only** |
| Cleanup | none — this screen writes nothing. |

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | yes | | |
| CB-04 Auth gate and account state | yes — must be reachable **signed out** | | |
| CB-05 Session expiry and token refresh | **N-A** — anonymous content | | |
| CB-06 Network failure and retry | yes | | |
| CB-07 Server 500 and malformed payload | yes | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | yes — data screen | | |
| CB-10 Audit trail | **N-A** — no write | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-TRM-A01` | Screen chrome | P1 | Open from the More menu. | Back + centred title header; the terms body rendered as readable sections and bullet cards; the surface tint matches the design tokens. | Figma `505:1553`, D-639 | | | | |
| `TC-MOB-TRM-A02` | Consent-mode chrome | P0 | Open from the sign-up terms link. | The same content **plus** the accept action. Read-only mode shows **no** accept action. | D-375 | | | | |
| `TC-MOB-TRM-A03` | Long content scrolls | P1 | Scroll to the end of a long terms body. | The whole body is reachable; the accept action stays reachable in consent mode; no content is clipped. | CB-01.3 | | | | |
| `TC-MOB-TRM-A04` | Tablet width | P1 | Open on a tablet in portrait. | Text is centred at a readable measure — not stretched edge to edge and not pinned to a phone-width column. | responsive rule §13.7 | | | | |
| `TC-MOB-TRM-A05` | Loading state | P1 | Open on a throttled connection. | A loading state shows while the content is in flight — never a blank screen and never an empty terms body presented as the terms. | CB-03.1 | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-TRM-C01` | Content loads from the server | P0 | Open the screen as **FX-1**, and change the published terms in the content source, then reopen. | The body reflects the **server** content. The terms are not hardcoded in the app — a legal change must be publishable without an app release. | PAGE-INDEX #9 | | | | |
| `TC-MOB-TRM-C02` | Read-only mode | P0 | Open from the More menu. | The terms are readable and there is **no** accept action — this entry point cannot record consent. | D-375 | | | | |
| `TC-MOB-TRM-C03` | Consent mode — accept | P0 | Open from the sign-up terms link, tap accept. | The screen pops **true**; the sign-up form's accept box is **auto-checked**; sign-up can then proceed. | `E2E-MOB005-013` | | | | |
| `TC-MOB-TRM-C04` | Consent mode — decline by back | P0 | Open from the sign-up terms link, tap the back chevron. | The screen pops **false**; the accept box stays **unchecked**; sign-up remains blocked. Backing out must never be read as consent. | `E2E-MOB005-014` | | | | |
| `TC-MOB-TRM-C05` | Consent mode — system back gesture | P0 | Repeat C04 using the OS back gesture and the hardware back button. | Both are treated as a **decline**, exactly like the chevron. A gesture must not accidentally grant consent. | D-375 | | | | |
| `TC-MOB-TRM-C06` | **Cannot accept unread terms** | P0 | Force the content load to fail, then open in consent mode. | The error state is shown and the accept action is **not** available — a user must not be able to consent to terms that failed to load. If accept is offered over an error state, raise it as a **high-severity** defect. | D-375, CB-03.3 | | | | |
| `TC-MOB-TRM-C07` | Empty content | P1 | Open as **FX-2**. | The shared empty state is shown — not a blank white screen. In consent mode, accept is unavailable for the same reason as C06. | D-639, CB-03.2 | | | | |
| `TC-MOB-TRM-C08` | Pull-to-refresh | P1 | Pull down on the body, and on the empty and error states. | The content re-fetches in all three states. | CB-09 | | | | |
| `TC-MOB-TRM-C09` | Reachable signed out | P0 | Open the terms with no session. | The screen loads. Terms must be readable before anyone has an account. | CB-04.1 | | | | |
| `TC-MOB-TRM-C10` | Double-tap accept | P1 | Tap accept twice rapidly in consent mode. | Pops once; the sign-up form is not left in a confused state. | CB-06.5 | | | | |

### D. Server-side and content integrity

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-TRM-D01` | Endpoint is anonymous by design | P0 | Call `GET /app/content/terms` with no token. | Succeeds. This is a legitimate anonymous endpoint — public legal text that must be readable before sign-up. | A1 (anonymous surface) | | | | |
| `TC-MOB-TRM-D02` | Endpoint returns content only | P0 | Inspect the response. | Only the terms content. No user data, no account state, no internal identifiers. | A9-9 | | | | |
| `TC-MOB-TRM-D03` | Content is not user-injectable | P0 | Confirm who can publish the terms body. | Only an authorised Control Panel role can change it. An unauthenticated or under-privileged caller cannot write the terms. | A1, A8 | | | | |
| `TC-MOB-TRM-D04` | Rendered content cannot inject | P0 | Publish terms containing script tags, markup and control characters, then render them. | The content renders as **text**. No markup is executed and no layout is broken by injected content. | A3 | | | | |
| `TC-MOB-TRM-D05` | Transport | P0 | Capture the request. | TLS only. | A5 | | | | |
| `TC-MOB-TRM-D06` | Consent is client-side only | P1 | Complete C03 and inspect the sign-up request body. | Consent is recorded client-side by decision (D8) — **no** consent field is added to the frozen sign-up wire contract. Record how consent is evidenced for audit. | D-719 (D8) | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-TRM-E01` | Offline | P0 | Network off, open the screen. | The shared error state with a retry. Retrying after restoring the network loads the content. | CB-06.1, CB-06.3 | | | | |
| `TC-MOB-TRM-E02` | Server 500 | P1 | Force a 500. | The shared error state; no crash; no stack trace shown. | CB-07 | | | | |
| `TC-MOB-TRM-E03` | Malformed payload | P1 | Return a body with a missing or wrongly-typed field. | Degrades to the error or empty state rather than crashing. | CB-07.3 | | | | |
| `TC-MOB-TRM-E04` | Back mid-load | P1 | Open in consent mode and immediately back out while loading. | Pops **false** cleanly; no crash; the sign-up form is unchanged. | — | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-TRM-F01` | Arabic RTL | P0 | Run the sheet in Arabic. | Header, body, bullets and the accept action mirror; the back chevron points the correct way. | CB-02.1 | | | | |
| `TC-MOB-TRM-F02` | Content language follows the app | P0 | Switch language with the terms open. | The **Arabic** terms are served in Arabic and the **English** terms in English — not one language's text under the other's chrome. | CB-02.3 | | | | |
| `TC-MOB-TRM-F03` | Single-language content | P1 | Open as **FX-3** in the missing language. | A sensible fallback is shown rather than an empty screen. Record the exact behaviour — silently serving the wrong language for a legal document is worth raising. | CB-03.4 | | | | |
| `TC-MOB-TRM-F04` | No hardcoded chrome string | P0 | Compare the title, accept label, empty state and error state in both languages. | All translated. | CB-02.3 | | | | |
| `TC-MOB-TRM-F05` | Accessible reading | P1 | Screen reader on; traverse the body. | Headings and bullets are announced with structure, not as one undifferentiated block; the accept action announces its label. | CB-08.1, CB-08.3 | | | | |
| `TC-MOB-TRM-F06` | Text scaling | P2 | Largest supported font size. | The body reflows; the accept action stays reachable; nothing is clipped. | CB-08.5 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-TRM-H01` | Consent is meaningful | P0 | Run C03 → C07. | A user can read the terms before accepting, cannot accept them unread or unloaded, and a decline is honoured. | D-719, D-375 | | | | |
| `TC-MOB-TRM-H02` | Terms are publishable without a release | P0 | Run C01. | Legal text is server-driven. | PAGE-INDEX #9 | | | | |
| `TC-MOB-TRM-H03` | Design parity | P1 | Compare the live render against Figma `505:1553`. | Typography, spacing, bullet cards and the surface tint match. | DoD-Gate-4 | | | | |
| `TC-MOB-TRM-H04` | Live-render gate | P0 | Capture a full screenshot, the device log and the network list. | Screenshot captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-TRM-H05` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (36 authored + 7 applicable inherited blocks) | |
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
| Both entry modes (read-only and consent) exercised | | |
| Evidence captured for every PASS and FAIL | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set**, and include
[sign-up-form.md](sign-up-form.md) — the consent gate depends on this screen's
pop value.

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
| 1.0 | 2026-08-03 | SIMF Team | First issue. Grounded in `PAGE-INDEX.md` #9, D-375 / D-639 / D-719 and `e2e/mobile-sign-up-form.md` `E2E-MOB005-013` / `-014`. |

---

_Authored:_ 2026-08-03 · _Last reviewed:_ 2026-08-03
