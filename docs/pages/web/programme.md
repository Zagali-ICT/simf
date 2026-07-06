# Website programme / agenda — `/programme`

| | |
|--|--|
| **Route** | `/programme` |
| **Layout** | `MainLayout` |
| **Audience** | Public (anonymous) |
| **Auth** | None — anonymous public read |
| **Status** | ✅ Real (D-199) |
| **Source** | [`Programme.razor`](../../../src/Website/SIMF.Web/Components/Pages/Programme.razor) + [`Programme.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Programme.razor.cs) |
| **Last reviewed** | 2026-07-06 |

## 1. Purpose

The public agenda. A static-SSR read over the anonymous backend: on load it
fetches every published session (`GET /api/v1/app/programme/sessions`) and
groups them by the local calendar date of `StartUtc` into day sections. Each
row shows the bilingual title, hall, the local time window, and — when the
primary theme carries a name — a neutral theme pill. An optional speakers strip
(`GET /api/v1/app/speakers`) is shown when the speakers read returns rows.

## 2. Data flow

- `Programme.razor.cs` → `SimfPublicClient.GetProgrammeSessionsAsync()` →
  `GET /api/v1/app/programme/sessions` → `PublicSessions`.
- `SimfPublicClient.GetSpeakersAsync()` → `GET /api/v1/app/speakers` →
  `PublicSpeakers` (best-effort; a failure leaves the strip empty).
- Both are anonymous public reads (no bearer token) — the same wire contract the
  Flutter app decodes (D-219; the field names/types must not change).

## 3. Bilingual / RTL

Arabic-preferred-then-English fallback per field (`Pick`): in an Arabic UI the
`*Arabic` value is used when present, else the base value. The day heading and
the time window render in `CultureInfo.CurrentUICulture`.

## 7. Edge cases

- **Sessions read fails / service unreachable** → the client returns null → the
  page shows `Programme.Error` and does not fetch the speakers strip.
- **No published sessions** → the `Programme.Empty.Title` empty state.
- **Speakers read fails** → the strip is omitted; the agenda still renders.

## 10. Tests

- bUnit: [`ProgrammePageTests`](../../../tests/SIMF.Web.Tests/ProgrammePageTests.cs)
  — populated agenda + speakers strip, empty state, API-failure error alert.
- API contracts: `ProgrammeSessionsTests`, `PublicSpeakersTests`.

## 11. E2E

[`e2e/web-programme.md`](../../tests/e2e/web-programme.md) — the agenda scenarios.

## Changelog

- 2026-07-06 (D-628) — C# moved to a `Programme.razor.cs` code-behind partial
  (Website clean-code, Phase 5); behaviour unchanged, added bUnit coverage +
  this doc. No wire change.
