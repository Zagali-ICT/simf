# E2E test catalogue — Invitations desk (`/admin/invitations`)

| | |
|--|--|
| **Page** | [`cp/admin-invitations.md`](../../pages/cp/admin-invitations.md) |
| **Route** | `/admin/invitations` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page facts (grounded in `InvitationsList.razor`, post D-256 SimfDataGrid migration; D-353 CrudShell framing + D-356 Excel export):**
> - Page permission gate: `@attribute [RequirePermission(PermissionCatalog.Invitations.View)]` (`"Invitations.View"`).
>   Baseline grant = **PublicRelations** role (and Administrator via the `"*"` wildcard).
> - Write endpoints (`POST`/`PUT`/`DELETE`) are gated by `PermissionCatalog.Invitations.Manage` (`"Invitations.Manage"`)
>   **and** `RequireApprovedAccount`, and carry the `"auth"` rate-limit policy.
> - BFF passthroughs the page calls (forward the `access_token` JWT to the API):
>   - `POST /account/api/admin/invitations/list` → API `POST /admin/invitations/list`
>   - `GET  /account/api/admin/invitations/{id}` → API `GET  /admin/invitations/{id}`
>   - `POST /account/api/admin/invitations` → API `POST /admin/invitations`
>   - `PUT  /account/api/admin/invitations/{id}` → API `PUT  /admin/invitations/{id}`
>   - `DELETE /account/api/admin/invitations/{id}` → API `DELETE /admin/invitations/{id}`
> - **D-353 framing (current):** Add / Edit / View / Delete are no longer inline `SimfModal`s. The page hosts a
>   single `<CrudShell Presentation="_presentation" …>` that frames the reusable **`InvitationsAddEdit`** and
>   **`InvitationsViewDelete`** forms as a popup (default) or a full page per the admin's toolbar toggle
>   (`<CrudPresentationToggle PageKey="invitations" @bind-Value="_presentation" />`, persisted in localStorage
>   via `CpPreferences` — key `simf.cp.prefs.invitations`, default `Dialog`). The two old inline `SimfModal`s
>   (Send + Edit) are gone.
> - The **Add** form (`InvitationsAddEdit` with `IsEdit=false`, title "Send invitation") collects exactly two
>   fields: **Recipient (UserProfile id)** (a free-text GUID — `_model.RecipientId`) and **Notes**
>   (`_model.Notes`). The form parses the GUID **client-side** (`Guid.TryParse`) before posting; a bad GUID shows
>   the `Admin.Invitations.RecipientRequired` SimfAlert in the form and fires **no** POST.
> - The **Edit** form (`InvitationsAddEdit` with `IsEdit=true`, title "Edit invitation") swaps the Recipient field
>   for a **State** `SimfSelect` (`Pending` / `Confirmed` / `Declined`, from the `InvitationState` enum) plus
>   **Notes**. It is opened by re-fetching the row via `GET .../{id}` first (`LoadDetailAsync`).
> - The **View** form (`InvitationsViewDelete` with `IsDelete=false`, title "Invitation details") shows the full
>   read-only `dl` (recipient EN/AR name, profile type, job title, email, state, notes, sent-by, created/responded/
>   updated timestamps, active flag) and only a "Close" button.
> - The **Delete** form (`InvitationsViewDelete` with `IsDelete=true`, title "Cancel invitation") shows the same
>   read-only `dl` plus a danger "Cancel invitation" button that opens a **`SimfConfirm`** dialog
>   (`Danger="true"`, message `Admin.Invitations.Delete.Message` naming the recipient); only confirming fires the
>   `DELETE`. This replaces the earlier native-confirm/no-confirm behaviour.
> - Each grid row carries quiet **icon** actions: **Edit** (pencil, `OnEditOne` → `OnEditAsync`), **Details**
>   (`OnDetailsOne` → `OnDetailsAsync`), and **Delete / Cancel invitation** (trash, `OnDeleteOne` →
>   `OnDeleteAsync`). Delete now opens the ViewDelete form's SimfConfirm gate rather than deleting directly.
> - **D-356 Excel export (export only):** the grid wires `OnExport="OnExportAsync"`; `OnExportAsync` direct-downloads
>   via `simfAccount.downloadXlsx` to `/account/api/admin/invitations/export`, posting
>   `AdminGridExportRequest { Ids, Query }` (Ids = the selected rows; Query = the current grid query only when no
>   rows are selected). **There is no import** — invitations are created/edited from the forms, so the page wires no
>   `OnImport` and renders no import `<input>`.
> - Grid columns (`SimfDataGrid`): Recipient (`recipient`), Profile type (`profileType`), State (`state`),
>   Sent (`createdat`, `yyyy-MM-dd HH:mm 'UTC'`), Sent by (`sentBy`), Active (`active`), plus the row-actions column.
> - **Grid now renders per-column filter + sort + pager** (post D-256). `Filterable="true"` is set on **State**
>   (`state`) only; `Sortable="true"` is set on **State** (`state`) and **Sent** (`createdat`). The grid pages at
>   `Top = 20` (`_query = new() { Top = 20 }`) with Prev/Next/First/Last + page-size controls and a summary line.
>   The backend (`AdminInvitationService.ListAllAsync`) honours `Filters["state"]` (parsed as `InvitationState`,
>   case-insensitive) and `Sort` keys `state` / `createdat` with `SortDescending`. Add (`OnAdd`) is the grid
>   toolbar's "New invitation" button. The `isActive` filter is honoured by the service but no UI control drives it.
> - Server error codes (from `AdminInvitationService` / `InvitationEndpoints`):
>   - `INVITATION_TARGET_NOT_FOUND` (400) — recipient `UserProfile` id does not exist
>   - `INVITATION_NOT_FOUND` (404) — invitation id does not exist (GET / PUT / DELETE)
>   - `INVITATION_STATE_INVALID` (400) — moving a settled invitation back to `Pending`
>   - `INVITATION_INVALID` (400) — notes longer than 1000 chars
> - Audit event keys: `Invitation.Created`, `Invitation.Updated`, `Invitation.StateChanged`, `Invitation.Deactivated`.
> - On create, an in-app notification (`NotificationKind.InvitationReceived`) is best-effort dispatched to the recipient.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-INV-001 | Full round-trip — Send → Edit/confirm state → Cancel invitation | happy | P0 | _to author_ |
| E2E-INV-002 | Send invitation (Add modal) with a valid recipient + notes | happy | P1 | _to author_ |
| E2E-INV-003 | Edit invitation — change State Pending → Confirmed via the Edit modal | happy | P1 | _to author_ |
| E2E-INV-004 | Cancel invitation (soft delete) flips the row's Active column to Inactive | happy | P1 | _to author_ |
| E2E-INV-005 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-INV-006 | Auth gate — signed-in admin lacking `Invitations.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-INV-007 | Validation — non-GUID recipient id is rejected client-side (no POST fires) | error | P1 | _to author_ |
| E2E-INV-008 | Target not found — valid GUID, no such profile → 400 `INVITATION_TARGET_NOT_FOUND` | error | P1 | _to author_ |
| E2E-INV-009 | Illegal transition — settled invitation back to `Pending` → 400 `INVITATION_STATE_INVALID` | error | P1 | _to author_ |
| E2E-INV-010 | Notes too long (>1000 chars) → 400 `INVITATION_INVALID` | error | P2 | _to author_ |
| E2E-INV-011 | Server 500 on `/list` → bilingual fallback toast, no rows render | resilience | P2 | _to author_ |
| E2E-INV-012 | RTL / Arabic render mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-INV-013 | Per-column filter (State) narrows the grid | grid | P1 | _to author_ |
| E2E-INV-014 | Column sort toggles (State / Sent) | grid | P2 | _to author_ |
| E2E-INV-015 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-INV-016 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-INV-017 | Delete confirmation: Cancel invitation opens View/Delete → SimfConfirm gates the DELETE (D-353) | error | P0 | _to author_ |
| E2E-INV-018 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |

## Scenarios

### E2E-INV-001 — Full round-trip (golden path)

```gherkin
Feature: Invitations desk round-trip
  As a Public Relations administrator
  I want to send an invitation, settle its state, and cancel it
  So that the PR invitation desk reflects the real outreach status

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an administrator holding the Invitations.View + Invitations.Manage permissions has signed in
    via /login + /login/totp (superadmin@zagali-ict.com + Get-Totp helper)
  And they have landed on /admin/invitations
  And a UserProfile exists with id "11111111-1111-1111-1111-111111111111"
    (English name "Capt. Faisal Al-Harbi", Arabic name "النقيب فيصل الحربي", ProfileType "VIP")

