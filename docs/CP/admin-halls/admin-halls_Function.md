# Halls — Function (`/admin/halls`)

What the operator does on the Control Panel Halls page. Grounded in the as-built
`HallsList.razor` + `HallsAddEdit.razor` + `HallsViewDelete.razor` and the admin
service. Verified against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-halls_Design.md) · [API](admin-halls_API.md) ·
> [Logic](admin-halls_Logic.md) · E2E
> [`docs/tests/e2e/cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md)
> (E2E-HAL-001 … 022).

## Who can open it
**Administrators** (and any role granted `Halls.View`). The page is
`@attribute [RequirePermission(PermissionCatalog.Halls.View)]`; a signed-in admin
without that permission (and not `Administrator = "*"`) is redirected to
`/not-permitted` and **no** `/list` call fires. Each write action is gated again
at the API by its own permission (`Halls.Create` / `.Edit` / `.Delete`), so a
view-only operator sees the grid but cannot create, edit or deactivate.

## What the page is for
Curate the venue **halls / rooms** (SIMF-FDS-004 §5.2). A hall is the room the
programme places a **Session** in — its bilingual name is what visitors read on
the app agenda (Page 016) and its `Capacity` caps the session's bookable seats.
A hall may also be pinned on the 2D venue map (Page 015). Each hall has a stable
uppercase **Code**, a bilingual name, an integer **Capacity**, an optional
**Floor** label, free-text **Equipment + accessibility notes**, and an optional
**GPS geofence** (lat/lon/radius) for arrival detection.

## What the operator does

### 1. Browse / search / sort / page
The grid lists halls (default 20/page) with Code, Name, Name (Arabic), Capacity,
Floor and a Status pill. The operator can **sort** by Code / Name / Name (Arabic)
/ Capacity, **search** (matches Code / Name / Name (Arabic)), and **filter**
per-column on Code / Name / Name (Arabic) / Floor. Pager controls page through
larger sets.

### 2. Add a hall (`Halls.Create`)
Click **Add hall** → the `HallsAddEdit` form opens (popup or full page per the
toggle). Fill **Code, Name (English), Name (Arabic), Capacity** (all required),
optionally **Floor**, **Equipment + accessibility notes**, and the **geofence
triple**. Click **Create hall**. Code is uppercased before send. On success the
form closes, the grid reloads with the new row (green **Active** pill), and a
green toast reads `Hall "{name}" was created.` / `تم إنشاء القاعة "{name}".`

### 3. Edit a hall (`Halls.Edit`)
Click the **Edit** action → the page fetches the full detail, opens
`HallsAddEdit` pre-filled (incl. notes + geofence, which the grid omits). An
extra **Active — available for Session assignment** checkbox appears (Edit only).
Change fields, click **Save changes**. On success: green toast `Hall "{name}"
was updated.` / `تم تحديث القاعة "{name}".` **Re-activating** a deactivated hall
is done here by ticking Active and saving (there is no separate activate button).

### 4. View details (`Halls.View`)
Click the **Details** action → the read-only `HallsViewDelete` opens showing
Code, Name, Name (Arabic), Capacity, Floor, Equipment + accessibility notes and
Status — no editable inputs, no Deactivate button. **Close** dismisses it.

### 5. Deactivate a hall (`Halls.Delete`, soft-delete + confirm)
Click the **Deactivate** action → the read-only `HallsViewDelete` opens with a
red **Deactivate** button. Clicking it raises a **`SimfConfirm`** titled
"Deactivate hall" reading `Deactivate the hall "{name}"? It will no longer be
available for session assignment.` **Cancel** → nothing happens. **Confirm** →
exactly one `DELETE` fires, the hall's `IsActive` flips to false (the row stays
visible with a grey **Inactive** pill), and a green toast reads `Hall "{name}"
was deactivated.` / `تم تعطيل القاعة "{name}".` The hall is never physically
deleted.

### 6. Export / Import Excel (`Halls.Export` / `Halls.Import`)
**Export** downloads an `.xlsx` of the current filtered grid (or just the
selected rows) — sheet header `Code | Name | NameArabic | Capacity | Floor |
IsActive`, capped at 5000 rows. **Import** opens a file picker (`.xlsx` only);
rows are created/updated by Code, a result modal shows the per-row outcome, and a
non-`.xlsx` / oversized / wrong-sheet upload is rejected (HTTP 400) with a
bilingual error toast and nothing written.

### 7. Toggle Page ⇄ Popup presentation (D-353)
The toolbar toggle switches whether Add / Edit / Details / Deactivate open as a
dialog popup or take over the content area as a full page. The choice persists in
the browser (`localStorage` `simf.cp.prefs.halls`) across reloads.

## Golden path (E2E-HAL-001)
Add `H1 / Main Auditorium / القاعة الرئيسية / 500 / Ground` → edit Capacity to
650 → view details → deactivate (with confirm). Each step round-trips a single
`/account/api/admin/halls/*` call returning 200 and raises the matching bilingual
toast; the audit log records `Hall.Created`, `Hall.Updated`, `Hall.Deactivated`.

## Validation the operator will hit (client + server)
| Rule | Message (EN) |
|------|--------------|
| Code length ∉ [2,16] | "Code must be between 2 and 16 characters." |
| Blank English name | "English name is required (1–128 characters)." |
| Blank Arabic name | "Arabic name is required (1–128 characters)." |
| Capacity not int / negative | "Capacity must be zero or a positive integer." |
| Partial / out-of-range geofence | "The geofence needs a valid latitude (−90..90), longitude (−180..180) and radius (greater than 0, up to 100000 m) — set all three or leave all empty." |
| Duplicate Code (server, 409) | "A hall with code '{code}' already exists." |

The first five are caught client-side (the form does not send); the duplicate is
a server 409 surfaced in the form's alert. Every message has an Arabic pair.

## Permission summary
| Action | Permission | Baseline |
|--------|------------|----------|
| Open page / list / details | `Halls.View` | AdminOnly |
| Add | `Halls.Create` | AdminOnly |
| Edit / re-activate | `Halls.Edit` | AdminOnly |
| Deactivate | `Halls.Delete` | AdminOnly |
| Export | `Halls.Export` | AdminOnly |
| Import | `Halls.Import` | AdminOnly |

`Administrator = "*"` covers all six. Nav item `Module.Halls` carries
`RequiredPermission = PermissionCatalog.Halls.View`.

## Effect on the app
- **Page 016 (Sessions agenda):** the hall's name (EN/AR) is what each session
  row names; its Capacity caps the session's bookable seats.
- **Page 015 (Venue map):** a hall can be the target of a map node (`Kind =
  Hall`), so deactivating/keeping a hall affects what the map can point at.

Deactivating a hall removes it from new Session assignment (the picker reads
active halls), but does **not** currently block deactivation of a hall an active
session already uses (`HALL_IN_USE` is reserved, not enforced — see
[Logic](admin-halls_Logic.md) L-7).
