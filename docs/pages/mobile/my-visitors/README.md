# My Booth Visitors — زوار جناحي (`myVisitors`, D-426)

- **Route:** `/exhibitor/visitors` (`RouteNames.myVisitors`). Access:
  **Exhibitor (approved) with a current booth membership** — DEF-EXH-001: the
  server authorises on `ProfileType.MobileAppRole == Exhibitor` (D-519), so
  Staff / Moderator / Media / Sponsor / plain Visitor callers all get 403 → the
  forbidden surface. DEF-EXH-006: an active `ExhibitorMembership` of an active
  `Exhibitor` is required alongside the role, so a former officer can no longer
  read back the contact cards of the booth they left. Reached from the side
  drawer (Other-only) and after a successful visitor-badge scan.
  **Exhibitor (approved, non-visitor)** — a visitor-tier caller gets 403 → the
  forbidden surface. Reached from the side drawer (Other-only), the exhibitor
  home's "Exhibitor tools" tile row, and after a successful visitor-badge scan.
- **API:** `GET /app/exhibitor/visitors` (`ExhibitorRepository.listMyVisitors`;
  the path is `ExhibitorEndpoints.visitors` — an earlier draft of this line said
  `/app/exhibitor/my-visitors`, which the app has never called).
  DEF-EXH-004: the capture-time SUBJECT test runs here too, so a row whose
  subject has since been DEACTIVATED drops out of the list instead of projecting
  a live card. **D-780 (owner decision 2026-07-27 — "can scan all badges"):** that
  shared test is now simply "an ACTIVE profile", reversing the DEF-EXH-003
  audience-side narrowing — a media / sponsor / staff / fellow-exhibitor capture
  is a legitimate lead and DOES list.
- **API:** `GET /app/exhibitor/visitors` (`ExhibitorRepository.listMyVisitors`),
  `DELETE /app/exhibitor/visitors/{id}` (`removeVisitor`) and
  `GET /app/exhibitor/visitors/{id}/vcard` (`getVcard`).
  DEF-EXH-004: the capture-time SUBJECT test runs here too, so a row captured
  while the old rule was in force (a staff / rival-exhibitor / since-deactivated
  subject) drops out of the list instead of projecting a live card — and the
  vCard export runs the SAME test, so it is not a second door onto a card the
  list refuses.
- **Figma:** none — this is a D-426 functional page, not a KSA design frame.
  **Clean-code freeze:** D-642 (2026-07-04).

## Purpose

The exhibitor's captured visitors — everyone they scanned at their booth,
newest first, each rendered as the shared `ContactCard` with the visitor's full
card resolved live (name / job / organisation / country / email / mobiles, or
the "no longer available" state when the visitor has hidden their card).

## Not "My Contacts" (BUG-025, 2026-07-26)

This list and **My Contacts** (`/contacts`, `myContacts`) are two different
features and are deliberately **not** merged:

| | My Booth Visitors (this page) | My Contacts |
|--|--|--|
| Filled by | the exhibitor scanning a visitor's **entry badge** at their booth | visitor-to-visitor **card sharing** (a share token) |
| Backend | `ExhibitorVisitorScan` → `GET /app/exhibitor/visitors` | `SavedContact` → `GET /app/contacts` |
| Who can use it | Exhibitor tier only (403 otherwise) | any approved visitor |
| Side effect | a new capture emails the lead to the exhibitor (BUG-024) | none |

Merging them needs an **owner ruling** (see `docs/decisions/DECISIONS_LOG.md`
D-771). Until then the distinction is made unmistakable in the UI: the title
names the booth (زوار جناحي / My Booth Visitors) and the first row of the list
is a shared `SimfPageNote` reading "بطاقات الزوار التي مسحتها في جناحك. قائمة
منفصلة عن «جهات اتصالي»." / "Badges you scanned at your booth. This list is
separate from My Contacts."

## The list belongs to the BOOTH, not the officer (FR-EXH-003, 2026-07-27)

`ExhibitorVisitorScan` carried no exhibitor reference, so a captured lead
belonged to the **person** who scanned it: two officers of the same booth kept
two disjoint lead lists and neither could see the other's captures. A lead is
the exhibiting company's, so the row now carries an **additive nullable
`ExhibitorId`** (a real FK — both tables live on the App DB) and every read is
scoped by the caller's active `ExhibitorMembership`.

