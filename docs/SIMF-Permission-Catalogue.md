# SIMF — Permission Catalogue

Last updated: 2026-05-31 · Issue-1 (per-page / per-action permissions)

This document is the human-readable companion to
[`PermissionCatalog.cs`](../src/Shared/SIMF.Common/PermissionCatalog.cs), which is
the **single source of truth** in code. If the two ever disagree, the code wins
and this document must be corrected in the same changeset.

## Model

A **permission** is one *action* on one *page* — the unit of authorisation
(SIMF-RPM-001 §8, SIMF-DAT-001 §5.1). It is stored as a `Permission` row
(`Code`, which is exactly `Page.Action`) and granted to a role through a
`RolePermission` row. The page, action and display name are not columns: the
assignment UI takes them from the in-process `PermissionCatalog`. These two tables pre-exist in the D-110 frozen schema, so
the catalogue is seeded as **data only — no schema change and no migration**.

- **Assignment is roles-only.** A user receives permissions by holding a role;
  there are no per-user permission grants.
- **Administrator = wildcard.** An Administrator is never granted individual
  codes. At token-mint time its `perm` claim is the single wildcard value `*`,
  which satisfies every permission check. This keeps the token small and the
  super-admin un-lockout-able.
- **Resolution is baked into the token.** A user's permission codes are resolved
  from their roles once, at sign-in / refresh, and minted as `perm` claims in the
  JWT. The Control Panel copies those claims into its auth cookie. No per-request
  database lookup.

### Conventions

| Concern | Convention |
|---|---|
| Code format | `Page.Action`, PascalCase both sides (e.g. `Sessions.Edit`) |
| Claim type | `perm` (multi-valued); Administrator holds the single value `*` |
| Policy name | `perm:` + code (e.g. `perm:Sessions.Edit`), built by `PermissionCatalog.PolicyFor` |
| API gate | `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Edit))` |
| CP page gate | `@attribute [RequirePermission(PermissionCatalog.Sessions.Edit)]` |
| CP menu | each `NavItem` carries a `RequiredPermission`; the shell hides items the user lacks |
| CP action | `<AuthorizedAction Permission="...">` hides a button the user lacks |

### Action vocabulary

`View` (list + detail + page render) · `Create` · `Edit` (update) · `Delete`
(soft-delete, incl. bulk) · `Approve` / `Reject` (incl. bulk) · `Export` ·
`Import` · plus the page-specific verbs `ResetTwoFactor`, `RegisterOnsite`,
`PrintBag`, `Assign` / `Revoke`, `Moderate`, `Manage`, `Operate`, `Notify`,
`Test`, `AssignPermissions`. Duplicate folds into `Create`; bulk variants fold
into their single-row verb.

## Baseline grants

| Role | Seeded permissions | Source |
|---|---|---|
| **Administrator** | *all* (via the `*` wildcard, not per-code rows) | mint-time |
| **GateOperator** | `Gates.Operate`, `Gates.ViewOwnReports` | seeded grant (D-148) |
| **PublicRelations** | `Invitations.View`, `Invitations.Manage`, `Vips.View`, `Vips.Notify`, `News.*` | seeded grant (D-168; News carried over from the PR-gated admin News endpoints) |
| *(custom roles)* | whatever an Administrator assigns in the CP | runtime |

Every other code is, by default, held only by Administrator (wildcard). An
Administrator can grant any code to any custom role at runtime.

## Catalogue

> The six codes marked † (`Gates.Manage/Operate/ViewOwnReports`,
> `Invitations.Manage`, `Vips.View/Notify`) pre-date this catalogue and keep
> their exact strings, pages, actions and display names so existing seeded rows
> and grants match on re-seed.

