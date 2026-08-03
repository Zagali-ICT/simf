# Test-Case Sheet — `Onboarding` (app screen #2)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | التعريف بالتطبيق · Onboarding | **Doc id** | `TC-MOB-ONB` |
| **Route / screen id** | app screen **#2** `onboarding` — first-run carousel | **Surface** | Mobile app (Flutter) |
| **API under test** | none — the screen is fully local | **Audience** | Guest (first run only) |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill — include a Huawei / no-GMS handset for the video decoder path)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/onboarding/](../../pages/mobile/onboarding/README.md) · [e2e/mobile-onboarding.md](../../tests/e2e/mobile-onboarding.md) · Figma `148:22` · D-636 (clean-code freeze) · DEF-ONB-004 / DEF-ONB-006 | | |

### Source of the expected values on this sheet

| Value | Source |
|---|---|
| **3-step** first-run carousel | `PAGE-INDEX.md` #2 |
| **One** looping hero clip on **one** decoder, plus a world-map poster fallback and a scrim | `PAGE-INDEX.md` #2 (DEF-ONB-004 / DEF-ONB-006) |
| Per-step title and body, brand mark, page dots, **skip** and **next** actions | same |
| Figma node `148:22` | `FIGMA-NODE-MAP.md` |

> **The single-decoder rule is the point of DEF-ONB-004/006.** Instantiating one
> video decoder per step exhausts the hardware decoder on constrained devices —
> especially HiSilicon handsets — and the hero goes black. The `A02` and `D02`
> rows below exist specifically to catch a regression back to per-step decoders.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** First run | app freshly installed; onboarding has never been completed |
| **FX-2** Completed before | onboarding already dismissed once |
| **FX-3** Constrained device | a **HiSilicon / Huawei no-GMS** handset — the decoder-pressure case |
| **FX-4** Low-end device | the slowest handset in the matrix |
| Reset | Reinstall (or clear app data) to return to **FX-1**. Record how you reset between runs. |

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | partial — the hero has a poster fallback | | |
| CB-04 Auth gate and account state | partial — guest-only surface | | |
| CB-05 Session expiry and token refresh | **N-A** — no session | | |
| CB-06 Network failure and retry | partial — the screen is local; confirm it needs no network | | |
| CB-07 Server 500 and malformed payload | **N-A** — no API call | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A** | | |
| CB-10 Audit trail | **N-A** — no write action | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-ONB-A01` | Screen chrome | P1 | Launch as **FX-1**. | Full-bleed hero behind a scrim; brand mark; step title and body; **three** page dots with the first active; **skip** and **next** actions. | Figma `148:22` | | | | |
| `TC-MOB-ONB-A02` | **One decoder, one clip** | P0 | Step through all three steps, then back, then forward again. Watch the hero throughout. | The **same single** looping clip plays continuously across all three steps. The hero **never** goes black, never restarts from frame zero on each step, and no second video is instantiated. | DEF-ONB-004 / DEF-ONB-006 | | | | |
| `TC-MOB-ONB-A03` | Poster fallback | P0 | Run on **FX-3**, and on a device where the clip cannot decode. | The world-map **poster** renders in place of the clip. The screen stays fully usable and legible — a failed video must never leave a black or empty hero. | DEF-ONB-004 | | | | |
| `TC-MOB-ONB-A04` | Scrim legibility | P1 | Read the title and body on every step against the moving hero. | The scrim keeps the text legible at every frame of the clip. Check the brightest frame specifically. | Figma `148:22` | | | | |
| `TC-MOB-ONB-A05` | Dots reflect the step | P1 | Move through the steps forward and backward. | The active dot always matches the visible step. | — | | | | |
| `TC-MOB-ONB-A06` | Tablet | P1 | Run on a tablet in portrait. | The hero fills the frame without letterboxing or dead gutters; text and actions stay proportionate. | responsive rule §13.7 | | | | |
| `TC-MOB-ONB-A07` | Small phone | P1 | Run on the smallest handset in the matrix. | No text clipping, no overlapping actions, no horizontal overflow. | CB-01.3 | | | | |
| `TC-MOB-ONB-A08` | Orientation | P1 | Rotate the device. | Portrait-locked; the screen does not rotate or distort. | `main.dart` portrait lock | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-ONB-C01` | Next through all steps | P0 | Tap **next** on step 1, then 2, then 3. | Each tap advances one step. On the final step the action completes onboarding and routes to **sign-in** (#3). | PAGE-INDEX #2→#3 | | | | |
| `TC-MOB-ONB-C02` | Swipe navigation | P1 | Swipe forward and backward through the steps. | Swiping moves between steps and stays in sync with the dots and the actions. In **Arabic** the swipe direction is mirrored correctly. | CB-02.2 | | | | |
| `TC-MOB-ONB-C03` | Skip | P0 | Tap **skip** on step 1. | Onboarding is dismissed immediately and the app routes to sign-in. | — | | | | |
| `TC-MOB-ONB-C04` | Shown only once | P0 | Complete onboarding by **next**, then force-quit and relaunch. | Onboarding does **not** reappear — the app goes straight to sign-in. | `TC-MOB-SPL-C02` | | | | |
| `TC-MOB-ONB-C05` | Skip also marks it complete | P0 | Dismiss by **skip**, then force-quit and relaunch. | Onboarding does **not** reappear. Skipping must persist the same flag as completing. | — | | | | |
| `TC-MOB-ONB-C06` | Back gesture on step 1 | P1 | On step 1, use the system back gesture / button. | Behaviour is defined and sane — either it does nothing or it exits the app. It must **not** land on a blank screen or a partially-built route. | — | | | | |
| `TC-MOB-ONB-C07` | Back through the steps | P1 | Advance to step 3, then use back twice. | Returns step by step to step 1 with the dots and content in sync. | — | | | | |
| `TC-MOB-ONB-C08` | Double-tap next | P1 | Tap **next** twice rapidly on step 1. | Advances **one** step, not two, and does not skip past the final step into a duplicate route. | CB-06.5 | | | | |
| `TC-MOB-ONB-C09` | Backgrounding mid-carousel | P1 | Background the app on step 2, return after a minute. | The app returns to step 2 with the hero playing. No crash, no black hero, no reset to step 1. | — | | | | |

### D. Resilience and device behaviour

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-ONB-D01` | Fully offline | P0 | Airplane mode, then launch as **FX-1**. | Onboarding renders and completes normally — the hero and copy are bundled, so the screen must not depend on the network. | CB-06.1 | | | | |
| `TC-MOB-ONB-D02` | **Decoder pressure** | P0 | On **FX-3**, open another video app, then launch onboarding and step through it. | The hero either plays or falls back to the poster. It **never** produces a black frame, a codec error dialog, or a crash. | DEF-ONB-006; hero-video decoder findings | | | | |
| `TC-MOB-ONB-D03` | Low-end device | P1 | Run the whole carousel on **FX-4**. | Steps advance without stutter that makes the actions unusable. Record the observed frame behaviour. | CB-01 | | | | |
| `TC-MOB-ONB-D04` | Battery saver / reduced motion | P1 | Enable the OS battery saver and any reduced-motion setting, then run the carousel. | The screen stays usable. If the clip is suppressed, the poster renders in its place. | CB-08 | | | | |
| `TC-MOB-ONB-D05` | Audio | P0 | Run the carousel with the device volume up. | The hero clip is **silent** — an onboarding video must not play audio unprompted. | — | | | | |
| `TC-MOB-ONB-D06` | Memory | P1 | Step back and forth through the carousel twenty times. | No memory growth that leads to a crash; no decoder leak. Record the memory profile if the tooling is available. | DEF-ONB-004 | | | | |
| `TC-MOB-ONB-D07` | No console error | P0 | Capture the device log across a full run. | **Zero** errors, including decoder warnings that indicate a failed instantiation. | CB-01.2 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-ONB-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | Text, dots and actions mirror. The **swipe direction** and the next/skip positions follow RTL convention. Watch for a double-mirrored chevron. | CB-02.1, CB-02.2 | | | | |
| `TC-MOB-ONB-F02` | No hardcoded string | P0 | Read all three steps in each language. | Every title, body, dot label, **skip** and **next** is translated. No English word inside the Arabic run. | CB-02.3 | | | | |
| `TC-MOB-ONB-F03` | Arabic text fits | P1 | Read all three steps in Arabic. | Arabic copy wraps rather than clipping or ellipsising mid-word; no missing glyphs. | CB-02.4 | | | | |
| `TC-MOB-ONB-F04` | Accessible names | P1 | Screen reader on; traverse each step. | The step title and body are announced; **skip** and **next** carry labels; the dots announce the position (for example "page 2 of 3"). | CB-08.1 | | | | |
| `TC-MOB-ONB-F05` | Text scaling | P2 | Largest supported font size. | Copy wraps and the actions stay reachable on every step. | CB-08.5 | | | | |
| `TC-MOB-ONB-F06` | Contrast over video | P1 | Check text contrast against the brightest frame of the clip. | Text meets the contrast baseline at every frame, not only against the poster. | CB-08.6 | | | | |
| `TC-MOB-ONB-F07` | Motion is not the only signal | P2 | Observe the step change. | The step change is conveyed by the dots and the content, not only by an animation. | CB-08.4 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-ONB-H01` | First-run introduction works | P0 | Run C01 → C05. | A new user is introduced in three steps, can skip, and is never shown it again. | — | | | | |
| `TC-MOB-ONB-H02` | The hero never breaks the screen | P0 | Run A02, A03, D01, D02. | On every device class the hero either plays or falls back cleanly. | DEF-ONB-004 / DEF-ONB-006 | | | | |
| `TC-MOB-ONB-H03` | Design parity | P1 | Compare the live render against Figma `148:22`. | Strings, typography, colour, spacing and the scrim match. Record any deliberate deviation. | DoD-Gate-4 | | | | |
| `TC-MOB-ONB-H04` | Live-render gate | P0 | Capture a screenshot per step and the device log for a full run. | Screenshots captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-ONB-H05` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-ONB-H06` | Device matrix | P0 | Run C01 and D02 on a standard phone, a **HiSilicon / Huawei no-GMS** handset, a low-end phone and a tablet. | Behaviour is correct on every class. | SIMF-MAA-001 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (37 authored + 5 applicable inherited blocks) | |
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
| Device matrix completed (incl. HiSilicon / no-GMS) | | |
| Evidence captured for every PASS and FAIL | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set.** A change to the hero-video
handling also affects the app's other video surfaces — include the home hero and
the live-broadcast screen in the regression pass.

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
| 1.0 | 2026-08-01 | SIMF Team | First issue. Grounded in `PAGE-INDEX.md` #2, D-636 and DEF-ONB-004 / DEF-ONB-006. |

---

_Authored:_ 2026-08-01 · _Last reviewed:_ 2026-08-01
