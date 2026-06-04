# Page 011 — Design (حالة التسجيل · Registration status)

Flutter screen design — layout, components, RTL, states and localization. The user
flow is in [Page_011_Function.md](Page_011_Function.md); the rules are in
[Page_011_Logic.md](Page_011_Logic.md); the contract is in
[Page_011_API.md](Page_011_API.md).

## Screen identity
| | |
|---|---|
| Route | `RouteNames.registrationStatus` → `/registration/status` |
| Titles | AR **حالة التسجيل** · EN **Registration status** |
| Mockup | page **011** (owner) |
| Nature | Full-screen, single-column status / waiting screen |

## Layout (top → bottom)
| Region | Content |
|--------|---------|
| App bar / header | Screen title (bilingual) + optional sign-out affordance |
| Hero / illustration | Status icon or illustration matching the current state |
| Headline | Bilingual status title for the current state |
| Message | One short explanatory line per state |
| Stages tracker (المراحل · Stages) | Static four-step progress tracker — 1) إرسال البيانات (Data submitted) · 2) تأكيد البريد الإلكتروني (Email confirmed) · 3) مراجعة فريق SIMF (SIMF team review) · 4) تفعيل الحساب (Account activation). Reflects the registration stages; the current step is driven by `registrationStatus` (step 3 review is current while `Pending`, all four complete on `Approved`). |
| Reference block | Approval **reference number + date** — **decoration only (D11)**, static |
| Primary action | State-dependent button: **Re-check** (pending) / **Continue** (approved) |
| Footer | Sign-out link |

Single centered column, generous vertical spacing, one primary action at a time. No
inline styles or hardcoded colors — use the app theme tokens.

## Components
| Component | Use |
|-----------|-----|
| Status illustration/icon | One per state (pending / approved / rejected / error) |
| Headline text | Localized title, bound to current state |
| Body text | Localized message, bound to current state |
| Stages tracker (المراحل · Stages) | Static four-row progress tracker for the registration stages (Data submitted · Email confirmed · SIMF team review · Account activation), bilingual; the current step follows `registrationStatus` (review while Pending, all complete on Approved) |
| Primary button | Re-check (pending) or Continue (approved) |
| Loading indicator | Shown during the `GET /app/users/me` call |
| Retry affordance | Shown in the Error state |
| Sign-out link | Always available |

## States
| State | Trigger | Visuals | Actions |
|-------|---------|---------|---------|
| **Loading** | first open + every Re-check in flight | spinner / skeleton; primary button disabled | none (busy) |
| **Pending** | `registrationStatus = Pending` | hourglass / waiting illustration; "under review" headline + message | **Re-check**, Sign out |
| **Approved** | `registrationStatus = Approved` | check / success illustration; "approved" headline | **Continue**, Sign out |
| **Rejected** | `registrationStatus = Rejected` | cross / declined illustration; "not approved" headline + recovery copy | Sign out (+ design-specified recovery) |
| **Error** | call failed / 401 / unknown value | neutral error illustration; "couldn't load status" message | **Retry**; on 401 → route to sign-in |
| **Empty** | n/a | This screen has **no list/collection**, so there is no empty state — only the status states above. |

## RTL & localization
- Fully bilingual; **AR is RTL**, EN is LTR. The whole layout mirrors under RTL
  (icon/text alignment, button placement, chevrons).
- All copy comes from localization resources (AR + EN) — no hardcoded strings.
- Status icon and color follow the state, sourced from theme tokens (no raw hex).
- Numerals / dates in the decoration block follow the app's locale formatting.

## Decoration note (D11)
The approval **reference number + date** are **layout decoration only** — never bound
to live API data (see [Page_011_Logic.md](Page_011_Logic.md) L-5). If present, render
them as static placeholders; their absence is never an error.

## Acceptance (design)
- Each `registrationStatus` value renders its matching state with the correct
  illustration, headline, message and single primary action.
- Loading is visible during every fetch; the primary action is disabled while busy.
- Error and 401 paths are distinct (retry vs. sign-in), never a fake Pending.
- The screen mirrors correctly in Arabic RTL with localized copy throughout.