### People & accounts
| Code | Page · Action | Baseline | Backing surface |
|---|---|---|---|
| `Admins.View` | Admins · View | — | `/admin/admins`, `/admin/admins/pending`; `POST /admin/admins/list`, `…/pending/list` |
| `Admins.Create` | Admins · Create | — | `/admin/admins/new`; `POST /admin/admins`, `…/duplicate` |
| `Admins.Delete` | Admins · Delete | — | `POST /admin/admins/bulk-delete` |
| `Admins.Approve` | Admins · Approve | — | `POST /admin/admins/{id}/approve`, `…/bulk-approve` |
| `Admins.Reject` | Admins · Reject | — | `POST /admin/admins/{id}/reject`, `…/bulk-reject` |
| `Admins.Export` | Admins · Export | — | `POST /admin/admins/export` |
| `Admins.Import` | Admins · Import | — | `POST /admin/admins/import` |
| `Admins.ResetTwoFactor` | Admins · ResetTwoFactor | — | `/admin/reset-2fa`; `POST /admin/admins/reset-two-factor` |
| `Admins.AssignRoles` | Admins · AssignRoles | — | `GET/PUT /admin/admins/{id}/roles`; the existing-user role editor (D-208) |
| `Others.View` | Others · View | — | `/admin/others`, `…/pending`; lists + profile reads + id-document fetch |
| `Others.Create` | Others · Create | — | `/admin/others/new`; `POST /admin/others`, `…/duplicate` |
| `Others.Edit` | Others · Edit | — | id-document upload |
| `Others.Delete` | Others · Delete | — | `POST /admin/others/bulk-delete` |
| `Others.Approve` | Others · Approve | — | `…/{id}/approve`, `…/bulk-approve` |
| `Others.Reject` | Others · Reject | — | `…/{id}/reject`, `…/bulk-reject` |
| `Others.Export` | Others · Export | — | `POST /admin/others/export` |
| `Others.Import` | Others · Import | — | `POST /admin/others/import` |
| `Others.RegisterOnsite` | Others · RegisterOnsite | — | `POST /admin/others/register-onsite` |
| `Visitors.*` | Visitors · View/Create/Edit/Delete/Approve/Reject/Export/Import/RegisterOnsite | — | mirror of Others under `/admin/visitors` |
| `Accounts.ChangeType` | Accounts · ChangeType | — | `POST /admin/accounts/{id}/change-type` (D-728) — flip an account between Visitor and Other; the "Change type" block on the Visitors / Others detail views |
| `Attendees.View` | Attendees · View | — | `/admin/attendees`; `POST /admin/attendees/list`, `GET /admin/qr-lookup/{qrId}` |
| `Attendees.PrintBag` | Attendees · PrintBag | — | `/admin/print-bag` |
| `Roles.View` | Roles · View | — | `/admin/roles`; `POST /admin/roles/list`, `GET /admin/roles/{id}` |
| `Roles.Create` | Roles · Create | — | `POST /admin/roles` |
| `Roles.Edit` | Roles · Edit | — | `PUT /admin/roles/{id}` |
| `Roles.Delete` | Roles · Delete | — | `DELETE /admin/roles/{id}` |
| `Roles.AssignPermissions` | Roles · AssignPermissions | — | `GET/PUT /admin/roles/{id}/permissions`; the role→permission editor |
| `Interests.*` | Interests · View/Create/Edit/Delete | — | `/admin/interests`; `/admin/interests*` |
| `Countries.*` | Countries · View/Create/Edit/Delete | — | `/admin/countries`; `/admin/countries*` |
| `ProfileTypes.*` | ProfileTypes · View/Create/Edit/Delete | — | `/admin/profile-types/*`; `/admin/profile-types*` |

