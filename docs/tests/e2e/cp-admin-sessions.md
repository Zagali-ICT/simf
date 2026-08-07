# E2E test catalogue — Sessions CRUD + broadcast lifecycle (`/admin/sessions`)

| | |
|--|--|
| **Page** | [`cp/admin-sessions.md`](../../pages/cp/admin-sessions.md) — source `SessionsList.razor` + `SessionsAddEdit.razor` + `SessionsViewDelete.razor` (D-353 CrudShell) |
| **Route** | `/admin/sessions` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-08-04 (arrival-grace override: added D-839 SES-057..059, its PUT round-trip fixed D-842 SES-060) |

> **Permissions.** The page is gated `@attribute [RequirePermission(PermissionCatalog.Sessions.View)]`
> (`"Sessions.View"`). CRUD actions sit behind distinct codes:
> `Sessions.Create`, `Sessions.Edit`, `Sessions.Delete`. The **broadcast
> lifecycle** transition buttons AND the **recording** upload/remove block are
> gated by a separate code `Sessions.Publish` (`<AuthorizedAction
> Permission="PermissionCatalog.Sessions.Publish">`) — held by the Scientific
> Committee role — so an admin with View/Edit but not Publish sees the Details
> modal **without** the lifecycle footer or the recording uploader. The
> superadmin wildcard (`Administrator = "*"`) satisfies all of these.