Scenario: Send, confirm, then cancel one invitation
  Given the grid currently shows {N} rows
  When the administrator clicks "New invitation"
  Then the "Send invitation" modal opens with two fields: "Recipient (UserProfile id)" and "Notes"
  When they fill Recipient (UserProfile id)="11111111-1111-1111-1111-111111111111"
  And they fill Notes="VIP keynote — front-row seating requested"
  And they click "Send"
  Then the BFF posts POST /account/api/admin/invitations and the API returns 200
  And the modal closes
  And a green toast reads "Invitation sent." / "تم إرسال الدعوة."
  And the grid shows {N + 1} rows
  And a row exists with Recipient="Capt. Faisal Al-Harbi", Profile type="VIP", State="Pending",
    Sent by=the signed-in admin's display name, and a "Sent" timestamp in "yyyy-MM-dd HH:mm UTC" form

  When the administrator clicks the row's Edit (pencil) action
  Then a GET /account/api/admin/invitations/{id} fires and the "Edit invitation" modal opens
  And the State dropdown is pre-selected to "Pending"
  And the Notes field is pre-filled with "VIP keynote — front-row seating requested"
  When they change State to "Confirmed"
  And they click "Save changes"
  Then the BFF puts PUT /account/api/admin/invitations/{id} and the API returns 200
  And the modal closes
  And a green toast reads "Invitation updated." / "تم تحديث الدعوة."
  And the row's State column now reads "Confirmed"

  When the administrator clicks the row's Delete / Cancel invitation (trash) action
  Then the BFF sends DELETE /account/api/admin/invitations/{id} and the API returns 200
  And a green toast reads "Invitation cancelled." / "تم إلغاء الدعوة."
  And after the list reloads the row's Active column flips to "Inactive"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-invitations-roundtrip-before.png`
