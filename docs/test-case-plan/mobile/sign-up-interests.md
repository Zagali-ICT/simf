# Test-Case Sheet — `Sign Up — interests` + the single profile save (app screen #7-01)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | اختر اهتماماتك · Sign Up — interests | **Doc id** | `TC-MOB-SUI` |
| **Route / screen id** | `/sign-up/interests` (`RouteNames.signUpInterests`) — app screen **#7-01**. **Auth-gated** | **Surface** | Mobile app (Flutter) |
| **APIs under test** | `GET /app/account/interests` (lookup) · **`POST /app/account/user-profile`** — the **single** upsert carrying the screen-#7 data **and** `interestIds` · `POST …/user-profile/id-image` (multipart, optional, after the row exists) | **Audience** | Visitor — signed in, profile incomplete, arriving from screen #7 |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/sign-up-interests/](../../pages/mobile/sign-up-interests/README.md) · [e2e/mobile-sign-up-interests.md](../../tests/e2e/mobile-sign-up-interests.md) `E2E-MOB7A-001…007` · Figma `505:1083` · D-332 / D-365 / D-550 · Sibling: [sign-up-visitor.md](sign-up-visitor.md) | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| **This screen performs the only write in the sign-up flow.** Screen #7 saves nothing; both screens' data land in **one** `POST /app/account/user-profile`. | D-332, `E2E-MOB7A-003` |
| Selection rule **1 to 10** — Continue disabled below 1, capped at 10 with a toast, with an `n/10` counter | `E2E-MOB7A-002` |
| Two-column pill grid — **gold** selected, `navyDeep` with a border unselected | D-365, Figma `505:1083` |
| Primary button is **متابعة (Continue)** — the "Save" label was removed | D-365 |
| The optional ID image is uploaded **after** the profile row exists, and a failure there is **non-blocking** | `E2E-MOB7A-004` |
| An empty interests lookup is a **seed-data** condition, not a screen error | `E2E-MOB7A-006` |
| The same screen also serves the post-sign-up edit surface (`myInterests`, edit mode) | shared screen, 2026-07-21 |

