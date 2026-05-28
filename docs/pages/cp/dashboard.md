# Dashboard — `/`

| | |
|--|--|
| **Route** | `/` |
| **Layout** | `CpShellLayout` |
| **Audience** | Any signed-in CP user |
| **Auth** | `[Authorize]` |
| **Pattern** | SimfBanner (D-132) + placeholder welcome card. **Not a list page.** |
| **Status** | ✅ Real (placeholder) |
| **Source** | [`Home.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Home.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Landing page after sign-in. **Placeholder today** — statistics + live
attendance modules are tracked under D-134 (the 22-stub-module build-out).
Currently renders the banner + a welcome panel with a one-line intro string.

## 4. UI

- `<SimfBanner Title="@L[\"Dashboard.Title\"]" />` (swapped from
  SimfPageHeader in D-132).
- `<div class="simf-page-wide"><div class="simf-surface"><h2>{Welcome}</h2>
  <p>{Intro}</p></div></div>`.

## 7. Edge cases

N/A — no data, no actions.

## 10. Use cases

UC-DASH-LAND _(placeholder until D-134 ships the real KPIs)_.

## 11. E2E

| Scenario | ID |
|----------|----|
| Signed-in user lands on `/` after TOTP | E2E-DASH-001 |
| RTL render | E2E-DASH-002 |
| Unauthenticated → redirect to `/login` | E2E-DASH-003 |

## 12. Related

- D-132 (banner swap).
- D-134 (planned KPI / live-attendance modules).

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