- **Idempotency moved with it.** A repeat scan is deduplicated per **(booth,
  visitor)**, so a colleague re-scanning the same visitor updates the booth's one
  lead (and refreshes its note) instead of forking a private second copy. The
  subject is notified once, for the first capture.
- **Existing rows.** Migration
  `App/20260727045650_FRExh003_AddExhibitorVisitorScanExhibitorId` backfills the
  column from the capturing user's **oldest ACTIVE** `ExhibitorMembership` — the
  same tie-break `EnsureExhibitorAsync` uses. A row whose capturer has **no**
  active membership at migration time has no booth to resolve and is deliberately
  left **NULL** rather than guessed at: it stays visible to its capturer alone
  (the person-scoped fallback the service keeps), and the first re-scan of that
  visitor adopts it into the scanning officer's booth.
- The FK is `Restrict`, never `Cascade`, so closing a booth cannot silently
  delete its lead history — the rule `ExhibitorMembership` already follows.
- **The uniqueness invariant moved with the ownership.** D-611's
  `(ExhibitorUserId, VisitorUserId)` unique-over-active index encoded "a lead
  belongs to the officer"; it is demoted to a plain index (it still serves the
  legacy-null fallback lookup) and a filtered unique
  `(ExhibitorId, VisitorUserId)` takes its place. Keeping the old one would have
  **500'd** the legitimate case of an officer who transfers to another booth and
  captures a visitor they had already captured for their previous one — the old
  booth rightly keeps its lead, so a second row has to exist. The migration
  collapses rows the old rule allowed to fork (two officers of one booth each
  holding their own capture of the same visitor): newest kept, the rest
  soft-deleted, so the index can build and nothing is destroyed.

## Remove + export a captured lead (FR-EXH-002, 2026-07-27)

My Contacts has had **both** a remove and a vCard export since D-286; the lead
list had **neither**, so a mis-scan (or a lead the visitor asked to be dropped)
was permanent and the card could only be read on screen.

- Tapping a row opens **`CapturedVisitorSheet`**, mirroring `SavedContactSheet`
  (same layout, same confirm-then-pop contract) so the two card lists behave
  identically.
- **Remove** confirms first (`SimfConfirmDialog`, destructive), then
  soft-deletes (`BaseAuditEntity.Deactivate` — the project convention) and writes
  an `Exhibitor.LeadRemoved` audit entry: the capture carries the visitor's
  consent trail, so its removal has to be attributable too. Idempotent — a
  repeat delete is a 200, not a 404. A rival booth's officer cannot remove it.
- **Export vCard** renders through the shared `VisitorCardVCard`, the *same*
  implementation My Contacts uses, so the two exports can never drift; only the
  download filename differs (`simf-lead.vcf`). It is hidden when the subject's
  card is unavailable — there is nothing to export.

## Structure

| File | Holds |
|------|-------|
| `exhibitor/my_visitors_screen.dart` (25) | `MyVisitorsScreen` (`ConsumerWidget`) — the `SimfPageShell` (title + `backOrHome`) and nothing else. |
| `exhibitor/widgets/my_visitors_body.dart` (`MyVisitorsBody`) | The `myVisitorsProvider` loading / forbidden / error / empty / list dispatch. Both failure branches stay refreshable, the 403 especially: an exhibitor whose booth link lands after the first load would otherwise be stuck on it. |
| `exhibitor/widgets/my_visitors_list.dart` (`MyVisitorsList`) | The pull-to-refresh list — the BUG-025 `SimfPageNote` then the shared `ContactCard`s, each tappable into the detail sheet; a sheet that pops `true` toasts and invalidates the list. |
| `exhibitor/widgets/exhibitor_centered.dart` (`ExhibitorCentered`) | The text-only centred message serving both the empty and the forbidden surfaces. |
| `exhibitor/widgets/captured_visitor_sheet.dart` | `CapturedVisitorSheet` — the lead's full card + **Export vCard** / **Remove** (FR-EXH-002). Pops `true` on a confirmed removal so the list reloads and toasts. |
| `exhibitor/data/exhibitor_models.dart` · `exhibitor_repository.dart` | `ExhibitorVisitor` + the repo (already split). |

## Clean-code freeze (D-642)