### Programme
| Code | Page · Action | Backing surface |
|---|---|---|
| `Themes.*` | Themes · View/Create/Edit/Delete | `/admin/themes`; `/admin/themes*` |
| `Sessions.*` | Sessions · View/Create/Edit/Delete | `/admin/sessions`; `/admin/sessions*` |
| `ProgrammeTimeline.View` | ProgrammeTimeline · View | `/admin/programme/timeline` |
| `Halls.*` | Halls · View/Create/Edit/Delete | `/admin/halls`; `/admin/halls*` (incl. `GET /admin/halls/{id}/schedule` — the hall occupancy view, QA B16, `Halls.View`) |
| `HallAvailability.View/Manage` | HallAvailability · View/Manage | `/admin/hall-availability`; `GET/POST /admin/halls/{id}/availability-windows`, `DELETE /admin/hall-availability-windows/{id}`, `GET /admin/halls/{id}/available-slots`. **QA A36** — hall-scoped, not `SpeakerMeetingRequests.*`: the free slots are read by BOTH meeting Approve modals (speaker + delegation), so a meeting-desk role needs `HallAvailability.View` (one grant) instead of the unrelated speaker-desk code. |
| `SeatLayouts.View/Edit` | SeatLayouts · View/Edit | `/admin/halls/seat-layouts` |
| `SeatPlans.View/Edit` | SeatPlans · View/Edit | `/admin/sessions/seat-plans`; seat-reservation admin |
| `Speakers.*` | Speakers · View/Create/Edit/Delete | `/admin/speakers` |
| `SessionModerators.View/Assign/Revoke` | SessionModerators | `/admin/session-moderators` |
| `SessionModeration.Moderate` | SessionModeration · Moderate | `/sessions/{id}/moderate`; admin comment + question moderation |

### Exhibition
| Code | Backing surface |
|---|---|
| `Companies.*` | `/admin/companies` |
| `Booths.*` | `/admin/booths` |
| `Sponsors.*` | `/admin/sponsors` |

### Engagement
| Code | Backing surface |
|---|---|
| `Comments.View/Moderate` | `/admin/comments-moderation` |
| `Ratings.View` | `/admin/ratings`; feedback admin list |

### Knowledge
| Code | Backing surface |
|---|---|
| `AiPrompts.View/Create/Edit/Delete/Test` | `/admin/ai/prompts` |
| `AiInvocations.View` | `/admin/ai/invocations` |

### Content
| Code | Backing surface |
|---|---|
| `ContentBlocks.View/Edit/Delete` | `/admin/content-blocks` (`Edit` = upsert) |
| `Banners.*` | `/admin/banners` |
| `Media.*` | `/admin/media` (`Edit` = update + image upload) |
| `News.*` | `/admin/news` (baseline: PublicRelations) |
| `MediaPartners.*` | `/admin/media-partners` |
| `Archive.*` | `/admin/archive` |

### System & operations
| Code | Baseline | Backing surface |
|---|---|---|
| `Statistics.View` | — | `/admin/statistics` |
| `Gates.Manage` † | — | `/admin/gates`, `/admin/gates/dashboard`; gate CRUD + reports |
| `Gates.Operate` † | GateOperator | `/admin/gates/operator`; operator scan submission |
| `Gates.ViewOwnReports` † | GateOperator | operator's own daily report |
| `Operations.View/Edit` | — | `/admin/operations`; registration + archive-visibility toggles |
| `Editions.View` | — | `/admin/editions`; the open year, when it opened, the last re-issue count |
| `Editions.Open` | — | opens a year — closes the current one into history and clears EVERY attendee's badge. Split from `View` because that is not an authority a viewer should hold |
| `OperationLog.View` | — | `/admin/operation-log` |
| `Logs.View` | — | `/admin/logs` |
| `Invitations.View` | PublicRelations | `/admin/invitations` list/detail |
| `Invitations.Manage` † | PublicRelations | invitation create/edit/delete |
| `Vips.View` † | PublicRelations | `/admin/vips` list |
| `Vips.Notify` † | PublicRelations | VIP bulk notify |

## Out of catalogue (intentionally ungated by permissions)

- **Anonymous auth endpoints** (`/auth/sign-in`, `/auth/sign-up`, OTP/TOTP
  verify, refresh, forgot/reset password, device-key challenge): `AllowAnonymous`.
- **Authenticated self-service** (`/account/*` — own profile, notifications,
  avatar, own TOTP, recovery codes; the dashboard `/`): gated by authentication
  + `account_state`, not by an admin permission. Any signed-in user reaches
  their own account surface.
- **Public read endpoints** (`/public/*`, public news/sessions/booths/speakers):
  `AllowAnonymous`.
