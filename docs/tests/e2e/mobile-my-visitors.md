# E2E test catalogue — `My Booth Visitors` (`myVisitors`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the
> exhibitor "زوار جناحي / My Booth Visitors" list (D-426). Reached from the
> exhibitor side-drawer entry, the exhibitor home's tools row, and after a
> successful visitor-badge scan. Backend:
> `GET /app/exhibitor/my-visitors` (`ExhibitorRepository.listMyVisitors`),
> resolving each captured visitor's card live (no PII snapshot). App tests:
> `src/Mobile/simf_app/test/features/exhibitor/my_visitors_screen_test.dart`
> (widget, 4 cases) + the render-lock golden
> `test/golden/my_visitors_golden_test.dart` (`goldens/my_visitors.png`
> @375×812). Clean-code reviewed + frozen (D-642, 2026-07-04); per-page doc
> [`docs/pages/mobile/my-visitors/`](../../pages/mobile/my-visitors/README.md).
> **BUG-025 (2026-07-26):** the screen was renamed زوار جناحي / My Booth
> Visitors and carries a `SimfPageNote` stating it is separate from
> [My Contacts](mobile-my-contacts.md) — the two features are deliberately NOT
> merged, pending an owner ruling.

| | |
|--|--|
| **Page** | mobile exhibitor captured-visitor list (no Figma frame — functional page) |
| **Route** | app screen `/exhibitor/visitors` (`RouteNames.myVisitors`) |
| **Surface** | Mobile (Flutter); single-column list |
| **Role/gate** | Exhibitor (approved) with a current booth membership — DEF-EXH-001: the server authorises on `ProfileType.MobileAppRole == Exhibitor` (D-519), so Staff / Moderator / Media / Sponsor / plain Visitor callers all get 403 → the forbidden surface. DEF-EXH-006: an active `ExhibitorMembership` of an active `Exhibitor` is required alongside the role |
| **Test runner** | Flutter widget/unit test + device manual |

> **Notes:** each row is the shared `ContactCard` with the visitor's card
> resolved on read; a visitor who has hidden their card renders the "no longer
> available" state instead of details. The list is pull-to-refresh (branded
> `SimfPullToRefresh`) and its first row is the BUG-025 explanatory note.

---

### E2E-MOBMYVIS-001 — Golden path (captured visitors list)

```gherkin
Scenario: An exhibitor sees the visitors they captured
  Given a signed-in approved exhibitor opens "زوار جناحي" from the drawer
  When GET /app/exhibitor/my-visitors returns their captured visitors (newest first)
  Then each visitor renders as a ContactCard (name, job title, organisation,
    country, email, mobile) with gold RTL field icons
  And the app bar title reads "زوار جناحي / My Booth Visitors"
```

### E2E-MOBMYVIS-002 — Empty state

```gherkin
Scenario: No visitors captured yet
  Given the exhibitor has captured no visitors
  When GET /app/exhibitor/my-visitors returns an empty list
  Then the message "No booth visitors yet. Scan a visitor badge at your booth to
    capture them here." ("لم تقم بمسح أي زائر بعد…") shows
  And no ContactCard is rendered
```

### E2E-MOBMYVIS-003 — Auth gate (visitor-tier → 403 forbidden)

```gherkin
Scenario: A non-exhibitor account is refused
  Given a signed-in visitor-tier account reaches the screen
  When GET /app/exhibitor/my-visitors returns 403
  Then the forbidden message "Only exhibitor accounts can scan visitor badges."
    ("يمكن لحسابات العارضين فقط…") shows
  And no visitor list is rendered
```

### E2E-MOBMYVIS-004 — Server error + retry

```gherkin
Scenario: A transport / 5xx failure shows error + retry
  Given the list load fails (non-403 ApiFailure)
  Then the shared error surface shows the message + a "Retry" button
  When the exhibitor taps Retry
  Then GET /app/exhibitor/my-visitors is re-fetched
  And on success the captured-visitor list renders
```

### E2E-MOBMYVIS-005 — Pull-to-refresh

```gherkin
Scenario: Pull-to-refresh re-fetches the list
  Given the captured-visitor list is shown
  When the exhibitor pulls down (SimfPullToRefresh — gold accent spinner)
  Then GET /app/exhibitor/my-visitors is re-fetched
  And a newly-captured visitor (e.g. one scanned since) appears
```