The screen was already small (139 lines) with its data layer in `data/` and its
error branch already on the shared `SimfErrorState`. The one deviation was a raw
`RefreshIndicator` — swapped for the app-wide branded **`SimfPullToRefresh`**
(the accent/navy-deep spinner, D-520/D-532); the resting render is unchanged
(the spinner colour only shows during an active pull). `_Centered` is **kept
local** — it is a text-only centred message (no icon), so neither `SimfEmptyState`
(icon) nor `SimfErrorState` (retry button) fits; it serves both the empty and
the forbidden surfaces. Already fully tokenised.

It is no longer *in the screen*, though: it is the public `ExhibitorCentered` in
`widgets/exhibitor_centered.dart`, alongside `MyVisitorsBody` and
`MyVisitorsList`, since no `_Private` widget class may live in a screen
(`tool/conventions` SIMF-C3). The call above stands — it is still feature-local,
not one of the shared states.

## L4 render-lock (no Figma frame)

Captured `my_visitors.png` (@375×812, ar, two captured visitors — one available
with full details, one unavailable) and **read it** — the زوار جناحي header (and,
since BUG-025, the explanatory note row beneath it), the two
`ContactCard`s (gold avatar + job/org/country/email/mobile with gold RTL icons;
the second showing هذه الجهة لم تعد متاحة), the bottom nav. RTL, no tofu. No
Figma frame is bound, so this is a structural render-lock, not a parity claim.

## Level-F

- **Note** — the BUG-025 `SimfPageNote` (first list row): what this list is, and
  that it is not My Contacts.
- **List** — each captured visitor's `ContactCard`.
- **Pull-to-refresh / Retry** — re-fetch `listMyVisitors`.
- **403** — the forbidden message (only exhibitor accounts may scan).
- **Empty** — the "scan a visitor badge at your booth to capture them here"
  message.
- **Back** — `backOrHome`.

### The sheet's buttons can no longer strand (fixed 2026-08-20)

Both of `CapturedVisitorSheet`'s actions cleared `_busy` on the success path and
again inside `on ApiFailure`, with no `finally` — so anything thrown that is
**not** an `ApiFailure` left Export vCard and Remove disabled for good, with no
toast and no way out but dismissing the sheet. The escape is real:
`SimfApiClient` converts only the **first** call's errors to `ApiFailure`, and
the 401 token-refresh branch sits outside that guard, so a keystore/keychain
`PlatformException` (an OS keystore reset, a restored backup) surfaces raw
mid-action. Both now clear in a `finally`.

Remove adds one condition: the `finally` is skipped once the sheet has popped.
`mounted` does **not** stand in for "already gone" — `pop()` only reverses the
route's exit animation and the State outlives it — so re-enabling there would
flick the spinner back to the icon on a sheet the user can still see sliding
away. `SavedContactSheet` carries the same pair of fixes, which is the point:
the two card lists still behave identically.

## Tests

`test/golden/my_visitors_golden_test.dart` (render-lock, @375×812, ar; re-locked
for the BUG-025 title + note — unchanged by FR-EXH-002, the sheet lives behind a
tap) +
`test/features/exhibitor/my_visitors_screen_test.dart` (empty / list / 403 /
booth title + note / FR-EXH-002 sheet opens / FR-EXH-002 confirmed removal drops
the lead and reloads) +
`test/features/exhibitor/captured_visitor_sheet_test.dart` (2 — a confirmed
removal leaves Remove disabled as the sheet exits; a failed removal re-enables it
on the sheet that stays). Backend:
`tests/SIMF.Api.Tests/ExhibitorLeadManagementTests.cs` (9 cases — booth-shared
list, rival-booth isolation, colleague re-scan dedup, soft-delete, delete
scoping, vCard export, cross-booth export 404, visitor-token 403, legacy
untagged-row adoption).
E2E: [`docs/tests/e2e/mobile-my-visitors.md`](../../../tests/e2e/mobile-my-visitors.md)
(E2E-MOBMYVIS-001..012; 001..006 authored D-648 — closed the earlier
pre-existing gap; 008 added for BUG-025; 010..012 for FR-EXH-002/003).

## Related decisions

- **FR-EXH-002 / FR-EXH-003** (2026-07-27) — remove + vCard export on a captured
  lead, and the list scoped to the booth rather than the officer.
- **D-771** (BUG-024 lead email + BUG-025 keep the two lists separate, title +
  note).
- **D-642** (this clean-code freeze — `SimfPullToRefresh` swap + render-lock
  golden + first PAGE-INDEX row).
- **D-426** (exhibitor scan + my-visitors built).
