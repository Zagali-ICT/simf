# Common case blocks — inherited by every screen sheet

> **Why this file exists.** Around fifteen cases are identical on every screen.
> Repeating them in ~200 sheets would produce ~3,000 duplicated rows that drift
> apart within a month. They are authored **once** here and inherited by
> reference: each sheet's §3 lists the blocks that apply and records one status
> per block. A failure **inside** a block still gets its own row in that sheet's
> §6 ledger, quoting the block id (e.g. `CB-02.3`).
>
> **Authority.** Subordinate to [`SIMF-TST-001-Test-Plan.md`](../SIMF-TST-001-Test-Plan.md).
> Where the two disagree, `SIMF-TST-001` wins.

**Status vocabulary:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

---

## CB-01 — Render, console and overflow

Applies to: every screen.

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-01.1 | Screen reaches first paint from a cold start | P0 | The screen renders its real content (not a spinner that never resolves) within a reasonable time on the target device. | DoD-Gate-4 |
| CB-01.2 | Console / log is clean | P0 | **Zero** errors in the browser console or the device log while opening and using the screen. A logged exception is a defect even if the UI looks correct. | DoD-Gate-4 |
| CB-01.3 | No horizontal overflow | P1 | `scrollWidth == clientWidth` on web; no sideways scroll or clipped content on device. Nothing is cut off at the screen edge. | DoD-Gate-4 |
| CB-01.4 | No broken images or assets | P1 | Every image renders; no placeholder / broken-image icon; no 404 in the network list. | DoD-Gate-4 |
| CB-01.5 | Responsive width | P1 | Content fills the available width and respects the standard padding. Verify on a phone, a tablet and (web/CP) a narrow desktop window. No fixed-width layout that leaves dead space or clips on a small screen. | — |
| CB-01.6 | Layout matches the approved design | P2 | The screen matches its Figma frame / mockup: spacing, type scale, colour roles, component shapes. | DoD-Gate-4 |

## CB-02 — Arabic RTL and English LTR

Applies to: every screen. **Run the whole sheet twice, once per language.**

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-02.1 | Arabic renders right-to-left | P0 | Text direction, alignment and reading order are RTL. Panels, drawers and list affordances swap sides. | NFR (localisation) |
| CB-02.2 | Chevrons, arrows and back affordances mirror **once** | P1 | Directional icons point the correct way in Arabic. Watch for the double-mirror fault (an icon mirrored by the framework **and** by the code, ending up pointing the wrong way again). | — |
| CB-02.3 | No hardcoded string | P0 | Every visible label, button, hint, empty state, toast and error is translated. An English word inside an Arabic run (or vice versa) is a defect. | SIMF-TST-001 §11 |
| CB-02.4 | No text truncation or tofu | P1 | Arabic text is not clipped, ellipsised mid-word, or rendered as `□□□` (missing glyph). Long Arabic labels wrap rather than overflow. | — |
| CB-02.5 | Numbers, dates and times | P1 | Dates and times display in the project's agreed local format (Saudi local time, 12-hour) — no raw UTC and no ISO timestamp leaking into the UI. | — |
| CB-02.6 | Language switch is not destructive | P2 | Switching language mid-screen keeps the user in place and does not lose entered form data or reset a list's scroll position. | — |

## CB-03 — Loading, empty and error states

Applies to: any screen that loads data.

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-03.1 | Loading state | P1 | A loading indicator or skeleton appears while data is in flight — never a blank screen and never stale content presented as current. | — |
| CB-03.2 | Empty state | P1 | With zero rows the screen shows its proper empty state with explanatory text — not an empty white area and not an error. | — |
| CB-03.3 | Error state | P1 | On a failed load the screen shows its error state with a retry affordance. Retry actually re-issues the request and recovers. | — |
| CB-03.4 | Partial data | P2 | Rows with missing optional fields (no photo, no description, no logo) render with their fallback rather than a crash or an empty gap. | — |

## CB-04 — Auth gate and account state

Applies to: every screen. Adjust the expected audience per sheet.

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-04.1 | Signed-out access | P0 | A guest-allowed screen renders read-only content. A signed-in-only screen redirects to the sign-in surface and does **not** flash protected content first. | A1 |
| CB-04.2 | Insufficient app privilege | P0 | Guest = 0, Visitor = 1, Moderator = 2, Staff = 3. A user below the screen's required privilege cannot reach it: the entry point is hidden **and** the direct route is refused. | A1 |
| CB-04.3 | Server enforces the same gate | P0 | Call the screen's endpoint(s) directly with a token that lacks the privilege → **403**. Hiding a button in the client is not access control. | A1-12 |
| CB-04.4 | Account state gates | P0 | `Registered` (email unverified), `PendingApproval`, `Rejected` and `Disabled` accounts each land on their own state screen and reach **no** protected content. | FR-1xx |
| CB-04.5 | Denied access is audited | P1 | A refused request writes an audit row identifying the actor, the resource and the decision. | A1-12, A9-7 |

## CB-05 — Session expiry and token refresh