- Screenshot after (post-confirm): `docs/screenshots/cp-admin-invitations-roundtrip-after.png`
- Add-modal + Edit-modal screenshots: `docs/screenshots/cp-admin-invitations-add-modal.png`, `docs/screenshots/cp-admin-invitations-edit-modal.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/invitations/*` call returns 200
- Audit rows: `Invitation.Created`, then `Invitation.StateChanged` (state moved Pending→Confirmed),
  then `Invitation.Deactivated`, each with the actor's user id
- Notification: one `NotificationKind.InvitationReceived` row for the recipient user (best-effort)

### E2E-INV-002 — Send invitation (Add modal)

```gherkin
Scenario: Send a new invitation with valid recipient + notes
  Given the administrator is on /admin/invitations
  When they click "New invitation"
  Then the "Send invitation" modal opens with empty Recipient and Notes fields
  When they fill Recipient (UserProfile id)="11111111-1111-1111-1111-111111111111"
  And they fill Notes="Please confirm attendance by 10 June"
  And they click "Send"
  Then POST /account/api/admin/invitations returns 200 with AdminInvitationDetail
  And the modal closes and a green toast reads "Invitation sent." / "تم إرسال الدعوة."
  And the new row shows State="Pending" and the recipient's projected name + profile type
```

### E2E-INV-003 — Edit invitation state Pending → Confirmed

