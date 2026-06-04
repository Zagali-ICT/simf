# E2E test catalogue — Venue map (`/admin/venue-map`)

| | |
|--|--|
| **Page** | [`cp/venue-map.md`](../../pages/cp/venue-map.md) |
| **Route** | `/admin/venue-map` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-03 |

> **Page background.** P2.5 (D-230) — CP editor for the 2D venue map
> (SIMF-FDS-006 §5.3/§7, FR-605). Each row is a **node**: a bilingual label
> (Label / Label (Arabic)), a `Kind` (`Hall` / `Zone` / `Booth` /
> `PointOfInterest`), a 2D position (`X`, `Y` — `double`, step `0.1`), and an
> **optional** link to a Hall **or** a Booth. The hall/booth pickers are loaded
> at mount from `/account/api/admin/halls/list` + `/account/api/admin/booths/list`
> (Top=500). The Flutter app renders the active nodes on its 2D canvas via the
> public `GET /api/v1/venue-map`. The table **ships empty** — the Logistics team
> places the nodes — so the empty-state path is the default first render. Mirrors
> `SessionCategoriesList`. **RequiredPermission:** the page is gated by
> `PermissionCatalog.VenueMap.View`; the toolbar/row actions are gated by
> `.Create` / `.Edit` / `.Delete` (all `AdminOnly` baseline).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-VMP-001 | Full CRUD round-trip — Add → Edit (move + toggle Active off) → Delete | happy | P0 | _to author_ |
| E2E-VMP-002 | Add node with no Hall/Booth link (free-standing PointOfInterest) | happy | P1 | _to author_ |
| E2E-VMP-003 | Add node linked to a Hall via the picker | happy | P1 | _to author_ |
| E2E-VMP-004 | Add node linked to a Booth via the picker | happy | P1 | _to author_ |
| E2E-VMP-005 | Kind dropdown lists all four `VenueMapNodeKind` values | happy | P2 | _to author_ |
| E2E-VMP-006 | Edit opens modal pre-filled from `GET /{id}` (incl. Active checkbox) | happy | P1 | _to author_ |
| E2E-VMP-007 | Cancel on the modal discards changes (no API call) | happy | P2 | _to author_ |
| E2E-VMP-008 | Delete confirmation cancelled → no DELETE fires | happy | P2 | _to author_ |
| E2E-VMP-009 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-VMP-010 | Auth gate: signed-in admin lacking `VenueMap.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-VMP-011 | Action gate: viewer with `View` but no `Create/Edit/Delete` sees no buttons | auth | P1 | _to author_ |
| E2E-VMP-012 | Validation: blank Label / Label (Arabic) → bilingual error toast, no POST | error | P1 | _to author_ |
| E2E-VMP-013 | Server validation: unknown Hall/Booth link → 400 `VENUE_MAP_NODE_INVALID` | error | P1 | _to author_ |
| E2E-VMP-014 | Not found: edit a node deleted by another admin → 404 `VENUE_MAP_NODE_NOT_FOUND` | error | P2 | _to author_ |
| E2E-VMP-015 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-VMP-016 | RTL render: Arabic toggle mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-VMP-017 | Per-column filter on Label narrows the grid (`Filters["label"]`, Skip→0) | happy | P1 | _to author_ |
| E2E-VMP-018 | Column sort toggles on Label / Kind (`Sort` + `SortDescending`) | happy | P2 | _to author_ |

## Scenarios

### E2E-VMP-001 — Full CRUD round-trip

```gherkin
Feature: Venue-map node CRUD round-trip
  As an Administrator with the VenueMap permissions
  I want to add, move and remove 2D venue-map nodes
  So that the Flutter app renders an accurate venue map

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in as superadmin@zagali-ict.com via /login + /login/totp (Get-Totp helper)
  And they have landed on /admin/venue-map
  And the page issued POST /account/api/admin/venue-map/list (200)
  And the picker loads POST /account/api/admin/halls/list and POST /account/api/admin/booths/list both returned 200

