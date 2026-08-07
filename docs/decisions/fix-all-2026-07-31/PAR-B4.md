# PAR-B4 — the booth card subtitle duplicates the company name

Item ref: `PAR-B4` (Track D-a, fix-all run 2026-07-30).
Files touched:
`src/Mobile/simf_app/lib/features/booths/widgets/booth_company_header.dart` ·
`test/features/booths/booth_company_header_test.dart` ·
`docs/tests/e2e/mobile-booths.md` · `docs/pages/mobile/booths/README.md`.

## DECISIONS_LOG

### D-NEXT — PAR-B4: the booth card's exhibitor line is skipped when it repeats the short name

`BoothCompanyHeader` rendered the exhibitor (legal) name under the gold short
name whenever it was non-null, with no equality guard. The shipped seed
(`docs/migrations/2026/SIMF_App_SeedGaps.sql:47-49` and the A-02 / B-01 / B-02
rows beside it) inserts `Name` and `ExhibitorName` as the **same** string, so
every seeded booth card printed the company name twice.

**Built (the guard, not the data):** the header resolves the exhibitor line to
null when `exhibitor.trim() == name.trim()`, so the second line is skipped. A
booth with a genuinely distinct trading vs legal name still renders both lines
(SAMI / Saudi Arabian Military Industries), and the whitespace-only variant is
caught by the trim on both sides.

**Why not the data fix.** Re-seeding the four booths with distinct trading names
would clear today's screenshot but leaves the widget unprotected: the next
exhibitor row whose two names match — a company that genuinely trades under its
legal name, which is common — reproduces the defect. The guard is one expression
and holds for every row, so it is the fix; correcting the seed content is a
separate content decision for the client's real exhibitor list.

## PAGE-INDEX

No row change. `/booths` (`#22`) keeps its existing entry — the route, access,
status and both doc links are unchanged; this is a rendering guard inside a
widget the row already points at.

## E2E-README

Replace the `#22 booths` registry row with:

| #22 `booths` (`GET /app/booths` + `/{id}`) — #9: country name + أرشدني→map; PAR-B4 no duplicate company name | [`mobile-booths.md`](mobile-booths.md) | E2E-MOB022-001..015 |

(The range widens from `001..013` to `001..015`: the file already carried `-014`
for DEF-LGO-002, and this run adds `-015`. The second `boothMap` row pointing at
`E2E-MOB022-013` is unchanged.)