```gherkin
Scenario: Confirm a pending invitation
  Given a Pending invitation exists for "Capt. Faisal Al-Harbi"
  When the administrator clicks the row's Edit (pencil) action
  Then GET /account/api/admin/invitations/{id} returns 200 and the modal pre-fills State="Pending"
  When they select State="Confirmed" in the dropdown
  And they click "Save changes"
  Then PUT /account/api/admin/invitations/{id} returns 200
  And the row's State column reads "Confirmed"
  And a green toast reads "Invitation updated." / "تم تحديث الدعوة."
  And the server records an Invitation.StateChanged audit row and sets RespondedAt
```

### E2E-INV-004 — Cancel invitation (soft delete)

```gherkin
Scenario: Cancel an active invitation
  Given an active invitation row is visible with the Edit (pencil) and Delete / Cancel invitation (trash) icon actions
  When the administrator clicks the row's Delete / Cancel invitation (trash) action
  Then DELETE /account/api/admin/invitations/{id} returns 200 (ApiResult<bool>.Ok(true))
  And a green toast reads "Invitation cancelled." / "تم إلغاء الدعوة."
  And after the list reloads the row still appears but its Active column now reads "Inactive"
  And clicking the trash action again on an already-cancelled row would be a server no-op (idempotent)
```

### E2E-INV-005 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Invitation rows (or all are filtered out by the server query)
  When the administrator opens /admin/invitations
  Then the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy "No invitations yet." / "لا توجد دعوات بعد."
  And the "New invitation" button is still visible
  And no error toast appears
```

### E2E-INV-006 — Auth gate

```gherkin
Scenario: Signed-in admin lacking the Invitations.View permission is denied
  Given a user is signed in whose role does NOT grant "Invitations.View"
    (e.g. an admin role without the PublicRelations baseline and without the "*" wildcard)
  When they navigate to /admin/invitations
  Then the RequirePermission(PermissionCatalog.Invitations.View) attribute denies access
  And they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/invitations/list request fires
```

### E2E-INV-007 — Client-side validation (non-GUID recipient)

```gherkin
Scenario: A recipient id that is not a GUID is rejected before any POST
  Given the "Send invitation" modal is open
  When the administrator fills Recipient (UserProfile id)="not-a-guid"
  And they click "Send"
  Then SubmitCreateAsync fails the Guid.TryParse guard
  And a red toast appears reading "The invitations could not be loaded." / "تعذّر تحميل الدعوات."
    (the page reuses the LoadFailed string as the parse-failure fallback)
  And the modal stays open
  And NO POST /account/api/admin/invitations request fires
```

### E2E-INV-008 — Target profile not found

```gherkin
Scenario: A well-formed GUID with no matching profile returns 400 INVITATION_TARGET_NOT_FOUND
  Given the "Send invitation" modal is open
  When the administrator fills Recipient (UserProfile id)="99999999-9999-9999-9999-999999999999"
    (a valid GUID that matches no UserProfile)
  And they fill Notes="" (left blank)
  And they click "Send"
  Then POST /account/api/admin/invitations forwards to the API
  And the API returns HTTP 400 with ApiResult.Error.Code = "INVITATION_TARGET_NOT_FOUND"
  And the modal stays open
  And a red toast surfaces the bilingual MessageForCurrentCulture()
    ("Recipient profile '...' does not exist." / "الملف المستهدف '...' غير موجود.")
```

### E2E-INV-009 — Illegal state transition (settled → Pending)

```gherkin
Scenario: Moving a Confirmed invitation back to Pending is rejected
  Given an invitation exists in State="Confirmed"
  When the administrator clicks the row's Edit (pencil) action
  And they select State="Pending" in the dropdown
  And they click "Save changes"
  Then PUT /account/api/admin/invitations/{id} forwards to the API
  And the API returns HTTP 400 with ApiResult.Error.Code = "INVITATION_STATE_INVALID"
  And the modal stays open
  And a red toast reads "Cannot move an invitation back to Pending once it has been settled."
    / "لا يمكن إعادة الدعوة إلى حالة الانتظار بعد البتّ فيها."