### E2E-MOBMYVIS-006 — Unavailable subject + RTL

```gherkin
Scenario: A visitor who hid their card
  Given a captured visitor has set their card unavailable
  Then that row shows the "no longer available" state ("هذه الجهة لم تعد متاحة")
    instead of the contact details

Scenario: RTL
  Given the app language is Arabic
  Then the app bar, the ContactCards (gold avatar right, chevron/icons mirrored)
    and the empty/forbidden messages render right-to-left, no tofu
```

### E2E-MOBMYVIS-007 — Bilingual job title (2026-07-20)

```gherkin
Scenario: A captured visitor's job title localizes per language
  Given a captured visitor whose profile has an Arabic job title (JobTitleArabic)
  And the app language is Arabic
  Then their ContactCard shows the Arabic job title
  When the app language is English
  Then the same card shows the English JobTitle
  # VisitorCard.jobTitleArabic + localizedJobTitle(isArabic): Arabic primary in
  # ar, English fallback, nothing shown when neither is set. Backend flow covered
  # by VisitorContactSharingTests; getter by contact_models_test.localizedJobTitle.
```

### E2E-MOBMYVIS-008 — Legacy captures stop projecting a card (DEF-EXH-004)

```gherkin
Scenario: A row whose subject is now deactivated is no longer listed
  Given a signed-in Approved exhibitor
  And an ExhibitorVisitorScan row it captured while the subject test did not
    exist yet, whose subject has since been deactivated (soft-deleted)
  When it calls GET /api/v1/app/exhibitor/visitors
  Then that row is absent from the response
  And no PII for that subject (login email, Saudi mobile, international mobile)
    is projected

Scenario Outline: Every ACTIVE subject is listed, whatever its profile type
  Given the same exhibitor also holds a capture of <subject>
  Then that row is still listed with the full card

  Examples:
    | subject                                  |
    | an ACTIVE audience visitor               |
    | a Staff (partner-side) account           |
```

> The subject test was enforced at CAPTURE time only, so every row taken while
> there was no test at all kept projecting a full live card on READ — the fix runs
> the same subject predicate (`IsCapturableSubject`, one shared expression) on the
> list path. **D-780 (owner decision 2026-07-27, "can scan all badges")** then
> widened that predicate itself to "any ACTIVE profile", reversing the DEF-EXH-003
> audience-side narrowing: a staff / media / sponsor capture is a legitimate lead
> and DOES list; only a deactivated subject drops out. The CALLER test still
> guards this endpoint (DEF-EXH-001 + DEF-EXH-006): a Staff token gets 403 and can
> never read back what it captured under the old rule.

**Evidence:** `tests/SIMF.Api.Tests/ExhibitorVisitorScanTests.cs` —
`Legacy_captures_of_deactivated_subjects_are_not_listed`,
`Staff_caller_cannot_list_rows_it_captured_under_the_old_rule_403`.

### E2E-MOBMYVIS-009 — A former officer cannot read the booth's cards (DEF-EXH-006)

```gherkin
Scenario: Revoking the booth membership closes the list too
  Given a signed-in booth officer whose captures are listed by
    GET /api/v1/app/exhibitor/visitors
  When their ExhibitorMembership is deactivated (or the exhibitor is closed
    with DELETE /api/v1/admin/exhibitors/{id})
  Then the same call answers 403 on their existing token
  And no visitor PII (login email, Saudi mobile, international mobile) is
    projected for any captured row
```

> DEF-EXH-006: the DEF-EXH-001 role test alone left the authority attached to
> the PERSON, so an officer dropped from a booth kept a live window onto every
> contact card that booth had captured. `ListMyVisitorsAsync` shares
> `EnsureExhibitorAsync` with the scan, so the membership requirement closes the
> read path with it.

**Evidence:**
`ExhibitorVisitorScanTests.Booth_officer_is_refused_once_the_membership_is_revoked`,
`ExhibitorVisitorScanTests.Closing_the_exhibitor_revokes_its_officers_scan_authority`.

---

