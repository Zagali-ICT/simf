# Test-Case Sheet — `Guest Mode` (app screen #12)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | الدخول كزائر · Guest Mode | **Doc id** | `TC-MOB-GST` |
| **Route / screen id** | app screen **#12** `guestMode` — the guest landing, reached from the sign-in screen | **Surface** | Mobile app (Flutter) |
| **API under test** | **none** — the landing itself makes no call | **Audience** | Guest (privilege **0**), signed out, **no token** |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/guest-mode/](../../pages/mobile/guest-mode/README.md) · [e2e/mobile-guest-mode.md](../../tests/e2e/mobile-guest-mode.md) · [e2e/mobile-sign-in.md](../../tests/e2e/mobile-sign-in.md) `E2E-MOB003-014` · D-325 (guest entry) · D-644 (clean-code freeze) | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| Reached from the underlined **"الدخول كزائر / Enter as guest"** link on sign-in | D-325, `E2E-MOB003-014` |
| "Continue as guest" enters public Home **with no token** | `E2E-MOB003-014` |
| Guest-visible content: sessions, speakers, venue map, media | `E2E-MOB003-014` |
| Account-only actions — badge, notifications, booking, contacts — still **gate to sign-in** | `E2E-MOB003-014` |
| App privilege ladder: **Guest = 0**, Visitor = 1, Moderator = 2, Staff = 3 | app privilege enum |
| A static screen (no API), tokenised, using the shared back control | D-644 |

> **Guest mode is a real screen.** The Figma node map previously recorded it as
> *dissolved* into Home (`758:2910`); that entry was **corrected on 2026-08-03**
> after confirming `GuestModeScreen` exists at
> `lib/features/guest/guest_mode_screen.dart` and is routed at `/guest`
> (router route 12). It is the guest **landing**; "continue as guest" then
> enters the guest **Home** variant, which is what `758:2910` describes.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Signed-out app | force-quit with no stored session, or sign out first |
| **FX-2** Previously signed-in device | a device where a session existed and was signed out — to prove no residual token is reused |
| API tool | REST client, to confirm guest-visible endpoints from an unauthenticated caller. |
| Cleanup | none — this screen creates nothing. |

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | **N-A** — static landing | | |
| CB-04 Auth gate and account state | yes — this screen defines the guest boundary | | |
| CB-05 Session expiry and token refresh | **N-A** — no session | | |
| CB-06 Network failure and retry | partial — the landing is offline-safe | | |
| CB-07 Server 500 and malformed payload | **N-A** — no API call | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A** — no server data | | |
| CB-10 Audit trail | **N-A** — no write | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-GST-A01` | Screen chrome | P1 | Tap the guest link on sign-in. | The guest landing renders with its explanatory copy, the **continue as guest** action and the shared back control. | D-644 | | | | |
| `TC-MOB-GST-A02` | The offer is honest | P0 | Read the copy. | It states plainly what a guest **can** and **cannot** do. A user must not be led to expect booking or a badge and then be bounced to sign-in. | D-325 | | | | |
| `TC-MOB-GST-A03` | Tablet width | P1 | Open on a tablet in portrait. | Content fills the frame proportionately; no dead side gutters. | responsive rule §13.7 | | | | |
| `TC-MOB-GST-A04` | Small phone | P1 | Open on the smallest handset. | Nothing is clipped; the action stays reachable. | CB-01.3 | | | | |
| `TC-MOB-GST-A05` | Offline render | P1 | Airplane mode, open the screen. | It renders fully — the landing has no network dependency. | CB-06.1 | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-GST-C01` | Entry from sign-in | P0 | Tap the underlined guest link on sign-in as **FX-1**. | The guest landing opens. | `E2E-MOB003-014` | | | | |
| `TC-MOB-GST-C02` | **Continue as guest issues no token** | P0 | Tap continue, then inspect the auth state and the network traffic. | The app enters public Home with **no token**, no session and no credential of any kind. | `E2E-MOB003-014` | | | | |
| `TC-MOB-GST-C03` | Guest content is browsable | P0 | From guest Home, open sessions, speakers, the venue map and media. | Each loads and is readable. | `E2E-MOB003-014` | | | | |
| `TC-MOB-GST-C04` | **Account-only actions gate to sign-in** | P0 | From guest Home attempt: badge, notifications, seat booking, contacts, rating, asking a question. | Each either hides its entry point or routes to **sign-in**. None performs the action and none shows a broken or silently-failing control. | `E2E-MOB003-014`, CB-04.2 | | | | |
| `TC-MOB-GST-C05` | Back to sign-in | P1 | Tap the back control on the landing. | Returns to sign-in. | D-644 | | | | |
| `TC-MOB-GST-C06` | Escape hatch to sign-in | P0 | From guest Home, find the route to sign in or create an account. | A clear path exists. A guest must always be able to convert to a real account. | D-325 | | | | |
| `TC-MOB-GST-C07` | Guest state survives a restart sensibly | P1 | Enter guest mode, force-quit, relaunch. | The app returns to a defined surface — record whether it resumes guest Home or returns to sign-in. Either is acceptable; an inconsistent or blank result is not. | — | | | | |
| `TC-MOB-GST-C08` | No residual session is reused | P0 | Sign out on **FX-2**, then enter guest mode. | The guest session carries **no** trace of the previous account — no name, no avatar, no cached personal data, and no reusable token. | A7, CB-05.4 | | | | |
| `TC-MOB-GST-C09` | Sign-in from guest promotes correctly | P0 | Enter guest mode, then sign in as an approved visitor. | The app promotes from Guest to **Visitor** and account-only actions become available without an app restart. | `E2E-MOB003-004` | | | | |
| `TC-MOB-GST-C10` | Double-tap continue | P2 | Tap continue twice rapidly. | Enters Home once; the route is not pushed twice. | CB-06.5 | | | | |