> **API surface (BFF passthrough → API).** Every call goes through the CP BFF
> (`/account/api/...`) which forwards to the API:
> - `POST /account/api/admin/sessions/list` → `POST /admin/sessions/list` (`Sessions.View`)
> - `GET  /account/api/admin/sessions/{id}` → `GET  /admin/sessions/{id}` (`Sessions.View`)
> - `POST /account/api/admin/sessions` → `POST /admin/sessions` (`Sessions.Create`)
> - `PUT  /account/api/admin/sessions/{id}` → `PUT  /admin/sessions/{id}` (`Sessions.Edit`)
> - `DELETE /account/api/admin/sessions/{id}` → `DELETE /admin/sessions/{id}` (`Sessions.Delete`, soft-delete)
> - `PUT  /account/api/admin/sessions/{id}/status` → `PUT  /admin/sessions/{id}/status` (`Sessions.Publish`)
> - `POST /account/api/admin/sessions/{id}/recording` → multipart upload (`Sessions.Publish`)
> - `DELETE /account/api/admin/sessions/{id}/recording` (`Sessions.Publish`)
>
> The form also lazy-loads pickers: `POST .../halls/list`, `.../speakers/list`,
> `.../themes/list`, `.../session-categories/list` (all `Top=500`, `isActive=true`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SES-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-SES-002 | Add form: Hall + Speakers (with role + reorder) + Themes + Category pickers | happy | P1 | _to author_ |
| E2E-SES-003 | Broadcast lifecycle — Scheduled → Held → Recorded → Published (+ reverse) | happy | P0 | _to author_ |
| E2E-SES-004 | Recording — upload video → Details shows file + size → remove | happy | P1 | _to author_ |
| E2E-SES-005 | Capacity override blank → "Inherits from hall"; numeric → effective capacity | happy | P2 | _to author_ |
| E2E-SES-006 | Grid: filter by Title, sort by Start, paginate. **End must order by End, not Start** — seed a session that starts first and ends last, so a switch falling through to Start returns the exact reverse | happy | P2 | authored ✓ (`GridDateSortKeyTests.Sessions_sort_on_start_honours_the_descending_direction`, `Sessions_sort_on_end_orders_by_END_not_by_start`) |
| E2E-SES-007 | Empty list renders `SimfEmptyState` ("No sessions yet.") | happy | P1 | _to author_ |
| E2E-SES-008 | Auth: signed-in user lacking `Sessions.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SES-009 | Auth: View/Edit but not `Sessions.Publish` → no lifecycle/recording controls | auth | P0 | _to author_ |
| E2E-SES-010 | Validation: blank Code / 1-char Code → bilingual modal error | error | P1 | _to author_ |
| E2E-SES-011 | Validation: End ≤ Start → bilingual time-window error | error | P1 | _to author_ |
| E2E-SES-012 | Conflict: duplicate Code → 409 `SESSION_CODE_DUPLICATE` | error | P1 | _to author_ |
| E2E-SES-013 | Conflict: illegal lifecycle move → 400 `SESSION_STATUS_TRANSITION_INVALID` | error | P1 | _to author_ |
| E2E-SES-014 | Validation: inactive/unknown Hall → 400 `SESSION_HALL_NOT_FOUND` | error | P2 | _to author_ |
| E2E-SES-015 | Recording: non-video upload → 400 `SESSION_RECORDING_INVALID` | error | P2 | _to author_ |
| E2E-SES-016 | Server 500 on `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-SES-017 | RTL/Arabic render mirrors grid + Add modal + lifecycle footer | i18n | P1 | _to author_ |
| E2E-SES-018 | Live-URL validation (D-349) — YouTube/HLS accepted; other URL → bilingual error (client + 400 `SESSION_INVALID`) | error | P1 | _to author_ |
| E2E-SES-019 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-SES-020 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-SES-021 | Delete confirmation: Deactivate opens View/Delete → SimfConfirm gates the call (D-353) | error | P0 | _to author_ |
| E2E-SES-022 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-SES-023 | Excel import: upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-SES-024 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |
| E2E-SES-031 | Moderate row action → navigates to the live Q&A desk; hidden without Questions.Moderate (D-646) | happy | P1 | _to author_ |
| E2E-SES-025 | AI live captions field round-trips + the whole live section survives an edit (regression — D-439) | happy/regression | P1 | authored ✓ (`AdminSessionsTests.Update_round_trips_all_live_fields`) |
| E2E-SES-026 | Excel export/import round-trips the 8 previously-dropped fields (Description+Arabic, the 2 live URLs, the 2 live captions, Type, SeatSelectionModeOverride) — D-506 | happy/regression | P1 | authored ✓ (`SessionsExcelTests.Export_includes_the_dropped_round_trip_columns` + `.Import_round_trips_the_dropped_fields`) |
| E2E-SES-032 | Deep-link `/admin/sessions?speakerId={id}` (from the Speakers grid's Sessions action) filters to that speaker's sessions + shows the "filtered by speaker" note (speakers redesign) | function | P1 | authored ✓ (`AdminSessionsTests.List_filtered_by_speakerId_returns_only_that_speakers_sessions`) |
| E2E-SES-033 | Booking guard: delete a session with active visitor bookings → 409 `SESSION_HAS_ACTIVE_BOOKINGS` (bilingual toast); the session stays Active (S-1) | error | P0 | authored ✓ (`AdminSessionsTests.DeactivateAsync_WithActiveVisitorBooking_ReturnsConflict`) |
| E2E-SES-034 | Booking guard: delete succeeds when only an admin row-block remains (no attendee to orphan) (S-1) | happy | P1 | authored ✓ (`AdminSessionsTests.DeactivateAsync_WithOnlyAdminRowBlock_Succeeds`) |
| E2E-SES-035 | Edit hall → held seats cascade-released + each affected visitor notified (BookingRejected) (S-1) | happy | P1 | authored ✓ (`AdminSessionsTests.UpdateAsync_HallChange_ReleasesHeldSeats_AndDispatchesNotification`) |
| E2E-SES-036 | Edit start/end window → held seats cascade-released; a title-only edit keeps the seats (S-1) | happy | P2 | authored ✓ (`AdminSessionsTests.UpdateAsync_TimeChange_ReleasesHeldSeats` + `.UpdateAsync_NoHallOrTimeChange_KeepsSeats`) |
| E2E-SES-037 | Capacity override below the seats already held → 409 `SESSION_CAPACITY_BELOW_BOOKINGS`; a hall move that lowers the override is allowed (seats are released) (S-1) | error | P1 | authored ✓ (`AdminSessionsTests.UpdateAsync_CapacityOverrideBelowHeldSeats_ReturnsConflict` + `.UpdateAsync_HallChange_WithLowerCapacityOverride_Succeeds`) |
| E2E-SES-038 | Create a session overlapping another in the same hall → 409 `SESSION_HALL_TIME_OVERLAP`; exact back-to-back (half-open) allowed; different hall allowed; a soft-deleted sibling is ignored (S-2) | error | P1 | authored ✓ (`AdminSessionsTests.CreateAsync_OverlappingHallTime_ReturnsConflict` + `.CreateAsync_SameHallBackToBack_NonOverlapping_Succeeds` + `.CreateAsync_OverlapDifferentHall_Succeeds` + `.CreateAsync_OverlapWithSoftDeletedSession_Succeeds`) |
| E2E-SES-039 | Move a session into an occupied hall/time → 409 `SESSION_HALL_TIME_OVERLAP`; a title-only edit of a legacy overlapping session still saves (S-2) | error | P1 | authored ✓ (`AdminSessionsTests.UpdateAsync_MoveIntoOccupiedHallTime_ReturnsConflict` + `.UpdateAsync_TitleOnlyEdit_WithPreexistingOverlap_Succeeds`) |
| E2E-SES-040 | Lifecycle guard: mark Held before the session's start → 400 `SESSION_STATUS_GUARD_FAILED`; after start it is allowed (S-7) | error | P1 | authored ✓ (`SessionLifecycleTests.SetStatusAsync_MarkHeldBeforeStart_ReturnsBadRequest` + `.SetStatusAsync_MarkHeldAfterStart_Succeeds`) |
| E2E-SES-041 | Lifecycle guard: mark Recorded/Published with no recording → 400 `SESSION_STATUS_GUARD_FAILED`; upload a recording first → allowed; a reverse (undo) move carries no guard (S-7) | error | P1 | authored ✓ (`SessionLifecycleTests.SetStatusAsync_MarkRecordedWithoutRecording_ReturnsBadRequest` + `.SetStatusAsync_RevertRecordedToHeld_NoGuard` + `SessionRecordingTests.SetStatusAsync_MarkRecordedWithRecording_Succeeds`) |
| E2E-SES-042 | Session language (bilingual "at a glance" label) round-trips on save; > 64 chars → 400 `SESSION_INVALID` (Website Session-detail, Figma 5991-85840) | happy | P1 | _to author_ |
| E2E-SES-043 | Key outcomes ("أبرز المخرجات") add / edit / reorder / remove — repeatable bilingual list, renumbered 0..n-1; one-language-only → 400; blank row dropped; remove-all clears (RemoveRange re-sync) | happy | P1 | _to author_ |
| E2E-SES-044 | Required session Type (#3) — create with no type → 400 `SESSION_TYPE_REQUIRED` + client marker; a legacy untyped row still saves an unrelated edit (grandfathered); clearing a set type → 400 | error | P1 | authored ✓ (`AdminSessionsTests.Create_without_a_type_is_400_SESSION_TYPE_REQUIRED` + `.Update_legacy_untyped_speakerless_row_is_grandfathered` + `.Update_clearing_a_set_type_is_400_SESSION_TYPE_REQUIRED`) |
| E2E-SES-045 | Min-1 speaker unless Event (#4) — non-Event create with no speaker → 400 `SESSION_SPEAKER_REQUIRED`; an Event saves with none; a legacy speakerless non-Event row still saves (grandfathered); dropping the last speaker of a non-Event → 400 | error | P1 | authored ✓ (`AdminSessionsTests.Create_non_event_with_no_speakers_is_400_SESSION_SPEAKER_REQUIRED` + `.Create_event_with_no_speakers_succeeds` + `.Update_dropping_the_last_speaker_of_a_non_event_is_400`) |
| E2E-SES-047 | Seat-release warning (A1/A6) — an edit that moves the Hall or the Start/End window opens a `SimfConfirm` naming the exact registration + admin-row-block counts before it submits; Cancel leaves the form untouched | error | P0 | authored ✓ (`SessionLifecycleNoticeTests.A1_Get_stamps_the_holding_a_hall_or_time_change_would_release`) |
| E2E-SES-048 | Seat-release reporting (A1/A6) — after confirming, the toast names what was released and a `SeatReservation.Released` audit row records `reason=HallChanged\|Rescheduled; reservations=N; adminBlocks=M`; a slot-preserving edit reports nothing | happy/regression | P0 | authored ✓ (`SessionLifecycleNoticeTests.A1_A6_Hall_change_reports_and_audits_what_it_released` + `.A1_An_edit_that_leaves_the_slot_alone_reports_no_releases`) |
| E2E-SES-049 | Release notice reaches the inbox (A2) — the affected attendee gets the in-app row **and** an email, bilingual, quoting the new start on the Saudi wall clock with no UTC anywhere | happy/regression | P1 | authored ✓ (`SessionLifecycleNoticeTests.A2_Released_seat_notice_is_emailed_not_only_in_app`) |
| E2E-SES-050 | Reschedule re-arms the workers (A4) — moving Start/End clears `ReminderSent` + `RatingPromptSent` so the reminder fires for the new time; a title-only edit leaves both stamped | happy/regression | P1 | authored ✓ (`SessionLifecycleNoticeTests.A4_Moving_the_window_rearms_the_reminder_and_rating_prompt` + `.A4_An_edit_that_keeps_the_window_does_not_rearm_the_reminder`) |
| E2E-SES-051 | Booking-conflict copy (A5) — the 409 `SESSION_HAS_ACTIVE_BOOKINGS` message names `/admin/sessions/seat-plans` (bilingual) and never the read-only `/admin/bookings` monitor | error | P1 | authored ✓ (`SessionLifecycleNoticeTests.A5_Active_booking_conflict_points_at_the_seat_plans_page`) |
| E2E-SES-052 | Cancellation notice (B2) — deactivating a session dispatches `SessionCancelled` (in-app + email, bilingual, Saudi wall clock) to everyone holding a seat or who favourited it; the audit row carries `notified=N`; a session nobody saved notifies nobody | happy/regression | P0 | authored ✓ (`SessionLifecycleNoticeTests.B2_Deactivating_a_session_notifies_everyone_who_saved_it` + `.B2_A_session_nobody_saved_is_cancelled_without_notifying_anyone`) |
| E2E-SES-053 | Cancellation notice on the edit-form path (B2) — clearing the **Active** checkbox and saving announces exactly like Deactivate (`SessionCancelled` in-app + email + `Session.Deactivated` audit with `notified=N`); an edit that leaves Active ticked announces nothing | happy/regression | P0 | authored ✓ (`SessionLifecycleNoticeTests.B2_Unticking_Active_on_the_edit_form_notifies_exactly_like_Deactivate` + `.B2_An_edit_that_leaves_Active_ticked_announces_no_cancellation`) |
| E2E-SES-054 | **FR-702 live notice — author it (owner 2026-07-31 / D-815).** The "Live notice — shown with the stream" English + Arabic textareas save on create and are read back into the edit form; the value reaches the app live screen and the Website session page. Purely informational — it never withholds the stream | happy | P0 | authored ✓ (`SessionLiveNoticeTests.Create_with_a_live_notice_round_trips_on_the_admin_detail` + `.Update_round_trips_a_live_notice_added_after_creation` + `.Public_detail_exposes_the_live_notice` + `.A_live_notice_does_not_withhold_the_live_stream`; form side `SessionsAddEditLiveNoticeTests.Add_mode_posts_the_typed_notice` + `.Edit_mode_loads_the_stored_notice_into_both_boxes` + `.Edit_mode_puts_the_edited_notice`) |
| E2E-SES-055 | **FR-702 live notice — clear it.** Emptying both textareas and saving stores `null` for both, so the banner comes down on every surface; a session that never had one reads null throughout, and neither case touches the live URLs | happy/regression | P0 | authored ✓ (`SessionLiveNoticeTests.Update_clears_the_live_notice_back_to_null` + `.Public_detail_omits_the_live_notice_when_none_is_authored`; form side `SessionsAddEditLiveNoticeTests.Emptying_both_boxes_clears_the_notice`) |
| E2E-SES-056 | **FR-702 live notice — length triple-lock.** The input stops at `MaxLength="512"`; a 513-character notice posted past the UI returns 400 `SESSION_INVALID` with **both** the English and Arabic message, on create and on update, for either language; exactly 512 is accepted | error | P1 | authored ✓ (`SessionLiveNoticeTests.Create_with_an_over_length_live_notice_is_400_SESSION_INVALID` + `.Update_with_an_over_length_arabic_live_notice_is_400_SESSION_INVALID` + `.Create_with_a_live_notice_at_the_512_boundary_succeeds`; UI cap `SessionsAddEditLiveNoticeTests.Add_mode_renders_both_notice_boxes_capped_at_512`) |
| E2E-SES-046 | Excel import Speakers column (#3/#4) — a `Speakers` cell of speaker codes attaches the roster in order; a non-Event row with no speakers, an unknown speaker code, or a blank Type each become a per-row error | error | P1 | authored ✓ (`SessionsExcelTests.Import_attaches_the_speakers_column_in_order` + `.Import_non_event_row_without_speakers_is_a_per_row_error` + `.Import_unknown_speaker_code_is_a_per_row_error` + `.Import_row_without_a_type_is_a_per_row_error`) |
| E2E-SES-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-SES-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-SES-001 — Full CRUD round-trip

```gherkin
Feature: Sessions CRUD round-trip
  As an Administrator
  I want to manage the programme sessions catalogue
  So that the public agenda and the Flutter app show the correct line-up

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the Website is reachable on http://localhost:5115
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have landed on /admin/sessions
  And at least one active Hall named "Auditorium A" (code "AUD-A") exists

Scenario: Create, edit, view, deactivate one session
  Given the grid currently shows {N} rows
  When the administrator clicks "Add session"
  Then the Add modal opens titled "Add session"
  And it shows the fields: Code, Title (English), Title (Arabic), Description (English),
      Description (Arabic), Hall, Category, Start (Saudi time), End (Saudi time), Capacity override,
      Add speaker, Add theme
  When they fill Code="SES-001"
  And they fill Title (English)="Future of Naval Logistics"
  And they fill Title (Arabic)="مستقبل الإمداد البحري"
  And they fill Description (English)="A panel on supply-chain resilience."
  And they select Hall="Auditorium A (AUD-A)"
  And they fill Start (Saudi time)="2026-11-10T09:00"
  And they fill End (Saudi time)="2026-11-10T10:30"
  And they leave Capacity override blank
  And they click "Create session"
  Then the BFF POSTs /account/api/admin/sessions and the API returns 200
  And the modal closes
  And the grid shows {N + 1} rows
  And a green toast reads "Session \"Future of Naval Logistics\" was created."
  And a row exists with Code="SES-001", Title="Future of Naval Logistics",
      Hall="Auditorium A", Start "2026-11-10 09:00", a green "Active" pill,
      and a "Scheduled" lifecycle pill

  When the administrator clicks the "Edit" icon on that row
  Then the GET /account/api/admin/sessions/{id} returns 200
  And the Edit modal opens titled "Edit session" with every field pre-filled
  And an additional "Active — show in the public agenda" checkbox is visible (ticked)
  When they change End (Saudi time) to "2026-11-10T11:00"
  And they click "Save changes"
  Then the PUT /account/api/admin/sessions/{id} returns 200
  And the modal closes
  And a green toast reads "Session \"Future of Naval Logistics\" was updated."
  And the row's End (Saudi time) column reads "2026-11-10 11:00"

  When the administrator clicks the "Details" icon on that row
  Then a read-only modal opens titled "Session details"
  And it renders a description list with Code, Title, Title (Arabic), Description,
      Hall, Start, End, Capacity override ("Inherits from hall"), Effective capacity,
      Speakers ("—"), Status ("Active"), Lifecycle ("Scheduled"), Recording ("No recording")
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on that row
  Then the View/Delete form opens (CrudShell, dialog by default) showing the row's read-only details
      and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog asks to confirm, naming the session
  When they click the confirm "Deactivate" button
  Then the DELETE /account/api/admin/sessions/{id} returns 200
  And a green toast reads "Session \"Future of Naval Logistics\" was deactivated."
  And the row's Status pill changes from the green "Active" to the grey "Inactive"
```

**Evidence captured:**
- Before / after screenshots → `docs/screenshots/cp-admin-sessions-{canonical,add-modal,edit-modal,details-modal,deactivated}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/sessions/*` call returns 200 (the `/list`, the POST,
  the GET, the PUT, the DELETE) and each picker `.../halls|speakers|themes|session-categories/list` returns 200
- Audit rows: `RowAudit` Insert + Update + soft-delete (Update `IsActive=false`) rows for the
  session, each stamped with the actor id

### E2E-SES-002 — Add form pickers (Hall, Speakers with role + reorder, Themes, Category)

```gherkin
Scenario: Populate the relational pickers on the Add form
  Given two active Speakers "Capt. Lin" / "النقيب لين" and "Dr. Omar" / "د. عمر" exist
  And one active Theme "Logistics" / "الإمداد" exists
  And one active Session category "Plenary" / "عام" exists
  And the Add modal is open with Code, Title (EN/AR), Hall, Start and End already filled
  When they select Category="Plenary"
  And they pick "Capt. Lin" in the "Add speaker" select
  Then a chip "1. Capt. Lin" appears with a role dropdown defaulting to "Speaker",
      plus "Up", "Down" and "Remove" buttons
  When they pick "Dr. Omar" in the "Add speaker" select
  Then a chip "2. Dr. Omar" appears
  When they change Dr. Omar's role dropdown to "Host"
  And they click "Up" on Dr. Omar's chip
  Then the chips renumber to "1. Dr. Omar" (Host) and "2. Capt. Lin" (Speaker)
  When they pick "Logistics" in the "Add theme" select
  Then a chip "Logistics" appears with a "Remove" button
  And the same speaker can no longer be re-picked (it leaves the Add-speaker option list)
  When they click "Create session"
  Then the POST body carries Speakers=[{Dr.Omar,Host,order 0},{Capt.Lin,Speaker,order 1}]
      and ThemeIds=[Logistics] and CategoryId=Plenary
  And the API returns 200 and a green "…was created." toast shows
  When they re-open the row's Details modal
  Then the Speakers list renders "Dr. Omar (د. عمر)" then "Capt. Lin (النقيب لين)" in order
```

### E2E-SES-003 — Broadcast lifecycle transitions

```gherkin
Scenario: Walk a session through its broadcast lifecycle and back
  Given an active session "SES-001" exists in lifecycle "Scheduled"
  And the administrator has the Sessions.Publish permission (superadmin wildcard)
  When they open its Details modal
  Then the modal footer shows a single lifecycle button "Mark held"

  When they click "Mark held"
  Then the PUT /account/api/admin/sessions/{id}/status returns 200 with Status=Held
  And a green toast reads "Session lifecycle set to Held."
  And the modal footer now shows "Back to scheduled" and "Mark recorded"

  When they click "Mark recorded"
  Then the API returns 200 with Status=Recorded
  And the footer now shows "Back to held" and "Publish"

  When they click "Publish"
  Then the API returns 200 with Status=Published
  And a green toast reads "Session lifecycle set to Published."
  And the Details list now shows a "Published at" timestamp
  And the grid's Lifecycle pill for the row turns the green "on" variant reading "Published"
  And the footer now shows the single reverse button "Un-publish"

  When they click "Un-publish"
  Then the API returns 200 with Status=Recorded
```

**Evidence captured:**
- Screenshots → `docs/screenshots/cp-admin-sessions-lifecycle-{scheduled,held,recorded,published}.png`
- Network: four sequential `PUT .../status` calls each return 200
- Audit row: `RowAudit` Update rows recording the status column change with the actor id

### E2E-SES-004 — Recording upload / remove

```gherkin
Scenario: Attach then remove a session recording
  Given an active session "SES-001" exists
  And the administrator has the Sessions.Publish permission
  When they open its Details modal
  Then the recording row reads "No recording"
  And below the description list a "Recording file" file-input and an "Upload recording" button are visible
  When they choose a small valid file "talk.mp4" (video/mp4) in the file-input
  And they click "Upload recording"
  Then the BFF POSTs /account/api/admin/sessions/{id}/recording as multipart and the API returns 200
  And a green toast reads "Recording uploaded."
  And the recording row now reads "talk.mp4 (… MB/KB)"
  And a "Remove recording" button appears

  When they click "Remove recording"
  Then the DELETE /account/api/admin/sessions/{id}/recording returns 200
  And a green toast reads "Recording removed."
  And the recording row reverts to "No recording"
```

### E2E-SES-005 — Capacity override semantics

```gherkin
Scenario: Blank capacity inherits the hall; a number overrides it
  Given the Add modal is open with Hall="Auditorium A" (hall seat count 200)
  When they leave Capacity override blank and create the session
  And they open its Details modal
  Then "Capacity override" reads "Inherits from hall"
  And "Effective capacity" reads "200"
  When they Edit the session and set Capacity override="120" and save
  And they re-open Details
  Then "Capacity override" reads "120"
  And "Effective capacity" reads "120"
```

### E2E-SES-006 — Grid filter / sort / paginate

```gherkin
Scenario: Filter, sort and page the sessions grid
  Given more than 20 sessions exist
  When the administrator types "Naval" into the grid filter (Title column is Filterable)
  Then the POST /account/api/admin/sessions/list body carries the title filter
  And only rows whose Title contains "Naval" render
  When they click the "Start (Saudi time)" column header
  Then the list re-queries sorted by start ascending
  When they click "Next"
  Then the pager advances and the summary reads "Showing 21–40 of {total}"
```

### E2E-SES-032 — Deep-link filter by speaker (speakers redesign)

```gherkin
Scenario: Opening /admin/sessions with a ?speakerId filters to that speaker
  Given a speaker "Rear Admiral John Carter" linked to session "Opening panel"
  And a session "Closing remarks" that Carter is NOT linked to
  When the administrator opens /admin/sessions?speakerId={carterId}
    (as reached from the Speakers grid's per-row "Sessions" action)
  Then SessionsList reads the [SupplyParameterFromQuery] speakerId and seeds
    the grid's "speakerId" filter before the first load
  And the POST /account/api/admin/sessions/list body carries Filters.speakerId
  And AdminSessionService translates it to a SQL EXISTS over SessionSpeaker
    (session.Speakers.Any(link => link.SpeakerId == id))
  And only "Opening panel" renders — "Closing remarks" is absent
  And a blue SimfAlert info note reads "Showing only the sessions linked to the
    selected speaker." with a "Clear filter" link to /admin/sessions
  When they click "Clear filter"
  Then the browser loads /admin/sessions (no query) and the full list returns
```

**Evidence:** API integration coverage in
`tests/SIMF.Api.Tests/AdminSessionsTests.cs`
(`List_filtered_by_speakerId_returns_only_that_speakers_sessions`); screenshot
`docs/screenshots/cp-admin-sessions-032-speaker-filter.png`.

### E2E-SES-007 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no active Session rows
  When the administrator opens /admin/sessions
  Then the grid body renders the SimfEmptyState component
  And it shows the bilingual copy "No sessions yet." / "لا توجد جلسات بعد."
  And the toolbar still shows the "Add session" button
  And no error toast appears
```

### E2E-SES-008 — Auth gate (page permission)

```gherkin
Scenario: User lacking Sessions.View is denied the page
  Given a signed-in admin whose role does NOT grant "Sessions.View"
  When they navigate to /admin/sessions
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/sessions/list request fires
  And the "Sessions" nav item is not rendered in the rail (RequiredPermission gate)
```

### E2E-SES-009 — Action-level auth gate (Sessions.Publish)

```gherkin
Scenario: Edit-capable admin without Sessions.Publish sees no lifecycle or recording controls
  Given a signed-in admin whose role grants Sessions.View + Sessions.Edit but NOT Sessions.Publish
  When they open a session's Details modal
  Then the description list and the "Close" button render
  But the lifecycle transition buttons in the footer are NOT rendered
  And the "Recording file" uploader + "Upload recording"/"Remove recording" buttons are NOT rendered
  When they attempt PUT /account/api/admin/sessions/{id}/status directly (out of band)
  Then the API returns 403 (the policy PolicyFor(Sessions.Publish) denies it)
```

### E2E-SES-010 — Code validation

```gherkin
Scenario: Blank or too-short Code shows a bilingual error in the modal
  Given the Add modal is open with Title, Hall, Start and End filled
  When they leave Code blank (or enter a single character "S")
  And they click "Create session"
  Then a SimfAlert error appears at the top of the modal
  And reads "Code must be between 2 and 16 characters." / "يجب أن يتراوح طول رمز الجلسة بين 2 و 16 حرفاً."
  And the modal stays open
  And no POST /account/api/admin/sessions request fires (client-side guard in SessionForm)
```

### E2E-SES-011 — Time-window validation

```gherkin
Scenario: End at or before Start shows a bilingual time-window error
  Given the Add modal is open with Code, Title, Hall all valid
  When they set Start (Saudi time)="2026-11-10T10:00"
  And they set End (Saudi time)="2026-11-10T09:00"
  And they click "Create session"
  Then a SimfAlert error appears reading "End time must be after start time." / "يجب أن تكون نهاية الجلسة بعد بدايتها."
  And the modal stays open
  And no POST request fires (client guard); were it to reach the API it would 400 SESSION_INVALID_TIME_WINDOW
```

### E2E-SES-012 — Duplicate Code conflict

```gherkin
Scenario: Duplicate Code returns 409 with the bilingual server message
  Given an active session with Code="SES-001" already exists
  When the administrator opens the Add modal
  And fills Code="SES-001" plus a valid Title, Title (Arabic), Hall, Start and End
  And clicks "Create session"
  Then the BFF forwards POST /admin/sessions
  And the API returns HTTP 409 with ApiResult.Error.Code = "SESSION_CODE_DUPLICATE"
  And the modal stays open
  And the SimfAlert surfaces the bilingual MessageForCurrentCulture():
      "A session with code 'SES-001' already exists." / "توجد جلسة بالرمز 'SES-001' بالفعل."
```

### E2E-SES-013 — Illegal lifecycle transition

```gherkin
Scenario: An illegal lifecycle move is rejected with a bilingual server message
  Given a session "SES-001" in lifecycle "Scheduled"
  When a PUT /account/api/admin/sessions/{id}/status with Status=Published is sent
      (e.g. a stale modal whose only legal next move was "Mark held")
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_STATUS_TRANSITION_INVALID"
  And the error toast reads "A session cannot move from Scheduled to Published."
      / "لا يمكن نقل الجلسة من Scheduled إلى Published."
  And the modal stays open with the unchanged status
```

### E2E-SES-014 — Inactive / unknown Hall

```gherkin
Scenario: Selecting (or posting) an inactive hall is rejected
  Given the Add modal is open with a valid Code, Title, Start and End
  And the chosen HallId no longer exists or has been deactivated
  When they click "Create session"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_HALL_NOT_FOUND"
  And the SimfAlert reads "Hall '…' does not exist or is inactive." / "القاعة '…' غير موجودة أو غير مفعّلة."
  And the modal stays open
```

### E2E-SES-015 — Non-video recording rejected

```gherkin
Scenario: Uploading a non-video file is rejected
  Given the Details modal is open for a session and the administrator has Sessions.Publish
  When they choose "agenda.pdf" in the recording file-input
  And they click "Upload recording"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_RECORDING_INVALID"
  And the error toast reads "The recording must be a video file (mp4, m4v, webm, ogg, mov)."
      / "يجب أن يكون التسجيل ملف فيديو (mp4، m4v، webm، ogg، mov)."
  And the recording row still reads "No recording"
```

### E2E-SES-016 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/sessions/list (e.g. DB down)
  When the administrator opens /admin/sessions
  Then the grid shows the loading indicator
  And then a red toast appears reading "The sessions could not be loaded." / "تعذّر تحميل الجلسات."
  And no rows render
```

### E2E-SES-017 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the grid, the Add modal and the lifecycle footer
  Given the administrator is on /admin/sessions in English
  When they click the "العربية" link in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الجلسات"
  And the grid headers, the "Active"/"Lifecycle" pills and the pager arrows mirror

  When they click "إضافة جلسة"
  Then the Add modal opens in RTL with Arabic labels (الرمز، العنوان، القاعة، …)
  And the speaker chip "Up/Down/Remove" buttons appear in reverse order

  When they open a session's Details modal
  Then the Hall label shows the Arabic hall name
  And the lifecycle footer buttons render in Arabic ("وضع علامة منعقدة"… per resx) and mirror
```

### E2E-SES-018 — Live broadcast URL validation (D-349)

```gherkin
Scenario: A YouTube or HLS live URL is accepted; anything else is rejected
  Given the Add modal is open with a valid Code, Title, Hall, Start and End
  And the "Live stream URL (live broadcast)" field shows the hint
      "Paste a YouTube link (youtube.com / youtu.be) or a direct HLS/MP4 stream URL."
  When they set Live stream URL="https://www.youtube.com/watch?v=dQw4w9WgXcQ"
  And they click "Create session"
  Then the API returns 200 and the session is created with that live URL

  When they Edit the session and set Live stream URL="https://vimeo.com/12345"
  And they click "Save changes"
  Then a SimfAlert error reads "Enter a valid YouTube link or an HLS/MP4 stream URL (https)."
      / "أدخل رابط يوتيوب صالحاً أو رابط بث HLS/MP4 (https)."
  And the modal stays open and no PUT fires (client guard)
  And were it to reach the API it would 400 SESSION_INVALID (shared LiveStreamUrlPolicy)
```

### E2E-SES-025 — AI live captions field + full live section round-trips on edit (P5 — D-439)

```gherkin
Scenario: An administrator sets the AI live-caption text and it round-trips
  Given the Add/Edit modal is open with a valid Code, Title, Hall, Start and End
  And the live section shows "AI live captions (English)" and "AI live captions (Arabic)"
      textareas with the hint
      "Optional caption / running-transcript text shown under the live player. …"
  When they set AI live captions (English)="Welcome to the opening session."
  And AI live captions (Arabic)="مرحباً بكم في الجلسة الافتتاحية."
  And they click "Create session"
  Then the API returns 200 and the session detail carries both caption fields

Scenario: Editing a session preserves the whole live section (regression — D-439)
  Given a session created with a Live stream URL, a sign-language URL and caption text
  When the administrator opens it, changes the Title and clicks "Save changes"
  Then the PUT round-trips and the Live stream URL, sign-language URL and BOTH
      caption fields survive (they are no longer dropped by the update DTO)
  # Before D-439 the API-layer UpdateSessionRequest omitted the live fields, so a
  # PUT silently wiped the live broadcast. Proven by AdminSessionsTests
  # `Update_round_trips_all_live_fields`.
```

### E2E-SES-019 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/sessions with the default "dialog" presentation
  And the grid toolbar shows the "Open as full page" toggle (maximize icon) from CrudPresentationToggle
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.sessions" holds {"v":1,"presentation":"page"}
  When they reload /admin/sessions
  Then OnInitializedAsync re-reads Prefs.GetPresentationAsync("sessions")
  And the toggle still reads "Open as dialog"
  And opening "Add session" now renders the full-page frame (not a popup)
```

### E2E-SES-020 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (CrudPresentation.Page)
  When the administrator clicks "Add session"
  Then the grid + SimfBanner are hidden (GridHidden) and replaced by the CrudShell page frame
      (title "Add session" + close header + the SessionsAddEdit form)
  And there is no modal backdrop
  When they fill Code, Title (EN/AR), Hall, Start and End and click "Create session"
  Then the POST /account/api/admin/sessions returns 200
  And the page frame closes and the grid re-appears with the new row and the success toast
  When they click the "Edit" icon and then the frame's "Close" (X) header button
  Then the GET /account/api/admin/sessions/{id} loads the full detail into SessionsAddEdit
  And clicking Close re-shows the grid unchanged (no PUT fires)
  When they click the "Details" icon
  Then SessionsViewDelete renders read-only in the same full-page frame (IsDelete=false, no Deactivate button)
```

### E2E-SES-021 — Delete confirmation gate (D-353)

```gherkin
Scenario: Deactivate requires explicit confirmation through SimfConfirm
  Given the administrator is on /admin/sessions
  When they click the "Deactivate" icon on a row
  Then the GET /account/api/admin/sessions/{id} loads the detail
  And SessionsViewDelete opens (CrudShell) showing the read-only details and a red "Deactivate" button
      (the broadcast-lifecycle + recording blocks render too, gated by Sessions.Publish)
  When they click "Deactivate"
  Then a SimfConfirm dialog appears with the title "Deactivate session" and the message
      naming the session: "Deactivate the session \"Future of Naval Logistics\"? It will be hidden from the public agenda."
  When they click "Cancel"
  Then no DELETE request fires and the row is unchanged
  When they re-open and click "Deactivate" then the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/sessions/{id} fires (soft-delete, Sessions.Delete)
  And the success toast reads "Session \"Future of Naval Logistics\" was deactivated."
  And the grid reloads with the row's pill turned grey "Inactive"
```

### E2E-SES-022 — Excel export (D-356)

```gherkin
Scenario: Export the sessions grid to an XLSX workbook
  Given the administrator is on /admin/sessions with at least two sessions
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/sessions/export fires carrying AdminGridExportRequest
      with an empty Ids list and the current Query (whole filtered grid, capped at 5000 rows)
  And the browser saves a file named simf-sessions-{timestamp}.xlsx
  And the workbook's "Sessions" sheet header row reads
      Code | Title | TitleArabic | Hall | Category | Start | End | Capacity | Status | IsActive
      | Type | SeatSelectionModeOverride | Description | DescriptionArabic
      | LiveStreamUrl | LiveSignLanguageUrl | LiveCaptions | LiveCaptionsArabic
      (the last eight appended by D-506 so they round-trip through import; blank when unset)
  And the Hall cell holds the hall *code*, the Category cell the category English name,
      and Start/End are ISO-8601 UTC strings (e.g. 2026-11-10T09:00:00Z)
  And Type / SeatSelectionModeOverride are written by their display name (Workshop/Session/Event,
      AssignedSeat/OpenSeating)
  And the speaker roster and theme set are NOT exported (M-to-M, omitted by design)
  When they instead select two rows then click "Export"
  Then the POST body carries those two Ids and the workbook contains exactly those two rows
```

### E2E-SES-023 — Excel import (D-356)

```gherkin
Scenario: Import sessions from a workbook and see the per-row outcome
  Given the administrator is on /admin/sessions
  And one active Hall with code "AUD-A" exists
  When they click the toolbar "Import" action (the file picker "sessions-import-input", accept=".xlsx", opens)
  And they choose an .xlsx whose "Sessions" sheet has the required headers
      Code | Title | TitleArabic | Hall | Start | End
      plus a Type column, with two new rows (Hall="AUD-A", valid ISO Start < End,
      Type="Event" — a non-Event row would also need a Speakers cell, see SES-046)
  Then a POST /account/api/admin/sessions/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped." (import is insert-only)
  And the grid reloads and a green toast reads the shared Grid.Import.Done text
  When they import a workbook where one row has a Hall code that no active hall matches
  Then that row appears in the per-row error list reading "No active hall with code '…' was found."
      and the others still import (one bad row never aborts the batch)
  When they import a row whose End is at/before Start
  Then that row errors with "The end time must be after the start time."
```

### E2E-SES-024 — Excel import rejection (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/sessions
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check)
  Then the API returns HTTP 400 and the page shows a bilingual error toast
      "The file is not a valid Excel workbook." / "الملف ليس مصنف Excel صالحًا."
  And no session is created
  When they import a file larger than 5 MB
  Then the API returns HTTP 413 "The Excel file is too large. The maximum is 5 MB."
  When they import a workbook whose sheet is not named "Sessions"
      (or is missing a required header from Code/Title/TitleArabic/Hall/Start/End)
  Then the parse rejects it with a bilingual error and nothing is created
```

### E2E-SES-026 — Excel round-trips the eight previously-dropped fields (D-506)

```gherkin
Scenario: The eight dropped fields survive an export → import round-trip
  Given the administrator is on /admin/sessions
  And one active Hall with code "AUD-A" exists
  When they import an .xlsx whose "Sessions" sheet adds the optional columns
      Type | SeatSelectionModeOverride | Description | DescriptionArabic
      | LiveStreamUrl | LiveSignLanguageUrl | LiveCaptions | LiveCaptionsArabic
      to the required headers, with one row carrying
      Type="Event", SeatSelectionModeOverride="OpenSeating",
      a bilingual Description, two valid YouTube live URLs and a bilingual caption
  Then the row is created and Type + SeatSelectionModeOverride appear on the grid summary
  And opening the new row in View/Edit shows the Description, the two live URLs and the captions
  And exporting that grid writes those eight values back into the same columns
  When a row carries Type="Bonfire" (not Workshop/Session/Event)
      or SeatSelectionModeOverride="Hovering" (not AssignedSeat/OpenSeating)
  Then that row errors per-row ("The type must be one of Workshop, Session or Event."
      / "The seat-selection mode must be one of AssignedSeat or OpenSeating.") and the others still import
  And a blank Type / SeatSelectionModeOverride leaves the field unset (null = inherit the hall)
```

### E2E-SES-027 — Import a subtitle file into the caption field (D-578)

```gherkin
Scenario: Importing an .srt file populates the caption field
  Given the Add/Edit modal is open below the AI-live-captions textareas
  And a "Get subtitle — import an .srt/.vtt file, or fetch it from the video below" control
  When the administrator picks a valid .srt file whose cues read
      "Good morning, and welcome." then "Today we discuss seabed security."
  Then the server parses it in-process (SubtitleParser) and fills the English caption
      field with "Good morning, and welcome. Today we discuss seabed security."
  And a success banner "Subtitle imported into the caption field above." appears
  And clicking "Create/Save" round-trips the caption text (SES-025 path)

Scenario: An Arabic-script subtitle file routes to the Arabic caption field
  When the administrator imports a .vtt whose text is predominantly Arabic
  Then the parsed transcript fills "AI live captions (Arabic)", not the English field
```

### E2E-SES-028 — Subtitle parsing strips VTT tags/timestamps (D-578)

```gherkin
Scenario: A WebVTT file is cleaned to running text
  When the administrator imports a .vtt containing the "WEBVTT" header, a NOTE block,
      cue timestamps "00:00:01.000 --> 00:00:04.000" and inline tags "<c>…</c>", "<v Speaker>"
  Then only the spoken text survives (header/notes/timestamps/tags removed),
      consecutive duplicate rolling-caption lines are collapsed
  And an empty / unreadable file shows "No readable text was found in that subtitle file."
  # Proven by SubtitleParserTests (srt, vtt, dedup, plain, empty).
```

### E2E-SES-029 — Fetch subtitle from the video, YouTube unreachable → graceful error (D-578)

```gherkin
Scenario: Fetch-from-video degrades cleanly where the server has no YouTube egress
  Given the Live stream URL is a valid YouTube watch URL
  When the administrator clicks "Fetch subtitle from video (YouTube)"
  And the API host cannot reach YouTube (the on-prem NCA network blocks it)
  Then the endpoint POST /admin/sessions/subtitle/fetch-from-video returns
      502 SUBTITLE_FETCH_FAILED
  And an error banner tells the admin the server may not reach YouTube —
      "paste or upload the subtitle instead"
  And the caption field is left unchanged
  # A non-YouTube URL → 400; a video with no captions → 422 (YoutubeTranscriptServiceTests).

Scenario: Fetch-from-video fills the caption field on an unblocked network
  Given a network that permits YouTube and a video that has captions
  When the administrator clicks "Fetch subtitle from video (YouTube)"
  Then the fetched transcript fills the caption field for the track's language
  And a success banner "Subtitle fetched from the video into the caption field above." appears
```

### E2E-SES-030 — Fetch with no Live stream URL → client guard (D-578)

```gherkin
Scenario: Fetching before a URL is entered is blocked client-side
  Given the Live stream URL field is blank
  When the administrator clicks "Fetch subtitle from video (YouTube)"
  Then no request is sent and the banner reads
      "Enter the Live stream URL above first, then fetch its subtitle."

Scenario: The subtitle tools are gated by Sessions.Edit
  Given the endpoint is Policies(Sessions.Edit + RequireApprovedAccount)
  Then an admin without Sessions.Edit is denied at the API (403), same as the CRUD gate
```

### E2E-SES-031 — Moderate row action opens the live Q&A desk (D-646)

```gherkin
Scenario: The Moderate row action navigates to the per-session moderation desk
  Given the administrator is on /admin/sessions with at least one session listed
  And the administrator holds Questions.Moderate (or the Administrator wildcard)
  When the administrator clicks the "Moderate live Q&A" (gavel) action on a session row
  Then the browser navigates to /sessions/{that session id}/moderate
  And the live Q&A moderation desk for that session loads

Scenario: The Moderate action is hidden for an admin without Questions.Moderate
  Given the administrator holds Sessions.View but not Questions.Moderate
  When the sessions grid renders
  Then the row shows Edit / Details / Deactivate but NOT the Moderate action
  # UX-only: the desk page + its API still enforce Questions.Moderate (403) if reached directly.
```

### E2E-SES-033/034 — Booking guard on delete (S-1)

```gherkin
Feature: A session with active visitor bookings cannot be deleted
  As an Administrator
  I want the delete to be blocked while visitors still hold seats
  So that a scheduled attendee is never silently orphaned

Scenario: Delete blocked while a visitor booking is held
  Given a session "SES-BOOK" with one active (held) visitor booking
  When the administrator opens View/Delete and confirms Deactivate
  Then the DELETE /account/api/admin/sessions/{id} returns 409
  And the error code is "SESSION_HAS_ACTIVE_BOOKINGS"
  And a red toast reads "This session has 1 active booking(s) — cancel or reject
      them before deleting it." (Arabic mirror in RTL)
  And the row keeps its green "Active" pill

Scenario: Delete allowed when only an admin row-block remains
  Given a session "SES-BLK" whose only reservation is an admin row-block (no attendee)
  When the administrator confirms Deactivate
  Then the DELETE returns 200 and the row becomes Inactive
```

### E2E-SES-035/036 — Hall / time edit releases held seats (S-1)

```gherkin
Feature: Moving a session frees the seats it can no longer honour
  As an Administrator
  I want a hall move or a reschedule to release the held seats and notify attendees
  So that stale seat holds do not block a re-configured hall

Scenario: Change the hall — held seats are released and attendees notified
  Given a session "SES-MOVE" in "Auditorium A" with two held visitor bookings
  When the administrator edits it to "Auditorium B" and saves
  Then the PUT returns 200
  And every held reservation is released (ReleasedAt set, Status = Cancelled)
  And each affected visitor receives a "Seat reservation released" notification
      (kind BookingRejected, session deep-link)

Scenario: Change only the start/end window — held seats are released
  Given a session "SES-TIME" with a held visitor booking
  When the administrator shifts Start/End by two hours and saves
  Then the held reservation is released

Scenario: A title-only edit keeps the seats
  Given a session "SES-KEEP" with a held visitor booking
  When the administrator edits only the Title and saves (same hall + window)
  Then the reservation is untouched and no release notification is sent
```

### E2E-SES-037 — Capacity override below held seats (S-1)

```gherkin
Feature: Capacity cannot be shrunk below the seats already held
Scenario: Override below the held count is rejected
  Given a session with two held seats (same hall + window unchanged)
  When the administrator sets Capacity override = 1 and saves
  Then the PUT returns 409 with code "SESSION_CAPACITY_BELOW_BOOKINGS"

Scenario: A hall move that lowers the override is allowed
  Given the same session with two held seats
  When the administrator moves it to another hall AND sets Capacity override = 1
  Then the PUT returns 200 (the held seats are cascade-released by the move)
```

### E2E-SES-038/039 — Same-hall time-overlap guard (S-2)

```gherkin
Feature: Two active sessions must not occupy one hall at overlapping times
Scenario: Overlapping create is rejected
  Given an active session in "Auditorium A" from 09:00 to 10:00
  When the administrator creates another in "Auditorium A" from 09:30 to 10:30
  Then the POST returns 409 with code "SESSION_HALL_TIME_OVERLAP"

Scenario: Exact back-to-back in the same hall is allowed
  Given an active session in "Auditorium A" from 09:00 to 10:00
  When the administrator creates another in "Auditorium A" from 10:00 to 11:00
  Then the POST returns 200 (half-open comparison)

Scenario: Different hall / soft-deleted sibling never conflict
  Given the 09:00-10:00 session in "Auditorium A"
  Then creating an overlapping session in "Auditorium B" succeeds
  And creating one in "Auditorium A" after the first is soft-deleted succeeds

Scenario: Move into an occupied slot is rejected; a legacy title edit still saves
  Given two overlapping active sessions already share "Auditorium A" (legacy data)
  When the administrator edits only the Title of one and saves
  Then the PUT returns 200 (the overlap check runs only when the hall/time moves)
  When the administrator moves a session into "Auditorium A" at an occupied time
  Then the PUT returns 409 with code "SESSION_HALL_TIME_OVERLAP"
```

### E2E-SES-040/041 — Lifecycle clock + recording guards (S-7)

```gherkin
Feature: Status moves are guarded by the clock and by the recording
Scenario: Cannot mark Held before the session has started
  Given a session whose Start is in the future
  When the administrator clicks "Mark Held"
  Then the PUT /status returns 400 with code "SESSION_STATUS_GUARD_FAILED"
  And after the start time has passed the same move succeeds

Scenario: Cannot mark Recorded/Published without a recording
  Given a started session with no recording attached
  When the administrator marks it Held then tries "Mark Recorded"
  Then the PUT /status returns 400 with code "SESSION_STATUS_GUARD_FAILED"
  When a recording is uploaded first
  Then the Held → Recorded move is allowed
  And a reverse move (Recorded → Held) carries no guard
```

### E2E-SES-042 — Session language round-trips (Website "at a glance")

```gherkin
Feature: The bilingual session-language label edits and round-trips
Scenario: Set and re-read the language label
  Given the administrator opens the session editor (Add or Edit)
  When they fill Language (English) "English & Arabic" and Language (Arabic) "الإنجليزية والعربية" and save
  Then the PUT/POST succeeds and re-opening the session shows both language values
  And the public GET /api/v1/app/programme/sessions/{id} returns Language + LanguageArabic
  And the Website /sessions/{id} "at a glance" card shows the language row

Scenario: Language over the cap is rejected
  Given a Language value longer than 64 characters
  When the administrator saves
  Then the API returns 400 with code "SESSION_INVALID" ("… 64 characters or fewer")
  And nothing is persisted
```

### E2E-SES-043 — Key outcomes add / edit / reorder / remove ("أبرز المخرجات")

```gherkin
Feature: The session's key-outcome bullets are a repeatable bilingual list
Scenario: Add, reorder and persist outcomes
  Given the administrator opens the session editor
  When they click "Add key outcome" twice and fill each row's English + Arabic text
  And reorder them with the Up/Down buttons and save
  Then the outcomes persist renumbered 0..n-1 in the shown order
  And re-opening the session lists them in that order
  And the public GET returns PublicSessionDetail.Outcomes in order
  And the Website /sessions/{id} "key outcomes" checklist renders them

Scenario: An outcome missing one language is rejected
  Given an outcome row with English text but a blank Arabic text
  When the administrator saves
  Then the API returns 400 with code "SESSION_INVALID" ("… both English and Arabic text")

Scenario: An entirely-blank outcome row is dropped, not sent
  Given an added outcome row left completely empty
  When the administrator saves
  Then that row is omitted from the request (no 400) and no empty outcome is persisted

Scenario: Removing all outcomes clears them
  Given a session with two saved outcomes
  When the administrator removes both and saves
  Then the update deletes the SessionOutcome rows (RemoveRange re-sync) and the public read returns none
```

### E2E-SES-044 — Required session Type (#3)

```gherkin
Feature: A session must declare its type (Workshop / Session / Event)
Scenario: Creating a session with no type is rejected
  Given the Add form is open with a valid Code, Title, Hall, Start and End
  And the Type select shows a required marker ("Type *") with a "— No type —" placeholder
  When they leave Type unselected and click "Create session"
  Then a bilingual SimfAlert reads "A session type is required (Workshop, Session or Event)."
      / "نوع الجلسة مطلوب (ورشة عمل أو جلسة أو حدث)." and no POST fires (client guard)
  And were it to reach the API it would 400 with code "SESSION_TYPE_REQUIRED"

Scenario: A legacy untyped session still saves an unrelated edit (grandfathered)
  Given a session that predates the rule has Type = null (seeded straight to the DB)
  When the administrator edits only its Title and saves (leaving Type unselected)
  Then the PUT returns 200 — a pre-existing untyped row is not forced to acquire a type

Scenario: Clearing a type that was set is rejected (no-regression)
  Given a session that already has Type = Event
  When a PUT arrives with Type = null (the type cleared)
  Then the API returns 400 with code "SESSION_TYPE_REQUIRED"
```

### E2E-SES-045 — Min-1 speaker unless Event (#4)

```gherkin
Feature: A non-Event session needs at least one speaker; an Event may have none
Scenario: A Workshop/Session with no speaker is rejected
  Given the Add form is open with Type = "Session" and an empty speaker roster
  When they click "Create session"
  Then a bilingual SimfAlert reads "A non-event session must have at least one speaker."
      / "يجب أن يكون للجلسة (غير الحدث) متحدّث واحد على الأقل." and no POST fires (client guard)
  And were it to reach the API it would 400 with code "SESSION_SPEAKER_REQUIRED"

Scenario: An Event saves with no speaker
  Given the Add form is open with Type = "Event" and no speakers
  When they click "Create session"
  Then the API returns 200 (an opening ceremony etc. legitimately has no speaker)

Scenario: A legacy speakerless non-Event session still saves (grandfathered)
  Given a Session that predates the rule has no speakers (seeded straight to the DB)
  When the administrator edits only its Title and saves
  Then the PUT returns 200 — a pre-existing speakerless row is not forced to gain one

Scenario: Dropping the last speaker of a non-Event is rejected (no-regression)
  Given a Session that currently has one speaker
  When the administrator removes it and saves (Type still "Session")
  Then the API returns 400 with code "SESSION_SPEAKER_REQUIRED"
```

### E2E-SES-046 — Excel import Speakers column (#3/#4)

```gherkin
Feature: The Sessions import reads an optional Speakers column and enforces the type/speaker rules
Scenario: A Speakers cell of codes attaches the roster in order
  Given two active speakers with codes "SPK-A" and "SPK-B" exist
  When they import a workbook whose row has Type="Session" and Speakers="SPK-A, SPK-B"
  Then the row is created and the session's roster is [SPK-A (order 0), SPK-B (order 1)]
      (position sets the display order; every entry takes the default Speaker role)

Scenario: A non-Event row with no speakers is a per-row error
  Given an import row with Type="Session" and a blank Speakers cell
  Then that row errors per-row ("A non-event session must have at least one speaker.")
      and any valid rows still import

Scenario: An unknown speaker code is a per-row error
  Given an import row with Speakers="NO-SUCH-SPK"
  Then that row errors per-row ("No active speaker with code 'NO-SUCH-SPK' was found.")

Scenario: A blank Type is a per-row error
  Given an import row with a blank Type cell (even with a valid speaker)
  Then that row errors per-row ("A session type is required (Workshop, Session or Event).")
# The export still omits the Speakers column; the import is insert-only, so there is
# no export→import round-trip for the roster.
```

### E2E-SES-047 / 048 / 049 — Moving the hall or the time destroys the room, loudly

```gherkin
Feature: A hall or time change never silently cancels the registrations
  As an Administrator
  I want to be told, with a number, what a reschedule is about to destroy
  So that nudging an end time by 30 minutes cannot wipe a booked room unnoticed

Background:
  Given an Administrator has signed in and landed on /admin/sessions
  And an active session "Future of Naval Logistics" (code "SES-001") runs
      2026-11-10 09:00 AM - 10:30 AM in "Auditorium A"
  And 12 approved attendees hold a seat for it
  And an administrator has blocked row "VIP" (3 seats) on its seat plan

Scenario: The warning names the real counts before anything is saved
  When the administrator opens Edit on SES-001
  And they change End (Saudi time) to "2026-11-10T11:00"
  And they click "Save changes"
  Then NO request is sent yet
  And a must-decide dialog opens titled "This change releases every held seat"
  And its message reads "... 12 attendee registration(s) and 3 admin-reserved row block(s) will be released ..."
  And its buttons are "Release and save" (danger) and "Cancel"
  When they click "Cancel"
  Then the dialog closes, no request was sent, and the form still shows End="2026-11-10T11:00"

Scenario: Confirming reports what was destroyed
  When they click "Save changes" again and then "Release and save"
  Then the BFF PUTs /account/api/admin/sessions/{id} and the API returns 200
  And the response carries releasedReservationCount=12 and releasedAdminBlockCount=3
  And the toast reads 'Session "Future of Naval Logistics" was updated. 12 attendee
      registration(s) and 3 admin-reserved row block(s) were released - the attendees
      were notified; re-create the row blocks.'
  And an OperationLog row exists with EventType="SeatReservation.Released" and Detail
      containing "reason=Rescheduled; reservations=12; adminBlocks=3"

Scenario: An edit that leaves the slot alone never warns and never releases
  When the administrator opens Edit and changes only Title (English)
  And they click "Save changes"
  Then no dialog opens, the save returns 200 with both released counts 0
  And the 12 seats are still held and no SeatReservation.Released row is written

Scenario: The attendee is told by app AND email, in local time
  Given attendee "visitor@simf.test" held one of the released seats
  Then they have an in-app notification of kind BookingRejected
  And its English body quotes the new start as "10-11-2026 09:00 AM" (Saudi wall clock)
  And its Arabic body carries the same, and neither body contains "UTC"
  And an email addressed to visitor@simf.test was queued
```

### E2E-SES-050 / 051 / 052 — Reschedule re-arms the reminder; cancellation tells people

```gherkin
Feature: A moved session is still remindable, and a cancelled one is announced

Scenario: Moving the window clears the worker stamps (A4)
  Given session SES-001 has already had its "starting soon" reminder sent
      (Session.ReminderSent is stamped) and its rating prompt sent
  When the administrator moves Start/End by 3 hours and saves
  Then Session.ReminderSent and Session.RatingPromptSent are both null again
  And SessionReminderWorker picks the session up for the NEW start time

Scenario: An unrelated save does not resend an already-delivered reminder (A4)
  Given the same stamped session
  When the administrator edits only the Title and saves
  Then both stamps are unchanged and no second reminder is dispatched

Scenario: The booking conflict names a page that can do the work (A5)
  Given session SES-001 has 12 active attendee bookings
  When the administrator clicks Deactivate and confirms
  Then the API returns 409 SESSION_HAS_ACTIVE_BOOKINGS
  And the bilingual message names "/admin/sessions/seat-plans"
  And it does NOT name "/admin/bookings" (a read-only monitor with no row actions)
  And the session is still Active

Scenario: Cancelling a session finally tells the attendees (B2)
  Given session SES-002 has no active bookings
  And attendee "saver@simf.test" has favourited it
  When the administrator clicks Deactivate and confirms
  Then the API returns 200 and the session leaves the public agenda
  And saver@simf.test has an in-app notification of kind SessionCancelled
      whose body names the session and its Saudi-wall-clock start, with no "UTC"
  And an email addressed to saver@simf.test was queued
  And the Session.Deactivated OperationLog row carries "notified=1"

Scenario: A session nobody booked or saved is cancelled quietly
  Given session SES-003 has no bookings and no favourites
  When the administrator deactivates it
  Then no SessionCancelled notification is written for it
```

### E2E-SES-053 — The edit form's Active checkbox is the same cancellation

```gherkin
Feature: Unticking Active cancels a session exactly as Deactivate does

Scenario: Clearing Active on the edit form announces the cancellation (B2)
  Given session SES-004 has no active bookings
  And attendee "saver@simf.test" has favourited it
  When the administrator opens Edit on SES-004
  And they untick "Active"
  And they click "Save changes"
  Then the API returns 200 and the session leaves the public agenda
  And saver@simf.test has an in-app notification of kind SessionCancelled
      whose body names the session and its Saudi-wall-clock start, with no "UTC"
  And an email addressed to saver@simf.test was queued
  And a Session.Deactivated OperationLog row carries "notified=1"
      (alongside the ordinary Session.Updated row)

Scenario: An ordinary edit that leaves Active ticked announces nothing
  Given the same favourited session, still Active
  When the administrator edits only the Title and saves
  Then no SessionCancelled notification is written for it
  And no Session.Deactivated OperationLog row is written for it
```

### E2E-SES-054 / 055 / 056 — FR-702: authoring the session's live notice (D-815)

```gherkin
Feature: Sessions — the live notice shown WITH the broadcast (FR-702)
  As an administrator holding Sessions.Edit
  I want to write a note that appears beside a session's live stream
  So that the audience is informed — without anyone being blocked from watching

Background:
  Given the administrator is signed in at /admin/sessions
  And the Add/Edit form's broadcast block shows, under the two stream URLs:
      "Live notice — shown with the stream (English)"  (textarea, 2 rows, MaxLength 512)
      "Live notice — shown with the stream (Arabic)"   (textarea, 2 rows, MaxLength 512)
  And the English field's helper reads "Optional note displayed beside the live
      stream, in the viewer's language. It is information only — it blocks nobody
      and the stream stays available to everyone, wherever they are. Leave both
      languages blank to show no notice."

Scenario: Author a bilingual notice on a new session
  When the administrator creates session Code "S-104", Title "Maritime security",
       Type "Session", a hall, a start/end window and one speaker
  And sets Live stream URL to "https://www.youtube.com/watch?v=simfsimfsim"
  And sets the English notice to "This broadcast is provided by the forum organisers."
  And sets the Arabic notice to "يقدَّم هذا البث من منظمي الملتقى."
  And saves
  Then the save succeeds and the grid shows "S-104"
  When the administrator reopens "S-104" in the edit form
  Then both notice textareas are pre-filled with exactly what was typed
  And GET /account/api/admin/sessions/{id} returns liveNotice + liveNoticeArabic
  And the anonymous GET /api/v1/app/programme/sessions/{id} returns the same pair
       alongside an unchanged liveStreamUrl

Scenario: The notice never withholds the broadcast
  Given "S-104" carries both a live stream URL and a notice
  When an anonymous caller reads /api/v1/app/programme/sessions/{id}
       with no Authorization header and no location of any kind
  Then liveStreamUrl is returned in full, exactly as it is for a session with no notice
  And no response field, header or status expresses a region, eligibility or restriction
  # FR-702 was re-scoped by the owner on 2026-07-31: notification only, no gate.

Scenario: Clear a notice that is no longer wanted
  Given "S-104" is showing its notice on the app live screen and the Website page
  When the administrator empties BOTH notice textareas and saves
  Then the update succeeds
  And the reloaded form shows both fields empty
  And the API returns liveNotice = null and liveNoticeArabic = null
  And the banner is gone from the app live screen and /sessions/{id}
  And the live stream URLs are untouched by the clear

Scenario: One language only is a valid notice
  When the administrator writes only the Arabic notice and saves
  Then the save succeeds
  And both an Arabic and an English viewer see the Arabic text (the shared fallback)

Scenario: Over-length notice is a clean bilingual 400
  When 513 characters are posted into the English notice past the MaxLength guard
  Then the API returns 400 with error code SESSION_INVALID
  And message      "The live notice must be 512 characters or fewer."
  And messageArabic "يجب أن يكون إشعار البث المباشر 512 حرفاً أو أقل."
  When 513 characters are posted into the Arabic notice on update
  Then the API returns 400 SESSION_INVALID with
       "The Arabic live notice must be 512 characters or fewer." /
       "يجب أن يكون الإشعار العربي للبث المباشر 512 حرفاً أو أقل."
  When exactly 512 characters are submitted
  Then the save succeeds and the value round-trips whole
```

> **Author's note for whoever drives this.** There is nothing here to "unlock"
> and no restricted state to reach. FR-702 was written in SIMF-FDS-007 §5.1 as a
> Riyadh-region restriction and the owner reversed it on 2026-07-31 (D-815):
> the field is free bilingual text and the stream is served to everyone either
> way. If a run produces a session whose stream is withheld from anyone, that is
> a defect to report, not a scenario to pass.

**Evidence:** form fields `Admin.Sessions.Field.LiveNotice` /
`…LiveNoticeArabic` / `…LiveNoticeHint` (both resx files) bound to
`_model.LiveNotice` / `.LiveNoticeArabic` in `SessionsAddEdit.razor`, sent
through `NullIfBlank` on **both** create and update — which is what makes a
cleared box store `null`. Server: `AdminSessionService.ValidateTextLengths`
enforces 512 against the `SessionConfiguration` `HasMaxLength(512)` columns, and
`UpdateSessionRequest` carries the pair so a PUT round-trips instead of wiping it
(the trap D-439 fixed for the live URLs). API suite
`tests/SIMF.Api.Tests/SessionLiveNoticeTests.cs` (9 facts); CP form suite
`tests/SIMF.ControlPanel.Tests/SessionsAddEditLiveNoticeTests.cs` (5 facts —
`Add_mode_renders_both_notice_boxes_capped_at_512`,
`Edit_mode_loads_the_stored_notice_into_both_boxes`,
`Add_mode_posts_the_typed_notice`, `Edit_mode_puts_the_edited_notice`,
`Emptying_both_boxes_clears_the_notice`). Reader-side coverage:
`mobile-live.md` E2E-MOB025-026..028 and `web-session-detail.md`
E2E-WSDT-014..016.

---

## Implementation notes

- **Lower-layer API coverage already exists.** These xUnit + WebApplicationFactory
  suites cover the same surface without a browser, and should be kept in sync /
  retired as E2E coverage lands:
  - `tests/SIMF.Api.Tests/AdminSessionsTests.cs` — CRUD, duplicate-code (409),
    time-window (400), hall-not-found (400), speaker/theme link validation,
    live-URL validation (D-349 — YouTube/HLS accepted, other → 400 SESSION_INVALID),
    plus the #3 required-type (400 SESSION_TYPE_REQUIRED) and #4 min-1-speaker-unless-Event
    (400 SESSION_SPEAKER_REQUIRED) rules with no-regression grandfathering on edit.
  - `tests/SIMF.Api.Tests/SessionLifecycleTests.cs` — the `Scheduled → Held →
    Recorded → Published` transition matrix + the `SESSION_STATUS_TRANSITION_INVALID`
    rejections (P3.2a / D-231).
  - `tests/SIMF.Api.Tests/SessionRecordingTests.cs` — recording upload/remove,
    the video allow-list and the `SESSION_RECORDING_INVALID` rejection (D-232).
  - Permission enforcement is asserted by `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`
    and `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` (the nav
    item's `RequiredPermission = Sessions.View`).
  - `tests/SIMF.Api.Tests/SessionsExcelTests.cs` — the D-356 Excel export/import
    surface (the `Sessions` sheet column layout, hall-code / category-name
    resolution, the insert-only import, the per-row error aggregation, the
    `Sessions.Export` / `Sessions.Import` permission gates and the
    ZIP-magic / 5 MB / 5000-row upload defence), plus the D-506 round-trip of the
    eight previously-dropped fields (`Export_includes_the_dropped_round_trip_columns`
    + `Import_round_trips_the_dropped_fields`), and the #3/#4 rules at the import
    boundary — the optional Speakers column resolved by speaker code
    (`Import_attaches_the_speakers_column_in_order`) plus the required-type and
    min-1-speaker per-row errors.
- **Manual smoke is canonical today.** Until Playwright is adopted, run these
  scenarios as a Chrome DevTools MCP session: sign in per the Auth setup, walk
  each scenario, and capture screenshots into `docs/screenshots/cp-admin-sessions-*.png`.
- **Convert to Playwright** when the runner is adopted: each Gherkin block maps to
  a `.feature` scenario under `tests/SIMF.E2E.Tests/` (project to be created) plus
  a step-definition class. The Gherkin is already runner-agnostic.
- **Two permissions, two gates.** Note the split: CRUD = `Sessions.Create/Edit/Delete`,
  broadcast lifecycle + recording = `Sessions.Publish`. E2E-SES-009 is the load-bearing
  proof that the `Sessions.Publish` UI controls are hidden AND the endpoint denies a
  caller without that code.

---

_Last reviewed:_ 2026-07-11 by Claude (on-site ops — booking guards on delete/edit + capacity (S-1), same-hall time-overlap guard (S-2), lifecycle clock + recording guards (S-7): SES-033..041).

_Last reviewed:_ 2026-07-22 by Claude (#3 required session Type + #4 min-1-speaker-unless-Event with no-regression grandfathering on edit, and the Excel-import Speakers column: SES-044..046).

_Last reviewed:_ 2026-07-26 by Claude (session-lifecycle QA package — A1/A6 seat-release confirmation + counts + audit, A2 release email, A4 reminder re-arm, A5 corrected conflict copy, B2 session-cancelled notice: SES-047..052; covered by `tests/SIMF.Api.Tests/SessionLifecycleNoticeTests.cs`).

_Last reviewed:_ 2026-07-27 by Claude (B2 completed on the second cancellation path — clearing the Active checkbox on the edit form now runs the same announce step as Deactivate: SES-053).

_Last reviewed:_ 2026-07-31 by Claude (FR-702 re-scoped by the owner from a Riyadh-region restriction to an informational per-session live notice — bilingual free text ≤512, authored on the broadcast block, shown WITH the stream and gating nothing: SES-054..056; covered by `tests/SIMF.Api.Tests/SessionLiveNoticeTests.cs`; decision D-815).

## D-839 — per-session arrival-grace override

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-SES-057 | A session's **Arrival grace (minutes)** override opens a session its hall would refuse; blank inherits the hall | happy | P0 | authored ✓ (`ArrivalGraceResolutionTests.A_session_override_opens_a_session_its_hall_would_refuse`) |
| E2E-SES-058 | A session override of **0** overrules a wide hall grace — a deliberate zero is not read as "inherit" | validation | P0 | authored ✓ (`ArrivalGraceResolutionTests.A_session_override_of_zero_beats_a_wide_hall_grace`) |
| E2E-SES-059 | The admin session row reports the **resolved** grace the door will use, so the Hall-Arrivals picker and the server agree | happy | P0 | authored ✓ (`ArrivalGraceResolutionTests.The_admin_session_row_reports_the_grace_the_door_will_use`) |
| E2E-SES-060 | Editing a session **saves** its arrival-grace override, and an existing override survives an edit that does not change it | happy | P0 | authored ✓ (`ArrivalGraceResolutionTests.An_edit_can_set_an_override_and_the_door_honours_it`, `.An_override_survives_an_edit_that_does_not_change_it`) |

### E2E-SES-057 — one keynote opens early, the hall is untouched

```gherkin
Feature: A session widens its own arrival window
  As an Administrator
  I want one session to open its doors earlier than the rest of its hall's day
  So that a keynote can pre-scan a queue without changing every other session

  Background:
    Given I am signed in to the Control Panel as an Administrator
    And a hall "GR-MAIN" has no arrival grace of its own (it inherits the global 15)
    And a session "GRS-KEYNOTE" in that hall starts in 40 minutes

  Scenario: the override admits what the hall would refuse
    When an operator scans an approved visitor at the hall door
    Then the API responds 409 with error code SESSION_NOT_LIVE

    When I open /admin/sessions, edit "GRS-KEYNOTE"
    And I set "Arrival grace (minutes)" to 60
    And I save
    Then the session is saved

    When the operator scans the same badge again
    Then the API responds 200 and the attendee is marked arrived

  Scenario: blank inherits, and the form says what it is inheriting
    Given the hall "GR-MAIN" has "Arrival grace (minutes)" set to 45
    When I open the edit form for a session in that hall with a blank override
    Then the helper under the field reads
         "Leave blank to inherit the hall, which is currently 45 minutes. ..."
         / "اتركه فارغاً لتوريث قيمة القاعة، وهي حالياً 45 دقيقة. ..."
    # Read off AdminSessionDetail.EffectiveArrivalGraceMinutes, so the admin sees
    # which layer is actually in force instead of guessing.
```

### E2E-SES-058 — a session zero beats a wide hall

```gherkin
Feature: A per-session zero overrules its hall
  As an Administrator running one strict session in a permissive hall
  I want 0 on the session to win
  So that "this one closes on time" is expressible

  Scenario: the session's 0 refuses what the hall's 60 would admit
    Given a hall has "Arrival grace (minutes)" set to 60
    And a session in that hall has its override set to 0
    And that session starts in 40 minutes
    When an operator scans an approved visitor at the hall door
    Then the API responds 409 with error code SESSION_NOT_LIVE
    # This is the case that catches the natural mistake: reading the override
    # with a truthiness or "> 0" test treats a deliberate 0 as "inherit".
```

### E2E-SES-059 — the console and the door agree

```gherkin
Feature: The admin session row reports the grace the door will actually use
  As an operator on the Hall-Arrivals console
  I want the session picker to list exactly what the server will admit
  So that I am not refused a session the server would have accepted

  Scenario: the resolved value rides the session list
    Given a hall has "Arrival grace (minutes)" set to 60
    And session A in that hall has no override
    And session B in that hall has its override set to 5
    When the Control Panel loads POST /admin/sessions/list
    Then session A reports effectiveArrivalGraceMinutes = 60
    And session A reports arrivalGraceMinutesOverride = null
    And session B reports effectiveArrivalGraceMinutes = 5
    And session B reports arrivalGraceMinutesOverride = 5
    # The raw override round-trips for the Excel lane; the resolved value is
    # deliberately NOT exported, because a round-trip would pin what a session
    # merely inherits onto it as an override.
```

### E2E-SES-060 — the edit actually saves, and does not wipe itself later

```gherkin
Feature: A session's arrival-grace override survives being edited
  As an Administrator
  I want the override I type to be stored, and to still be there after I edit
    something else on the same session
  So that a hall does not quietly narrow its doors back to the default

  Background:
    Given I am signed in to the Control Panel as an Administrator
    And a hall "GR-MAIN" has no arrival grace of its own (it inherits the global 15)

  Scenario: setting the override on an existing session reaches the door
    Given a session "GRS-KEYNOTE" in that hall starts in 40 minutes
    And it has no arrival-grace override
    And an approved visitor scanned at that hall door is refused SESSION_NOT_LIVE
    When I open the session, set "Arrival grace (minutes)" to 60 and save
    Then the save succeeds
    And the same visitor scanned at that hall door is admitted
    # Asserted through the DOOR, not the stored row. Before D-842 the Control
    # Panel showed "Session ... was updated" and the column stayed NULL, because
    # the API's own route DTO omitted the field and the PUT bound it to null.

  Scenario: an existing override is not wiped by an unrelated edit
    Given a session "GRS-KEYNOTE" already has its override set to 60
    When I open the session, change nothing but the title, and save
    Then the session still reports arrivalGraceMinutesOverride = 60
    # The worse half of the same defect: the form loads the stored value and
    # echoes it back on save, so before D-842 ANY edit reset it to null with no
    # error, no toast and nothing in the audit trail.
```

_Last reviewed:_ 2026-08-04 by Claude (D-842 — the PUT round-trip the D-839 field was missing; E2E-SES-060).