Scenario: Create, edit (move + deactivate), then delete one node
  Given the grid currently shows {N} rows (or the SimfEmptyState when {N}=0)
  When the administrator clicks "New node"
  Then a modal opens titled "New venue-map node"
  And it shows fields: Label (English), Label (Arabic), Kind, X position, Y position, Linked hall (optional), Linked booth (optional)
  And the "Active" checkbox is NOT shown (it only appears in edit mode)
  When they fill Label (English)="Main Entrance"
  And they fill Label (Arabic)="المدخل الرئيسي"
  And they select Kind="PointOfInterest"
  And they set X position="120.5"
  And they set Y position="88"
  And they leave Linked hall = "— None —" and Linked booth = "— None —"
  And they click "Save"
  Then POST /account/api/admin/venue-map fires and returns 200
  And the modal closes
  And a green toast reads "Node saved." / "تم حفظ العقدة."
  And the grid reloads via POST /account/api/admin/venue-map/list
  And a row exists with Label="Main Entrance", Kind="PointOfInterest", Position="120.5, 88" and Active="✓"

  When the administrator clicks the row's Edit (pencil) action
  Then GET /account/api/admin/venue-map/{id} fires and returns 200
  And the modal opens titled "Edit venue-map node" with every field pre-filled
  And the "Active" checkbox is now visible and ticked
  When they change X position to "200"
  And they change Y position to "150.4"
  And they untick the "Active" checkbox
  And they click "Save"
  Then PUT /account/api/admin/venue-map/{id} fires and returns 200
  And the modal closes
  And a green toast reads "Node saved." / "تم حفظ العقدة."
  And the row's Position column reads "200, 150.4" and the Active column reads "—"

  When the administrator clicks the row's Delete (trash) action
  Then a browser confirm() dialog reads "Remove this venue-map node?" / "هل تريد إزالة عقدة الخريطة هذه؟"
  When they accept the dialog
  Then DELETE /account/api/admin/venue-map/{id} fires and returns 200
  And a green toast reads "Node removed." / "تمت إزالة العقدة."
  And the grid reloads and no longer shows "Main Entrance"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-venue-map-001-before.png`
- Screenshot after (add): `docs/screenshots/cp-admin-venue-map-001-add.png`
- Screenshot after (edit): `docs/screenshots/cp-admin-venue-map-001-edit.png`
- Screenshot after (delete): `docs/screenshots/cp-admin-venue-map-001-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/venue-map/*` call returns 200; the two picker `list` calls at mount return 200
- Audit rows: `OperationLog` rows with `Event = 'VenueMapNode.Created'`, `'VenueMapNode.Updated'`, `'VenueMapNode.Deactivated'`, each with the superadmin actor id

### E2E-VMP-002 — Free-standing node (no link)

```gherkin
Scenario: Add a PointOfInterest with no hall or booth link
  Given the Add modal is open
  When the administrator fills Label (English)="Prayer Room", Label (Arabic)="مصلى"
  And selects Kind="PointOfInterest"
  And sets X position="40", Y position="12"
  And leaves both Linked hall and Linked booth on "— None —"
  And clicks "Save"
  Then POST /account/api/admin/venue-map fires with HallId=null and BoothId=null
  And it returns 200
  And the new row shows Position="40, 12" with no link error
```

### E2E-VMP-003 — Node linked to a Hall

```gherkin
Scenario: Add a Hall node linked through the hall picker
  Given at least one active Hall exists (picker non-empty)
  And the Add modal is open
  When the administrator fills Label (English)="Hall A Marker", Label (Arabic)="علامة القاعة أ"
  And selects Kind="Hall"
  And sets X position="10", Y position="10"
  And picks a hall from the "Linked hall (optional)" dropdown
  And clicks "Save"
  Then POST /account/api/admin/venue-map fires with the chosen HallId guid
  And it returns 200 and the node is created
```

### E2E-VMP-004 — Node linked to a Booth

```gherkin
Scenario: Add a Booth node linked through the booth picker
  Given at least one active Booth exists (picker non-empty)
  And the Add modal is open
  When the administrator fills Label (English)="Booth 12", Label (Arabic)="الجناح ١٢"
  And selects Kind="Booth"
  And sets X position="60", Y position="30"
  And picks a booth from the "Linked booth (optional)" dropdown (label shown is the booth's English name)
  And clicks "Save"
  Then POST /account/api/admin/venue-map fires with the chosen BoothId guid
  And it returns 200 and the node is created
```

### E2E-VMP-005 — Kind dropdown completeness

```gherkin
Scenario: The Kind dropdown offers all four VenueMapNodeKind values
  Given the Add modal is open
  When the administrator opens the "Kind" dropdown
  Then exactly four options are present in this order: Hall, Zone, Booth, PointOfInterest
  And the default selection is "Hall" (enum value 0)
```

### E2E-VMP-006 — Edit pre-fill

```gherkin
Scenario: Edit fetches the detail and pre-fills the modal
  Given a node "Registration Desk" exists
  When the administrator clicks the row's Edit (pencil) action
  Then GET /account/api/admin/venue-map/{id} returns 200
  And the modal title is "Edit venue-map node"
  And Label (English), Label (Arabic), Kind, X position, Y position, Linked hall, Linked booth are pre-filled from the detail
  And the "Active" checkbox is rendered and reflects the node's IsActive flag
```

### E2E-VMP-007 — Cancel discards

```gherkin
Scenario: Cancel closes the modal without saving
  Given the Add modal is open with Label (English)="Scratch"
  When the administrator clicks "Cancel"
  Then the modal closes
  And NO POST /account/api/admin/venue-map request fires
  And the grid is unchanged
```

### E2E-VMP-008 — Delete confirmation cancelled

```gherkin
Scenario: Declining the confirm dialog leaves the node intact
  Given a node "Keep Me" exists in the grid
  When the administrator clicks the row's Delete (trash) action
  Then a confirm() dialog reads "Remove this venue-map node?"
  When they dismiss the dialog
  Then NO DELETE /account/api/admin/venue-map/{id} request fires
  And the "Keep Me" row remains in the grid
```

### E2E-VMP-009 — Empty state

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no VenueMapNode rows
  When the administrator opens /admin/venue-map
  Then POST /account/api/admin/venue-map/list returns 200 with Total=0
  And the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy "No venue-map nodes yet." / "لا توجد عقد على الخريطة بعد."
  And the toolbar still shows the "New node" button (when the admin has VenueMap.Create)
```

### E2E-VMP-010 — Auth gate (page)

```gherkin
Scenario: Signed-in admin lacking VenueMap.View is denied
  Given a signed-in Control Panel user whose role does NOT include VenueMap.View
    (and is not the wildcard Administrator "*")
  When they navigate to /admin/venue-map
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/venue-map/list request fires
  And the "Venue map" item is absent from the nav rail (RequiredPermission = VenueMap.View)
```

### E2E-VMP-011 — Action gate (view-only role)

```gherkin
Scenario: A view-only admin sees the grid but no action buttons
  Given a signed-in admin whose role grants VenueMap.View but NOT Create/Edit/Delete
  When they open /admin/venue-map
  Then the grid renders with its rows
  And the grid's Add ("New node") toolbar affordance is NOT rendered (AuthorizedAction VenueMap.Create)
  And no row shows the Edit (pencil) action (AuthorizedAction VenueMap.Edit)
  And no row shows the Delete (trash) action (AuthorizedAction VenueMap.Delete)
```

### E2E-VMP-012 — Client-side validation

```gherkin
Scenario: Blank labels show a bilingual error and block the POST
  Given the Add modal is open
  When the administrator leaves Label (English) blank (or Label (Arabic) blank)
  And clicks "Save"
  Then a SimfAlert error toast appears reading "Both labels are required." / "كلا التسميتين مطلوبتان."
  And the modal stays open
  And NO POST /account/api/admin/venue-map request fires
```

### E2E-VMP-013 — Server validation (unknown link)

```gherkin
Scenario: A stale/unknown Hall or Booth link returns 400
  Given the Add modal is open with valid labels and Kind=Hall
  And the chosen Hall was deactivated/removed after the picker loaded (HallId no longer active)
  When the administrator clicks "Save"
  Then POST /account/api/admin/venue-map returns HTTP 400
  And ApiResult.Error.Code = "VENUE_MAP_NODE_INVALID"
  And the error toast surfaces the bilingual MessageForCurrentCulture()
    ("The referenced hall was not found." / "لم يتم العثور على القاعة المرتبطة.")
  And the modal stays open
```

### E2E-VMP-014 — Not found on edit

```gherkin
Scenario: Editing a node another admin just deleted returns 404
  Given a node was visible in the grid but has since been deactivated/removed by another admin
  When the administrator clicks the stale row's Edit (pencil) action
  Then GET /account/api/admin/venue-map/{id} returns HTTP 404
  And ApiResult.Error.Code = "VENUE_MAP_NODE_NOT_FOUND"
  And a red toast surfaces the bilingual message
    ("The venue-map node was not found." / "لم يتم العثور على عقدة الخريطة.")
  And the modal does not open
```

### E2E-VMP-015 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/venue-map/list (e.g. DB down)
  When the administrator opens /admin/venue-map
  Then the loading text "Loading venue map…" / "جارٍ تحميل خريطة المكان…" shows briefly
  And then a red toast appears reading "The action could not be completed. Please try again."
    / "تعذّر إكمال العملية. يُرجى المحاولة مرة أخرى."
  And no rows render
```

### E2E-VMP-016 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/venue-map in English
  When they switch the UI culture to Arabic (العربية)
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "خريطة المكان"
  And the nav rail "Venue map" item reads "خريطة الموقع" and mirrors
  And the table headers read التسمية / النوع / الموضع (س، ص) / مُفعّل

  When they click "عقدة جديدة" (New node)
  Then the Add modal opens in RTL titled "عقدة خريطة جديدة"
  And the field labels are Arabic (التسمية (بالعربية), النوع, الموضع الأفقي (س), الموضع الرأسي (ص),
      القاعة المرتبطة (اختياري), الجناح المرتبط (اختياري))
  And the "— None —" picker option reads "— لا شيء —"
  And the footer buttons read حفظ (Save) / إلغاء (Cancel) in reverse order
```

### E2E-VMP-017 — Per-column filter on Label narrows the grid

```gherkin
Scenario: Typing into the Label column filter narrows the grid server-side
  Given the grid has more than one page of nodes (e.g. "Main Entrance",
    "Registration Desk", "Prayer Room" all present)
  And the administrator is on page 2 (POST /list previously sent Skip=20)
  When the administrator types "Entrance" into the per-column filter input
    under the "Label" column header (the input labelled "Filter column Label")
  Then POST /account/api/admin/venue-map/list fires with
    GridQuery.Filters["label"]="Entrance" and Skip reset to 0
  And it returns 200
  And the grid narrows to only rows whose Label contains "Entrance"
    (e.g. "Main Entrance")
  And the pager summary reflects the smaller Total
  When the administrator clears the "Label" filter input
  Then POST /account/api/admin/venue-map/list fires again with
    Filters["label"] absent/empty and Skip=0
  And the full first page (Top=20) returns
```

### E2E-VMP-018 — Column sort toggles on Label / Kind

```gherkin
Scenario: Clicking a sortable header cycles ascending then descending
  Given the grid shows several nodes in natural order
  When the administrator clicks the sortable "Label" column header
  Then POST /account/api/admin/venue-map/list fires with
    GridQuery.Sort="label", SortDescending=false and Skip reset to 0
  And the rows render ascending by Label, header aria-sort="ascending"
  When they click the "Label" header again
  Then POST /account/api/admin/venue-map/list fires with
    Sort="label", SortDescending=true and Skip=0
  And the rows render descending, header aria-sort="descending"
  When they click the sortable "Kind" column header instead
  Then POST /account/api/admin/venue-map/list fires with
    Sort="kind", SortDescending=false and Skip=0
  And the previous Label sort indicator clears
  # Note: only the "Label" and "Kind" columns are Sortable; "Position" and the
  # Active column have no sort affordance.
```

---

## Implementation notes

- **Manual smoke as canonical-source-of-truth today.** Until Playwright is
  adopted, the canonical "run" of these scenarios is a Chrome DevTools MCP
  session: sign in as `superadmin@zagali-ict.com` via `/login` + `/login/totp`
  (TOTP from the `Get-Totp` helper), walk each scenario, and capture screenshots
  into `docs/screenshots/cp-admin-venue-map-{scenario}.png`.
- **No page reference doc yet.** `docs/pages/cp/venue-map.md` does not exist at
  the time of writing (P2.5 / D-230 page). The grounding for this catalogue is
  the `.razor` page, the `VenueMapEndpoints` / `VenueMapService`, and the resx
  strings — create the page doc when the page reference set is back-filled.
- **API integration tests** at `tests/SIMF.Api.Tests/VenueMapTests.cs` cover the
  same surface at a lower layer (no browser):
  - `Create_then_get_then_list_and_public_read` — create → GET by id → public
    `GET /api/v1/venue-map` returns the active node (mirrors E2E-VMP-001/002).
  - `Create_with_an_unknown_hall_is_400` — asserts `ErrorCodes.VenueMapNodeInvalid`
    (mirrors E2E-VMP-013).
  - `Deactivate_drops_it_from_the_public_read` — soft-delete removes the node
    from the public read (mirrors the delete leg of E2E-VMP-001).
  - `Non_admin_caller_is_forbidden_on_create` — a visitor token → HTTP 403 on
    create (the API-layer counterpart of the CP auth gate E2E-VMP-010/011).
- **API layer note.** The admin endpoints live in
  `src/Backend/SIMF.Api/Endpoints/Admin/VenueMapEndpoints.cs`, gated by
  `PermissionCatalog.PolicyFor(VenueMap.View|Create|Edit|Delete)` +
  `RequireApprovedAccount`; create/update/delete additionally use the `"auth"`
  rate-limiter. The CP reaches them through the BFF passthroughs in
  `AccountEndpoints.cs` under `/account/api/admin/venue-map/*`. Delete is a soft
  delete (`node.Deactivate()`) and is idempotent (a second delete on an already
  inactive node still returns 200).

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
