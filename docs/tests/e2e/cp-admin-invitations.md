# E2E test catalogue — Invitations desk (`/admin/invitations`)

| | |
|--|--|
| **Page** | [`cp/admin-invitations.md`](../../pages/cp/admin-invitations.md) _(reference doc not yet authored)_ |
| **Route** | `/admin/invitations` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page facts (grounded in `InvitationsList.razor` @ commit `aafe769`):**
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
> - The **Add** modal ("Send invitation") collects exactly two fields: **Recipient (UserProfile id)** (a free-text
>   GUID — `_newRecipientId`) and **Notes** (`_newNotes`). The page parses the GUID **client-side** before posting.
> - The **Edit** modal ("Edit invitation") collects a **State** dropdown (`Pending` / `Confirmed` / `Declined`, from
>   the `InvitationState` enum) and **Notes**. It is opened by re-fetching the row via `GET .../{id}` first.
> - Each grid row has an **Edit invitation** button and — when `IsActive` — a **Cancel invitation** button
>   (soft delete; no confirm dialog is wired — `DeactivateConfirm` resx exists but `OnDeactivateAsync` does not prompt).
> - Grid columns: Recipient, Profile type, State, Sent (`yyyy-MM-dd HH:mm 'UTC'`), Sent by, actions.
> - **There is no state/active filter control and no pager button rendered on the page** today — the service
>   supports `state` / `isActive` filters and paging, but the `.razor` exposes neither. Do not assert UI that is not there.
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
| E2E-INV-004 | Cancel invitation (soft delete) hides the Cancel button on the row | happy | P1 | _to author_ |
| E2E-INV-005 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-INV-006 | Auth gate — signed-in admin lacking `Invitations.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-INV-007 | Validation — non-GUID recipient id is rejected client-side (no POST fires) | error | P1 | _to author_ |
| E2E-INV-008 | Target not found — valid GUID, no such profile → 400 `INVITATION_TARGET_NOT_FOUND` | error | P1 | _to author_ |
| E2E-INV-009 | Illegal transition — settled invitation back to `Pending` → 400 `INVITATION_STATE_INVALID` | error | P1 | _to author_ |
| E2E-INV-010 | Notes too long (>1000 chars) → 400 `INVITATION_INVALID` | error | P2 | _to author_ |
| E2E-INV-011 | Server 500 on `/list` → bilingual fallback toast, no rows render | resilience | P2 | _to author_ |
| E2E-INV-012 | RTL / Arabic render mirrors page + Add modal | i18n | P1 | _to author_ |

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

  When the administrator clicks "Edit invitation" on that row
  Then a GET /account/api/admin/invitations/{id} fires and the "Edit invitation" modal opens
  And the State dropdown is pre-selected to "Pending"
  And the Notes field is pre-filled with "VIP keynote — front-row seating requested"
  When they change State to "Confirmed"
  And they click "Save changes"
  Then the BFF puts PUT /account/api/admin/invitations/{id} and the API returns 200
  And the modal closes
  And a green toast reads "Invitation updated." / "تم تحديث الدعوة."
  And the row's State column now reads "Confirmed"

  When the administrator clicks "Cancel invitation" on that row
  Then the BFF sends DELETE /account/api/admin/invitations/{id} and the API returns 200
  And a green toast reads "Invitation cancelled." / "تم إلغاء الدعوة."
  And the row's "Cancel invitation" button disappears (the row is now inactive)
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
  When the administrator clicks "Edit invitation" on that row
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
  Given an active invitation row is visible with both "Edit invitation" and "Cancel invitation" buttons
  When the administrator clicks "Cancel invitation"
  Then DELETE /account/api/admin/invitations/{id} returns 200 (ApiResult<bool>.Ok(true))
  And a green toast reads "Invitation cancelled." / "تم إلغاء الدعوة."
  And after the list reloads the row still appears but no longer renders the "Cancel invitation" button
  And clicking "Cancel invitation" again on an already-cancelled row would be a server no-op (idempotent)
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
  When the administrator clicks "Edit invitation" on that row
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
  And the grid headers read "المستلم" / "نوع الملف" / "الحالة" / "تاريخ الإرسال" / "المُرسِل"
  And the action buttons read "تعديل الدعوة" and "إلغاء الدعوة"
  And the recipient column renders the row's Arabic name (RecipientLabel picks ArabicName when culture is "ar")

  When they click "دعوة جديدة"
  Then the "إرسال دعوة" modal opens in RTL
  And the field labels read "المستلم (معرّف الملف)" and "الملاحظات"
  And the footer buttons read "إرسال" and "إلغاء" in reverse order
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
- **No filter / pager UI today.** The service supports `state` + `isActive` filters and `Skip`/`Top` paging,
  but `InvitationsList.razor` renders neither a filter control nor pager buttons (only a summary line). Do not
  author scenarios that drive a filter dropdown or pager until the page grows that UI.
- **Page reference doc gap.** `docs/pages/cp/admin-invitations.md` does not exist yet; the linked path is a
  placeholder for when the per-page reference doc is authored.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