```

### E2E-INV-010 — Notes too long

```gherkin
Scenario: Notes longer than 1000 characters are rejected
  Given the "Send invitation" modal is open
  When the administrator fills Recipient (UserProfile id)="11111111-1111-1111-1111-111111111111"
  And they fill Notes with a 1001-character string
  And they click "Send"
  Then POST /account/api/admin/invitations forwards to the API
  And the API returns HTTP 400 with ApiResult.Error.Code = "INVITATION_INVALID"
  And the modal stays open
  And a red toast reads "Invitation notes cannot exceed 1000 characters."
    / "لا يمكن أن تتجاوز ملاحظات الدعوة 1000 حرف."
```

### E2E-INV-011 — Server 500 on /list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on POST /admin/invitations/list (e.g. DB down)
  When the administrator opens /admin/invitations
  Then the page shows the loading line "Loading invitations…" / "جارٍ تحميل الدعوات…"
  And then a red toast appears reading "The invitations could not be loaded." / "تعذّر تحميل الدعوات."
  And no rows render and the SimfEmptyState does NOT appear (the load short-circuited on error)
```

### E2E-INV-012 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page + Send modal
  Given the administrator is on /admin/invitations in English
  When they switch the UI to Arabic from the header language switcher
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الدعوات"
  And the grid headers read "المستلم" / "نوع الملف" / "الحالة" / "تاريخ الإرسال" / "المُرسِل" / "مُفعّل"
  And the row's quiet icon actions carry the Arabic labels "تعديل" (Edit) and "إلغاء الدعوة" (Cancel invitation)
  And the recipient column renders the row's Arabic name (RecipientLabel picks ArabicName when culture is "ar")

  When they click "دعوة جديدة"
  Then the "إرسال دعوة" modal opens in RTL
  And the field labels read "المستلم (معرّف الملف)" and "الملاحظات"
  And the footer buttons read "إرسال" and "إلغاء" in reverse order
```

### E2E-INV-013 — Per-column filter (State) narrows the grid

```gherkin
Scenario: Typing into the State column filter posts a GridQuery filter and narrows the grid
  Given the administrator is on /admin/invitations
  And the grid shows a mix of Pending, Confirmed and Declined invitations
  And the only Filterable="true" column is State (Key "state") — Recipient / Profile type / Sent / Sent by / Active have no filter input
  When the administrator types "Confirmed" into the "Filter column State" input
  Then POST /account/api/admin/invitations/list fires with GridQuery.Filters["state"]="Confirmed"
    and GridQuery.Skip reset to 0 (page returns to the first page)
  And the API returns 200 and the grid re-renders showing only rows whose State pill reads "Confirmed"
  And the summary line recounts to the filtered total
  When the administrator clears the State filter input
  Then POST /account/api/admin/invitations/list fires again with Filters["state"] absent
  And the full, unfiltered set of rows returns
```

**Notes:** the backend (`AdminInvitationService.ListAllAsync`) parses `Filters["state"]` as `InvitationState`
case-insensitively, so "confirmed" and "Confirmed" both match; an unparseable value is ignored (no filter applied).
The `isActive` filter the service also supports is NOT reachable from the grid — there is no filter input on the
Active column — so do not author a UI filter scenario for it.

### E2E-INV-014 — Column sort toggles (State / Sent)

```gherkin
Scenario: Clicking a sortable column header cycles ascending → descending
  Given the administrator is on /admin/invitations
  And Sortable="true" is set on State (Key "state") and Sent (Key "createdat") only
  When the administrator clicks the "Sent" column header
  Then POST /account/api/admin/invitations/list fires with GridQuery.Sort="createdat" and SortDescending=false
  And the grid re-renders ordered by Sent ascending (oldest first)
  When the administrator clicks the "Sent" column header again
  Then POST /account/api/admin/invitations/list fires with Sort="createdat" and SortDescending=true
  And the grid re-renders ordered by Sent descending (newest first — the service default order)
  When the administrator clicks the "State" column header
  Then POST /account/api/admin/invitations/list fires with Sort="state" and SortDescending=false
  And the grid re-renders ordered by State