### D. Server-side and NCA security

> The guest boundary must be enforced by the **server**, not only by hidden
> buttons. Run these rows with an unauthenticated REST client.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-GST-D01` | Guest-visible endpoints are genuinely anonymous | P0 | Call the sessions, speakers, venue-map and media endpoints with **no** token. | Each succeeds. These are the intended public reads. | A1 (anonymous surface) | | | | |
| `TC-MOB-GST-D02` | **Account endpoints refuse anonymous callers** | P0 | Call the badge, notifications, seat-reservation, contacts, rating and question endpoints with **no** token. | Every one is **refused**. Hiding the button in the client is not access control. | CB-04.3, A1 | | | | |
| `TC-MOB-GST-D03` | Public reads leak no personal data | P0 | Inspect the anonymous responses from D01. | They carry public event content only — no attendee names, no email addresses, no account states, no internal identifiers. | A9-9, A1-14 | | | | |
| `TC-MOB-GST-D04` | Public reads are bounded | P1 | Call the anonymous list endpoints with a very large page size and crafted parameters. | Results are bounded. An anonymous caller cannot dump an entire table. | A1-14 | | | | |
| `TC-MOB-GST-D05` | Guest privilege is 0 | P0 | Inspect the app's resolved privilege in guest mode. | **Guest = 0.** It is never silently treated as Visitor, and no privileged route resolves. | app privilege enum, CB-04.2 | | | | |
| `TC-MOB-GST-D06` | Deep links do not bypass the boundary | P0 | While in guest mode, open a deep link straight into a signed-in-only screen. | Refused or routed to sign-in. Protected content does not flash first. | CB-04.2 | | | | |
| `TC-MOB-GST-D07` | Anonymous reads are rate-limited | P1 | Send a burst of anonymous requests from one IP. | The global per-IP limit applies to unauthenticated traffic too. Record the observed behaviour. | A7-8 | | | | |
| `TC-MOB-GST-D08` | Transport | P0 | Capture the anonymous requests. | TLS only. | A5 | | | | |
| `TC-MOB-GST-D09` | Nothing personal is stored for a guest | P0 | Inspect local storage after a guest session. | No token, no profile and no personal data is written. | A11, A2 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-GST-F01` | Arabic RTL | P0 | Run the sheet in Arabic. | The landing mirrors; the back control points the correct way. | CB-02.1 | | | | |
| `TC-MOB-GST-F02` | No hardcoded string | P0 | Compare every visible string in both languages. | Copy, the continue action and the back control are translated. | CB-02.3 | | | | |
| `TC-MOB-GST-F03` | Gate messages are translated | P0 | Trigger C04 in both languages. | Every "sign in to continue" prompt is translated. | CB-02.3 | | | | |
| `TC-MOB-GST-F04` | Accessible names | P1 | Screen reader on; traverse the screen. | The copy is announced and the continue and back controls announce their labels. | CB-08.1 | | | | |
| `TC-MOB-GST-F05` | Text scaling | P2 | Largest supported font size. | Copy reflows; the action stays reachable. | CB-08.5 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-GST-H01` | A visitor can browse without an account | P0 | Run C01 → C03. | Public event content is reachable with no sign-up. | D-325 | | | | |
| `TC-MOB-GST-H02` | The guest boundary is server-enforced | P0 | Run C04, D02, D05, D06. | No account-only capability is reachable by a guest, by any route. | **NFR-01**, A1 | | | | |
| `TC-MOB-GST-H03` | A guest can always convert | P0 | Run C06, C09. | A clear path to sign in or register exists and promotion works. | D-325 | | | | |
| `TC-MOB-GST-H04` | Documentation alignment | P1 | Compare the shipped screen against `PAGE-INDEX.md` #12 and the Figma node map entry. | Both now describe the same thing: a real routed guest **landing** at `/guest`, with `758:2910` being the guest **Home** variant reached after "continue as guest". Confirm the build still matches; report any drift. | DoD-SES-7 | | | | |
| `TC-MOB-GST-H05` | Live-render gate | P0 | Capture a full screenshot, the device log and the network list for a guest session. | Screenshot captured; **zero** console errors; **zero** failed assets; no horizontal overflow; **zero** authenticated requests. | DoD-Gate-4 | | | | |
| `TC-MOB-GST-H06` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (35 authored + 5 applicable inherited blocks) | |
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
| Every account-only action verified as gated **at the API** | | |
| Both language runs completed | | |
| Evidence captured for every PASS and FAIL | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set.** The guest boundary touches
every public screen — include the sessions, speakers, venue-map and media sheets
in the regression pass once they are authored.

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
| 1.0 | 2026-08-03 | SIMF Team | First issue. Grounded in `PAGE-INDEX.md` #12, D-325 / D-644 and `e2e/mobile-sign-in.md` `E2E-MOB003-014`. |

---

_Authored:_ 2026-08-03 · _Last reviewed:_ 2026-08-03