_Last reviewed:_ `2026-07-27` by `SIMF Team` — **owner decision D-780 ("can scan
all badges")**: the shared subject predicate is now "any ACTIVE profile", so a
media / sponsor / staff capture legitimately lists and only a DEACTIVATED subject
drops out; E2E-MOBMYVIS-008 rewritten. Earlier: `2026-07-27` — DEF-EXH-006: the
list now needs a
CURRENT booth membership, so a former officer loses the captured cards with the
booth; E2E-MOBMYVIS-009. Earlier: `2026-07-27` — DEF-EXH-004: the capture-time
subject eligibility test now also runs on the READ path, so rows captured while
the old rule was in force stop projecting a card; E2E-MOBMYVIS-008. Earlier:
2026-07-20 — bilingual job title: the captured-visitor `ContactCard` now
localizes the title via `VisitorCard.jobTitleArabic` / `localizedJobTitle`
(Arabic primary in ar, English fallback); E2E-MOBMYVIS-007. Earlier:
`2026-07-04` by `SIMF Team`.
### E2E-MOBMYVIS-008 — Booth title + "not My Contacts" note (BUG-025, 2026-07-26)

```gherkin
Scenario: The exhibitor list names the booth and says what it is not
  Given a signed-in approved exhibitor opens the list with at least one capture
  Then the app bar title reads "زوار جناحي" (ar) / "My Booth Visitors" (en)
  And the first row of the list is a SimfPageNote reading
      "بطاقات الزوار التي مسحتها في جناحك. قائمة منفصلة عن «جهات اتصالي»." (ar) /
      "Badges you scanned at your booth. This list is separate from My Contacts."
  And the note scrolls with the list (it never steals viewport height)

Scenario: The two lists stay separate
  Given the same account also has saved cards in My Contacts (/contacts)
  Then a badge scanned at the booth appears ONLY in My Booth Visitors
  And a card saved by visitor-to-visitor sharing appears ONLY in My Contacts
  # Deliberate: merging the two features needs an owner ruling. See
  # docs/decisions/DECISIONS_LOG.md D-771.
```

**Evidence:** `my_visitors_screen_test` case "titles the booth and explains it is
not My Contacts"; render-lock golden `goldens/my_visitors.png` re-locked with the
new title + note.

### E2E-MOBMYVIS-010 — The list is the BOOTH's, not the officer's (FR-EXH-003)

```gherkin
Scenario: Two officers of one booth share one lead list
  Given "Officer One" and "Officer Two" are both active members of booth
    "Shared Booth"
  And Officer One scans visitor badge "SHAREDLEAD1" with the note "met at the stand"
  And Officer Two scans visitor badge "SHAREDLEAD2"
  When either of them calls GET /api/v1/app/exhibitor/visitors
  Then BOTH captures are listed, with the same capture ids for both officers

Scenario: A rival booth sees none of them
  Given an officer of a different exhibitor "Rival Booth"
  When they call GET /api/v1/app/exhibitor/visitors
  Then neither capture is listed
  And no PII for either subject is projected

Scenario: A colleague re-scanning updates the booth's ONE lead
  Given Officer One captured badge "DEDUPEBADGE" with the note "morning"
  When Officer Two scans the same badge with the note "afternoon"
  Then the booth still holds exactly ONE capture for that visitor
  And its note reads "afternoon"
  And the visitor received exactly ONE ExhibitorLeadCaptured notification

Scenario: A legacy untagged capture is adopted on the next scan
  Given an ExhibitorVisitorScan row written before the ExhibitorId column
    existed, whose capturer had no membership for the migration to backfill from
  Then it is listed for its CAPTURER only, and not for their colleague
  When its capturer scans that visitor again
  Then the SAME row is adopted into the booth (not duplicated)
  And the colleague now sees it too
```

> `ExhibitorVisitorScan` carried no `ExhibitorId`, so a lead belonged to the
> PERSON who scanned it: two officers of one booth kept two disjoint lead lists
> and neither could see the other's captures. The additive nullable column +
> migration `App/20260727045650_FRExh003_AddExhibitorVisitorScanExhibitorId`
> backfills from the capturing user's oldest ACTIVE `ExhibitorMembership`; a row
> whose capturer has no membership is deliberately left NULL rather than guessed
> at, and the person-scoped fallback keeps it visible to that capturer alone.

**Evidence:** `tests/SIMF.Api.Tests/ExhibitorLeadManagementTests.cs` —
`Both_officers_of_a_booth_see_the_same_captured_leads`,
`A_rival_booths_officer_does_not_see_the_leads`,
`A_colleague_rescanning_updates_the_booths_single_lead`,
`A_legacy_untagged_capture_stays_visible_and_is_adopted_on_rescan`.

### E2E-MOBMYVIS-011 — Remove a captured lead (FR-EXH-002)

```gherkin
Scenario: A mis-scan is dropped from the booth's list
  Given a signed-in booth officer with at least one captured lead
  When they tap the lead's row
  Then the captured-visitor sheet opens showing the full card plus
    "تصدير vCard / Export vCard" and "إزالة / Remove"
  When they tap Remove
  Then the confirm dialog "إزالة هذا الزائر؟ / Remove this visitor?" asks first
  When they confirm
  Then DELETE /api/v1/app/exhibitor/visitors/{id} answers 200
  And the toast "تمت إزالة الزائر / Visitor removed" shows
  And the list reloads without that lead
  And a colleague on the same booth no longer sees it either

Scenario: Soft, not hard
  Then the ExhibitorVisitorScan row survives with IsActive = false
  And an Exhibitor.LeadRemoved audit entry names the actor and the capture id
  # The capture carries the visitor's consent trail (the capture notified them
  # their card had been shared), so its removal has to stay attributable.

Scenario: Idempotent
  When the same id is deleted a second time
  Then the response is still 200

Scenario: A rival booth cannot remove our lead
  Given an officer of a different exhibitor
  When they DELETE the same capture id
  Then our lead is still listed
```

### E2E-MOBMYVIS-012 — Export a captured lead as a vCard (FR-EXH-002)

```gherkin
Scenario: The lead is exported as a .vcf
  Given a captured lead "Nabil Farid", job title "Fleet Engineer",
    Saudi mobile "0501234567"
  When the officer taps "Export vCard" on the sheet
  Then GET /api/v1/app/exhibitor/visitors/{id}/vcard answers 200
  And the content type is "text/vcard; charset=utf-8"
  And the body starts "BEGIN:VCARD\r\nVERSION:3.0\r\n" and ends "END:VCARD\r\n"
  And it carries FN:Nabil Farid, TITLE:Fleet Engineer, TEL;TYPE=CELL:0501234567
  And the share sheet opens with the filename "simf-lead.vcf"

Scenario: The export is not a second door onto a refused card
  Given a lead captured by a DIFFERENT booth
  When this officer requests its vcard
  Then the response is 404
  # Same booth scope AND the same DEF-EXH-004 subject-eligibility test as the
  # list, so a card the list will not project cannot be exported either.

Scenario: A visitor token is refused outright
  Given a plain visitor-tier token
  Then both DELETE …/visitors/{id} and GET …/visitors/{id}/vcard answer 403
```

> FR-EXH-002: My Contacts has had BOTH a remove and a vCard export since D-286;
> the exhibitor lead list had neither, so a mis-scan was permanent and the card
> could only be read on screen. The export shares one renderer with My Contacts
> (`VisitorCardVCard`) so the two can never drift; only the download filename
> differs.

**Evidence:** `tests/SIMF.Api.Tests/ExhibitorLeadManagementTests.cs` —
`Removing_a_lead_soft_deletes_it_for_the_whole_booth`,
`A_rival_booth_cannot_remove_our_lead`, `Exports_a_captured_lead_as_a_vcard`,
`Cannot_export_a_lead_from_another_booth`,
`A_visitor_token_cannot_remove_or_export_a_lead`. App:
`my_visitors_screen_test` — "FR-EXH-002: tapping a lead opens the export +
remove sheet", "FR-EXH-002: a confirmed removal drops the lead and reloads".

---

_Last reviewed:_ `2026-07-27` by `SIMF Team` — FR-EXH-002 (remove + vCard export
on a captured lead, via the new `CapturedVisitorSheet`) and FR-EXH-003 (the list
is scoped to the BOOTH, not the officer — additive `ExhibitorVisitorScan
.ExhibitorId`); E2E-MOBMYVIS-010..012. Earlier: 2026-07-26 by Claude — BUG-025:
renamed زوار جناحي / My Booth Visitors, added the `SimfPageNote` separating it
from My Contacts, refreshed the empty-state copy and re-locked the golden;
E2E-MOBMYVIS-008. Earlier: `2026-07-20` (bilingual job title,
E2E-MOBMYVIS-007) and `2026-07-04` by `SIMF Team`.