```

### E2E-INV-015 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch the desk to full-page mode and it persists across reload
  Given the administrator is on /admin/invitations with the default "dialog" presentation
    (the page reads CpPreferences.GetPresentationAsync("invitations") in OnInitializedAsync)
  And the grid toolbar's CustomToolbar slot shows the CrudPresentationToggle "Open as full page" control (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.invitations" holds {"v":1,"presentation":"page"}
  When they reload /admin/invitations
  Then the toggle still reads "Open as dialog"
  And opening "New invitation" now renders the full-page CrudShell frame (not a popup)
```

### E2E-INV-016 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (CrudPresentation.Page)
  When the administrator clicks "New invitation"
  Then the grid + SimfBanner are hidden (GridHidden = FormOpen && _presentation == Page)
  And the CrudShell renders the InvitationsAddEdit form full-page with the title "Send invitation"
    and a "Close" header — no modal backdrop
  When they fill Recipient (UserProfile id)="11111111-1111-1111-1111-111111111111"
  And they fill Notes="Full-page send"
  And they click "Send"
  Then POST /account/api/admin/invitations returns 200, the frame closes, and the grid re-appears
  And a green success toast appears (key Admin.Invitations.Saved — AR "تم حفظ الدعوة.";
    note the EN value is currently missing from Strings.resx, so EN falls back to the key — i18n parity gap)
  When they click the row's Edit (pencil) action and then the frame's "Close" button
  Then the form closes via CloseForm and the grid re-appears unchanged (no PUT fired)
  When they click the row's Details action
  Then the InvitationsViewDelete form opens full-page in read-only mode (no danger button), and "Close" returns to the grid
```

### E2E-INV-017 — Delete confirmation gate (D-353)

```gherkin
Scenario: Cancel invitation requires explicit SimfConfirm before the DELETE
  Given the administrator is on /admin/invitations with an active invitation for "Capt. Faisal Al-Harbi"
  When they click the row's Delete / Cancel invitation (trash) action
  Then GET /account/api/admin/invitations/{id} fires and the InvitationsViewDelete form opens (IsDelete=true)
    showing the row's read-only details and a red "Cancel invitation" button
  When they click "Cancel invitation"
  Then a SimfConfirm dialog appears (Danger=true) with the message
    "Cancel the invitation to \"Capt. Faisal Al-Harbi\"? …" / the Arabic Admin.Invitations.Delete.Message
  When they click the confirm dialog's "Cancel" (CancelLabel) button
  Then _confirming is cleared, NO DELETE request fires, and the row is unchanged
  When they re-open the form, click "Cancel invitation", then click the confirm "Cancel invitation" button
  Then exactly one DELETE /account/api/admin/invitations/{id} fires and returns 200 (ApiResult<bool>.Ok(true))
  And the form closes and a green toast reads "Invitation cancelled." / "تم إلغاء الدعوة." (Admin.Invitations.Deactivated)
  And after the list reloads the row's Active column flips to "Inactive"
