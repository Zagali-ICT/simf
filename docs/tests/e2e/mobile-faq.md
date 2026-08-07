# E2E test catalogue — `FAQ` (`faq`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> data-driven from the **public** `GET /app/faq` (anonymous), a read-only
> projection of the D-211 FAQ tables (`FaqGroup` → `FaqEntry`). Built to KSA
> Figma frame **`1388:7567`** — an accordion of question/answer cards. Tested in
> `src/Mobile/simf_app/test/features/faq/faq_screen_test.dart` +
> `faq_models_test.dart`; backend in `tests/SIMF.Api.Tests/FaqTests.cs`
> (`Public_faq_returns_active_groups_with_their_entries_anonymously`,
> `Public_faq_hides_empty_and_deactivated_groups`). Previously a ComingSoon
> placeholder (D-464).

| | |
|--|--|
| **Page** | app screen #201 `faq` |
| **Route** | `/faq` (`GET /app/faq`) |
| **Surface** | Mobile (Flutter) |
| **Figma** | `1388:7567` |
| **Auth setup** | **None** — `GET /app/faq` is `AllowAnonymous` (public content, like the organization profile). |
| **Last reviewed** | 2026-06-26 |

## Layout

- **Header**: back chevron + centred title **الأسئلة الشائعة**.
- **Accordion**: one card per active entry (navy-deep, beige hairline) — the
  question (white) with a gold expand/collapse chevron; tapping reveals the
  answer (muted `#C2B8A2`) below a hairline divider.
- Group names render as section headers **only** when more than one active group
  exists (single-group catalogues show the flat accordion of the design).
- **States**: spinner while loading; retry surface on a wire error; an empty
  message (`No frequently asked questions yet.`) when no active entries.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB201-001 | Questions render collapsed; tapping one reveals its answer | happy | P0 | authored ✓ (screen `renders the questions collapsed, then expands on tap`) |
| E2E-MOB201-002 | Empty catalogue → empty message | empty | P1 | authored ✓ (screen `shows the empty state when there are no entries`) |
| E2E-MOB201-003 | Wire failure → error + retry | error | P1 | authored ✓ (screen `shows the error state with retry on a wire failure`) |
| E2E-MOB201-004 | `GET /app/faq` returns active groups + entries anonymously | happy | P0 | authored ✓ (API `Public_faq_returns_active_groups_with_their_entries_anonymously`) |
| E2E-MOB201-005 | Deactivated / empty groups are hidden from the public read | data | P1 | authored ✓ (API `Public_faq_hides_empty_and_deactivated_groups`) |
| E2E-MOB201-006 | RTL — Arabic question/answer text from the same entry | rtl | P2 | covered (models `localized*` getters; `isArabic` path) |
| E2E-MOB201-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB201-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

```gherkin
Feature: FAQ accordion (public, Figma 1388:7567, GET /app/faq)

Scenario: Expanding a question reveals its answer
  Given the FAQ catalogue has the question "How do I register for the forum?"
  When the user opens /faq
  Then the question is shown with its answer hidden
  When the user taps the question
  Then its answer "Register on the official website." is shown

Scenario: Empty catalogue
  Given no active FAQ entries exist
  When the user opens /faq
  Then the screen shows "No frequently asked questions yet."

Scenario: The read fails
  Given GET /app/faq returns a network error
  When the user opens /faq
  Then an error message with a Retry button is shown
  And tapping Retry refetches the FAQ

Scenario: The public read is anonymous and active-only
  Given an admin created a FAQ group with one entry
  And a second group was created with an entry then deactivated
  And a third group was created with no entries
  When an anonymous client GETs /api/v1/app/faq
  Then it returns 200 with the first group and its entry
  And neither the deactivated group nor the empty group is present
```

**Evidence:** screen tests (3) + models test (3 — parse + nested entries +
language fallback); API tests (2 — anonymous active read + hidden
empty/deactivated groups).

---

_Last reviewed:_ `2026-06-26` by `SIMF Team`.