Applies to: every signed-in screen.

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-05.1 | Silent refresh across the access-token lifetime | P0 | The access token lives **5 minutes**. Staying on the screen past that boundary refreshes silently — no visible error, no forced re-login, no burst of duplicate refresh calls (single-flight). | A7 |
| CB-05.2 | Absolute session cap | P1 | The absolute session is **24 hours** and activity does **not** slide it. At the cap the user is signed out and must sign in again. | A7 |
| CB-05.3 | Expiry during an in-flight action | P1 | If the session dies mid-action the user is signed out cleanly — no half-saved record, no silent data loss, no infinite spinner. | A7 |
| CB-05.4 | Sign-out revokes the session | P0 | After sign-out the previous tokens are refused. Re-using a captured refresh token fails. | A7 |
| CB-05.5 | No session identifier in a URL | P1 | No token, session id or code appears in any URL, deep link or log line. | A7-36 |

## CB-06 — Network failure and retry

Applies to: any screen that calls the API.

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-06.1 | Offline load | P1 | With the network off the screen shows a clear offline / failed state and a retry — never a silent success and never a permanent spinner. | — |
| CB-06.2 | Offline write | P0 | Submitting with no network surfaces a failure. It must **never** report success for a write the server did not accept. | — |
| CB-06.3 | Recovery | P1 | Restoring the network and retrying completes the action correctly, exactly once. | — |
| CB-06.4 | Slow network | P2 | On a throttled connection the screen stays usable: the loading state holds, controls do not duplicate the request, nothing times out into a blank screen. | — |
| CB-06.5 | Double submit | P0 | Tapping a submit control twice quickly creates **one** record, not two. The control disables while the request is in flight. | A4-10 |

## CB-07 — Server 500 and malformed payload

Applies to: any screen that calls the API.

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-07.1 | Server 500 | P1 | A 500 surfaces the localized fallback message. The app does not crash, does not white-screen, and stays navigable. | A9 |
| CB-07.2 | No stack trace or internal detail reaches the user | P0 | No exception text, SQL, file path, framework name or internal id is shown to the user or written into a client-visible payload. | A9 |
| CB-07.3 | Unexpected / malformed response | P1 | A response with a missing or unexpected field degrades gracefully rather than crashing the screen. | A9 |
| CB-07.4 | Error envelope is consistent | P1 | Failures return the standard `ApiResult` error envelope with a stable machine-readable error code — not a bare string or an HTML error page. | SIMF-API-001 |

## CB-08 — Accessibility baseline

Applies to: every screen.

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-08.1 | Every interactive control is reachable and labelled | P1 | Buttons, links, inputs and tappable cards carry an accessible name. An icon-only control has a label, not just a glyph. | SIMF-TST-001 §11 |
| CB-08.2 | Keyboard / focus order (web + CP) | P1 | The screen is completable by keyboard alone; focus order follows the visual order; focus is always visible; no keyboard trap. | SIMF-TST-001 §11 |
| CB-08.3 | Screen reader | P2 | With the screen reader on, headings, fields, errors and state changes are announced meaningfully. | SIMF-MAA-001 §14 |
| CB-08.4 | Colour is never the only signal | P1 | Status, validity and selection are conveyed by text or icon as well as colour. | SIMF-CPD-001 §14 |
| CB-08.5 | Text scaling | P2 | At the largest supported font size and with the app's accessibility settings on, the layout still works — no clipped or overlapping text. | — |
| CB-08.6 | Contrast | P2 | Text and essential icons meet the contrast baseline against their background in both themes. | — |

## CB-09 — Pull-to-refresh (mobile data screens)

Applies to: every mobile screen that displays server data. **Owner rule.**

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-09.1 | Pull-to-refresh exists | P1 | Pulling down re-fetches from the server. A data screen with no pull-to-refresh is a defect. | Owner rule |
| CB-09.2 | Refresh reflects a server-side change | P1 | Change a row in the Control Panel, pull to refresh in the app → the new value appears without restarting the app. | — |
| CB-09.3 | Refresh on empty and error states | P1 | Pull-to-refresh works while the screen shows its empty or error state, not only when a list is populated. | — |
| CB-09.4 | Refresh does not duplicate | P2 | Repeated pulls do not stack duplicate rows or fire overlapping requests. | — |

## CB-10 — Audit trail

Applies to: any screen that writes, approves, scans or authenticates.

| ID | Check | Pri | Expected result | Refs |
|---|---|---|---|---|
| CB-10.1 | Successful write is audited | P1 | The action writes an audit row naming the actor, the action, the target and the timestamp. | A9-7 |
| CB-10.2 | Failed / refused action is audited | P1 | A refusal (validation, permission, rate limit) is recorded, not silently dropped. | A1-12, A9-15 |
| CB-10.3 | Audit content is safe | P1 | The audit row carries no password, token, OTP or other secret. | A9-9 |
| CB-10.4 | Audit is not user-editable | P0 | No screen or endpoint lets a user alter or delete an audit row. | A8 |

---

## How a tester records these

In each screen sheet, §3 has one row per block. Record `PASS` if every check in
the block passed on that screen. If any check failed:

1. Set the block to `FAIL`.
2. Add a row to that sheet's §6 ledger with the specific check id in the Case ID
   column (e.g. `CB-02.3`) and the usual defect / developer / fix / re-test
   columns.
3. Note in the block's Notes cell which check failed, so the sheet reads
   correctly at a glance.

---

_Authored:_ 2026-08-01 · _Last reviewed:_ 2026-08-01