```

**Note:** this supersedes the pre-D-353 behaviour where the trash action soft-deleted directly with no confirm
dialog — the delete now always routes through the InvitationsViewDelete form + its SimfConfirm gate.

### E2E-INV-018 — Excel export (D-356, export only)

```gherkin
Scenario: Export the filtered grid (or selected rows) to an XLSX workbook
  Given the administrator is on /admin/invitations with at least two invitations
    and holds the Invitations.Export permission (PublicRelations baseline / Administrator wildcard)
  When they click the toolbar "Export" action with no rows selected
  Then OnExportAsync calls simfAccount.downloadXlsx against /account/api/admin/invitations/export
  And the POST body is AdminGridExportRequest with an empty Ids list and the current Query
    (Query is sent only because no rows are selected)
  And the API ExportInvitationsEndpoint lists via AdminInvitationService.ListAllAsync (Skip reset to 0, Top capped at 5000)
  And the browser saves a file named "simf-invitations-{yyyyMMddHHmmss}.xlsx"
  And the workbook's "Invitations" sheet has the header row
    RecipientEnglishName | RecipientArabicName | ProfileType | Email | State | Notes | SentBy | CreatedAt | RespondedAt | IsActive
  When they instead select two rows then click "Export"
  Then the POST body carries those two Ids and a null Query, and the workbook contains exactly those two rows
  And there is NO Import action on this page — invitations are created/edited from the CP forms only
```

---

## Implementation notes

- **API integration tests cover the same surface at a lower layer** —
  `tests/SIMF.Api.Tests/AdminInvitationsTests.cs` exercises:
  `Create_returns_invitation_with_recipient_data_projected`,
  `Create_with_unknown_profile_is_400_INVITATION_TARGET_NOT_FOUND` (mirrors E2E-INV-008),
  `Update_to_Confirmed_sets_RespondedAt_and_records_state_change` (mirrors E2E-INV-003),
  `Update_from_Confirmed_back_to_Pending_is_400_INVITATION_STATE_INVALID` (mirrors E2E-INV-009),
  `Deactivate_marks_invitation_inactive_and_is_idempotent` (mirrors E2E-INV-004),
  `Non_admin_non_pr_caller_is_forbidden_on_invitations_list` (the API-layer twin of the CP auth gate E2E-INV-006),
  and `PublicRelations_role_can_create_invitation`. These run without a browser, so during the
  transition keep both layers; once Playwright covers a scenario the matching lower-layer case may be retired.
- **Manual smoke is the canonical run today.** Until Playwright is adopted, walk each scenario via a
  Chrome DevTools MCP session: sign in per the Auth setup, drive each action, capture screenshots into
  `docs/screenshots/cp-admin-invitations-*.png`. Keep the Gherkin steps tool-agnostic so they port to a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus a step-definition class later.
- **Permission gate twin.** `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` asserts the nav
  item `Module.Invitations` carries `RequiredPermission = PermissionCatalog.Invitations.View`, and
  `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fails the build if any admin endpoint is ungated —
  these back E2E-INV-006 statically.
- **Grid filter + sort + pager (post D-256).** `InvitationsList.razor` now renders `SimfDataGrid` with a
  per-column filter input on **State** (`state`), sortable **State** (`state`) + **Sent** (`createdat`) headers,
  and a Prev/Next/First/Last + page-size pager (`Top = 20`). The service still also honours an `isActive` filter,
  but no UI control drives it — do not author a scenario that filters by Active until the page grows that control.
- **Page reference doc.** The per-page reference doc now exists at
  [`docs/pages/cp/admin-invitations.md`](../../pages/cp/admin-invitations.md) (authored under D-356 Phase 5).
- **D-353 CrudShell framing.** Add/Edit/View/Delete are hosted by `CrudShell` (popup or full page per the
  `CrudPresentationToggle`, persisted in `simf.cp.prefs.invitations`). Delete is gated by `SimfConfirm` inside
  `InvitationsViewDelete` — there is no native `window.confirm` and no direct trash-icon delete (E2E-INV-015..017).
- **D-356 Excel export (export only).** The grid wires `OnExport` to `ExportInvitationsEndpoint`
  (`POST /admin/invitations/export`, `Invitations.Export` permission, "Invitations" sheet, 5000-row cap). No
  import endpoint or UI exists for this resource (E2E-INV-018).

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle). Earlier: 2026-06-03 (E2E catalogue rebuild, D-256/D-257 grid affordances reconciled).