> **This is the commit point of registration.** Everything the user typed across
> two screens is written here in a single request. The rows that matter most are
> `C01` (the whole payload lands), `D03` (a partial save cannot happen) and `E01`
> (a failure does not lose the user's work).

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Completed screen #7 draft | fill [sign-up-visitor.md](sign-up-visitor.md) `TC-MOB-SUV-C01` and tap Next — arrive here with a full draft in memory |
| **FX-2** Draft including an optional ID image | as FX-1 with an ID image picked on screen #7 |
| **FX-3** Interests lookup with zero rows | a database with no seeded interests |
| **FX-4** Interests lookup with more than 10 rows | needed to exercise the cap |
| API tool | REST client for §D. |
| Cleanup | Every account and uploaded image tagged `QA-`; added to the cleanup register. |

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | yes — the lookup can be empty | | |
| CB-04 Auth gate and account state | yes — **auth-gated** | | |
| CB-05 Session expiry and token refresh | yes — the save can outlive an access token | | |
| CB-06 Network failure and retry | yes | | |
| CB-07 Server 500 and malformed payload | yes | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A** — selection screen | | |
| CB-10 Audit trail | yes — this screen writes | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUI-A01` | Screen chrome | P1 | Arrive from screen #7 as **FX-1**. | The **اختر اهتماماتك** heading, the long helper text, a **two-column pill grid**, the centred `n/10` counter and the **متابعة** primary button. No "Save" label. | Figma `505:1083`, D-365 | | | | |
| `TC-MOB-SUI-A02` | Pill states | P1 | Select and deselect a pill. | Selected pills are **gold**; unselected are `navyDeep` with a border. The state change is immediate and unambiguous. | D-365 | | | | |
| `TC-MOB-SUI-A03` | Counter tracks selection | P0 | Select and deselect several pills. | The `n/10` counter always matches the actual number selected. | `E2E-MOB7A-002` | | | | |
| `TC-MOB-SUI-A04` | Continue gating | P0 | Open the screen with nothing selected. | **متابعة** is **disabled** at zero selections and enables at one. | `E2E-MOB7A-002` | | | | |
| `TC-MOB-SUI-A05` | Busy state | P1 | Save on a throttled connection. | A "please wait" state is shown, the pills are disabled and no second submit is possible. | `E2E-MOB7A-001` | | | | |
| `TC-MOB-SUI-A06` | Tablet width | P1 | Open on a tablet in portrait. | The pill grid fills the frame proportionately; no dead side gutters and no stretched pills. | responsive rule §13.7 | | | | |
| `TC-MOB-SUI-A07` | Long list scrolls | P1 | Open with **FX-4**. | The grid scrolls smoothly; the counter and the Continue button stay reachable; no horizontal overflow. | CB-01.3 | | | | |

### B. Selection validation

| ID | Rule | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUI-B01` | Minimum — zero blocked | P0 | Attempt to continue with nothing selected. | Blocked; **no** request fires. | `E2E-MOB7A-002` | | | | |
| `TC-MOB-SUI-B02` | Minimum boundary — one allowed | P0 | Select exactly **one** and continue. | Accepted. | `E2E-MOB7A-002` | | | | |
| `TC-MOB-SUI-B03` | Maximum boundary — ten allowed | P0 | Select exactly **ten** and continue. | Accepted. | `E2E-MOB7A-002` | | | | |
| `TC-MOB-SUI-B04` | Maximum — eleventh refused | P0 | With ten selected, tap an eleventh pill. | The selection is **capped at ten**, a toast explains the limit, and the counter stays at `10/10`. | `E2E-MOB7A-002` | | | | |
| `TC-MOB-SUI-B05` | Deselect frees a slot | P1 | At ten, deselect one, then select another. | The swap works and the counter stays correct. | — | | | | |
| `TC-MOB-SUI-B06` | Rapid toggling | P2 | Toggle one pill rapidly ten times. | The final state is consistent and the counter is correct — no drift. | — | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUI-C01` | **Golden path — the single save** | P0 | Arrive as **FX-1**, pick 1–10 interests, tap **متابعة**. | Exactly **one** `POST /app/account/user-profile`. A "please wait" state shows. On success the app routes to the **registration confirmation** screen. | `E2E-MOB7A-001` | | | | |
| `TC-MOB-SUI-C02` | **The payload carries everything** | P0 | Capture the request body from C01. | The **one** request carries **both** the screen-#7 fields (names, both job titles, nationality, date of birth, document, place of birth, mobile, plate, profile type, organisation, gender) **and** `interestIds`. There is **no** separate interests write. | `E2E-MOB7A-003` | | | | |
| `TC-MOB-SUI-C03` | ID image uploaded after the row exists | P1 | Complete C01 as **FX-2**. | The multipart `id-image` upload happens **after** the profile row is created — never before, and never as part of the same request. | `E2E-MOB7A-004` | | | | |
| `TC-MOB-SUI-C04` | ID-image failure is non-blocking | P0 | As **FX-2**, force the `id-image` upload to fail. | The profile save still **succeeds** and the user still advances. The image failure is surfaced with a way to retry later — it does not roll back or block registration. | `E2E-MOB7A-004` | | | | |
| `TC-MOB-SUI-C05` | Back preserves the screen-#7 draft | P0 | Select some interests, tap back to screen #7, then return. | Screen #7 still holds every entered value, and the interest selection is preserved on return. Losing a two-screen form on a back tap is a defect. | `E2E-MOB7A-005` | | | | |
| `TC-MOB-SUI-C06` | Empty lookup | P1 | Open with **FX-3**. | The screen shows its **empty state** — a seed-data condition, not a blocking error. Record whether the user can still proceed or is genuinely stuck; if stuck, that is a deployment defect worth raising. | `E2E-MOB7A-006` | | | | |
| `TC-MOB-SUI-C07` | Double submit | P0 | Tap **متابعة** twice rapidly. | **One** request and **one** profile row. Two profiles, or a duplicated interest set, is a defect. | CB-06.5, A4-10 | | | | |
| `TC-MOB-SUI-C08` | Selection round-trips | P0 | Complete C01, then reopen the interests edit surface. | Exactly the interests selected are shown as selected — none added, none dropped. | shared edit surface | | | | |
| `TC-MOB-SUI-C09` | Profile becomes complete | P0 | Complete C01, then sign out and sign in again. | `profileComplete` is now **true** and sign-in routes to **Home**, not back into the profile form. | `E2E-MOB003-013` | | | | |

### D. Server-side and NCA security

> **Run these against the API directly**, bypassing the app.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUI-D01` | **Auth gate** | P0 | Open `/sign-up/interests` with no session; call the lookup and the upsert with no token. | The route redirects to sign-in; both endpoints refuse. | `E2E-MOB7A-007` | | | | |
| `TC-MOB-SUI-D02` | Profile is scoped to the caller | P0 | POST the upsert while authenticated as user A, targeting user B's id. | Refused. The endpoint writes the caller's own `sub` only. | A1 | | | | |
| `TC-MOB-SUI-D03` | **No partial save** | P0 | POST an upsert that fails server validation on one field. | **Nothing** is persisted — the profile is not half-written with the valid fields. Verify the row afterwards. | A4 | | | | |
| `TC-MOB-SUI-D04` | Interest-count rule mirrored | P0 | POST with **zero** interest ids, and with **eleven**. | Both rejected server-side with 400. The 1–10 rule is not client-only. | `E2E-MOB7A-002` | | | | |
| `TC-MOB-SUI-D05` | Unknown interest ids refused | P0 | POST with a fabricated interest id, and with a duplicate id repeated. | The unknown id is refused; duplicates do not create duplicate rows. | A3 | | | | |
| `TC-MOB-SUI-D06` | **Over-posting is refused** | P0 | POST the upsert with `accountState`, `isApproved`, `role`, `userType`, `profileComplete` and another user's `userId`. | Every one is ignored. Completing a profile must not approve or elevate the account. | A4, A1 | | | | |
| `TC-MOB-SUI-D07` | Completion does not grant approval | P0 | Complete C01, then attempt to reach approval-gated content. | The account is complete but still **PendingApproval** where approval is required. It reaches no approval-gated content. | CB-04.4 | | | | |
| `TC-MOB-SUI-D08` | Screen-#7 rules re-enforced here | P0 | POST the upsert with an invalid name part-count, an under-18 date of birth, a bad national ID and an invalid mobile. | Each rejected with 400 — the server is the authority even though the client validated on the previous screen. | `E2E-MOB007-021`, `-007`, `-005`, `-012` | | | | |
| `TC-MOB-SUI-D09` | ID-image upload — type and size | P0 | Upload a non-image, an oversized file and a mismatched extension. | Each refused with a clear error. | A6 | | | | |
| `TC-MOB-SUI-D10` | ID-image is owner-scoped | P0 | Attempt to attach an image to another user's profile, and to retrieve someone else's. | Refused in both directions. | A1, A9-9 | | | | |
| `TC-MOB-SUI-D11` | Lookup is not an enumeration tool | P1 | Call the interests lookup with crafted parameters. | It returns the interest catalogue only — no user data and no unbounded dump. | A1-14 | | | | |
| `TC-MOB-SUI-D12` | Transport | P0 | Capture the upsert and the image upload. | TLS only. Personal data and identity documents never traverse plain HTTP. | A5 | | | | |
| `TC-MOB-SUI-D13` | No personal data in the log | P0 | Capture the device log across a full save. | No national ID, mobile number or image payload is printed. | A9-9 | | | | |
| `TC-MOB-SUI-D14` | Session expiry at the save | P0 | Let the access token expire while sitting on this screen, then save. | The token refreshes silently and the save completes. If the absolute session has ended, the user is signed out cleanly **without** a partial write. | CB-05.1, CB-05.3 | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUI-E01` | **Save fails — nothing is lost** | P0 | Force a server validation failure and a 500 on the upsert. | The message is shown; the **interest selection and the whole carried screen-#7 state are preserved**; tapping back to screen #7 still shows every value. The user can correct and retry without retyping two screens. | `E2E-MOB7A-005` | | | | |
| `TC-MOB-SUI-E02` | Server field errors are attributable | P1 | Force a validation failure on a screen-#7 field. | The user is told **which** field failed and can get back to it — not left with a generic banner on a screen that does not contain the offending field. | A9 | | | | |
| `TC-MOB-SUI-E03` | Offline save | P0 | Network off, tap **متابعة**. | A failure is surfaced. The app **must not** advance to the confirmation screen and must not imply the profile was saved. | CB-06.2 | | | | |
| `TC-MOB-SUI-E04` | Recovery | P1 | Restore the network and save again. | Succeeds, and creates exactly **one** profile row. | CB-06.3 | | | | |
| `TC-MOB-SUI-E05` | Lookup fails | P1 | Force the interests lookup to fail. | An error state with a retry; retrying loads the list. | CB-03.3 | | | | |
| `TC-MOB-SUI-E06` | Server 500 body | P1 | Force a 500. | No stack trace, SQL or internal id reaches the user. | CB-07.2 | | | | |
| `TC-MOB-SUI-E07` | Navigate away mid-save | P1 | Tap **متابعة**, then immediately background the app. | No crash. On return the state is consistent — either the save completed and the user is past it, or it did not and they can retry. There is no ambiguous half-state. | — | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUI-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | The heading, helper, pill grid, counter and button mirror. The two-column grid fills right-to-left. | CB-02.1 | | | | |
| `TC-MOB-SUI-F02` | Interest labels follow the locale | P0 | Switch language with the list loaded. | Every interest label switches language with the app. | CB-02.3 | | | | |
| `TC-MOB-SUI-F03` | No hardcoded string | P0 | Compare every visible string in both languages. | Heading, helper, counter, button, cap toast, empty state and every error are translated. | CB-02.3 | | | | |
| `TC-MOB-SUI-F04` | Pill selection is not colour-only | P0 | Inspect a selected pill with the screen reader on. | Selection is announced as a state, and is visually conveyed by more than the gold fill alone. | CB-08.4 | | | | |
| `TC-MOB-SUI-F05` | Accessible names | P1 | Screen reader on; traverse the grid. | Every pill announces its label and selected state; the counter and the button announce their labels; the cap toast is announced. | CB-08.1 | | | | |
| `TC-MOB-SUI-F06` | Text scaling | P2 | Largest supported font size. | Long Arabic interest labels wrap inside their pills rather than clipping; the grid stays usable. | CB-08.5 | | | | |

### G. Data integrity and audit

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUI-G01` | Round-trip fidelity | P0 | Complete C01, then inspect the profile in the app and in the Control Panel. | Every field entered across both screens is stored exactly as entered, and the interest set matches the selection. | `E2E-MOB7A-003` | | | | |
| `TC-MOB-SUI-G02` | Save is audited | P1 | Complete C01; inspect the audit trail. | A row records the profile write with the actor, timestamp and source IP. | A9-7, CB-10.1 | | | | |
| `TC-MOB-SUI-G03` | Rejected save is audited | P1 | Complete D03; inspect the audit trail. | The rejection is recorded, not silently dropped. | A9-15 | | | | |
| `TC-MOB-SUI-G04` | Audit content is safe | P0 | Inspect the rows from G02–G03. | No national ID, mobile number or image payload is stored in the audit row beyond what the audit policy allows. | A9-9 | | | | |
| `TC-MOB-SUI-G05` | Re-save does not duplicate | P0 | Complete C01, then run the upsert again with a different interest set. | The profile is **updated**, not duplicated, and the old interest set is replaced rather than appended. | A4 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUI-H01` | Registration completes | P0 | Run C01, C09. | A new visitor can complete registration end to end and is thereafter routed to Home rather than back into the form. | **FR-2xx** | | | | |
| `TC-MOB-SUI-H02` | The save is atomic and lossless | P0 | Run C02, D03, E01. | Everything is written in one request, a failure writes nothing, and no user input is lost on failure. | **NFR-01**, A4 | | | | |
| `TC-MOB-SUI-H03` | Completion does not over-grant | P0 | Run D06, D07. | Completing a profile grants no approval and no elevation. | **NFR-01**, A1, A4 | | | | |
| `TC-MOB-SUI-H04` | Design parity | P1 | Compare the live render against Figma `505:1083`. | Pill grid, colours, heading, helper, counter and the **متابعة** button match. | DoD-Gate-4 | | | | |
| `TC-MOB-SUI-H05` | Live-render gate | P0 | Capture a full screenshot, the device log and the network list for a complete save. | Screenshot captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-SUI-H06` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-SUI-H07` | Catalogue alignment | P1 | Cross-check against `E2E-MOB7A-001…007`. | Every scenario is covered here and none contradicts the catalogue. | DoD-SES-7 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (58 authored + 9 applicable inherited blocks) | |
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
| Every account and uploaded image created has been removed | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set.** This screen is the commit
point of registration and is shared with the post-sign-up interests **edit**
surface — include [sign-up-visitor.md](sign-up-visitor.md) and the my-interests
sheet in the regression pass.

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
| 1.0 | 2026-08-03 | SIMF Team | First issue. Grounded in `e2e/mobile-sign-up-interests.md` `E2E-MOB7A-001…007` and the D-332 / D-365 decisions. |

---

_Authored:_ 2026-08-03 · _Last reviewed:_ 2026-08-03
