# Email templates — `/admin/email/templates`

| | |
|--|--|
| **Route** | `/admin/email/templates` |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(PermissionCatalog.EmailTemplates.View)]` (page) + per-action permission at the API — reads (`list` / `{type}` / `preview`) gated `EmailTemplates.View`, writes (`PUT {type}` / `{type}/reset`) gated `EmailTemplates.Edit` — plus `RequireApprovedAccount`. |
| **Pattern** | D-735 DB-backed **override** editor for the six transactional identity emails. Bilingual, token-templated messages with a code catalogue supplying defaults; `SimfDataGrid`-based fixed six-row list feeding an inline editor. |
| **Status** | ✅ Real (D-735) |
| **Backend endpoints** | BFF `/account/api/admin/email/templates/*` → API `/api/v1/admin/email/templates/*`: `POST .../list`, `GET .../{type}`, `PUT .../{type}`, `POST .../{type}/reset`, `POST .../{type}/preview` |
| **Tests** | [`docs/tests/e2e/cp-admin-email-templates.md`](../../tests/e2e/cp-admin-email-templates.md) (E2E-EMT-001..013) |
| **Last reviewed** | 2026-07-10 |

## 1. Purpose

The single place for an administrator to edit the wording of the six
**transactional identity emails** the platform sends — the sign-in OTP, the
sign-up email-verification code, the "an account already exists" notice, the
password-reset code, the badge-activation code, and the biometric step-up code —
without a redeploy. Each email is a **bilingual (EN + AR), token-templated**
message. The database stores **overrides only**: a code **catalogue** supplies the
built-in default subject + body for every type, so the table starts empty and the
resolver always falls back to the default when no override exists. The admin walks
in to reword an email, preview it with realistic sample values, and either save the
override or reset back to the built-in copy.

## 2. Audience + permissions

- **Who can reach it:** any admin whose role grants `EmailTemplates.View`
  (`Administrator = "*"` always qualifies).
- **Who can edit/write on it:** admins whose role also grants `EmailTemplates.Edit`
  (Save + Reset). A `View`-only admin gets a **read-only** editor (the live preview
  still works; Save / Reset are hidden via `<AuthorizedAction Permission="EmailTemplates.Edit">`
  and the API returns 403 if forced).
- **Authorisation gates:** page `[RequirePermission(PermissionCatalog.EmailTemplates.View)]`;
  API `Policies(PermissionCatalog.PolicyFor(EmailTemplates.View | .Edit), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
- **What an unauthenticated user sees:** redirect to `/login`; a session-less API
  call is 401. A signed-in admin lacking `EmailTemplates.View` is redirected to
  `/not-permitted` and the "Email templates" nav item is hidden.

## 3. The six templates + their tokens

The set is **fixed** — one row per `EmailTemplateType`; there is no create or delete.
Tokens are single-brace placeholders (`{Code}`, `{ExpiryMinutes}`), and each type
declares its own allowed token set in the catalogue.

| `EmailTemplateType` | Email | Tokens |
|---------------------|-------|--------|
| `SignInOtp` | The one-time code emailed as the sign-in second factor (OTP) | `{Code}`, `{ExpiryMinutes}` |
| `EmailVerification` | The code emailed at sign-up to verify the email address | `{Code}`, `{ExpiryMinutes}` |
| `AccountExists` | The "an account already exists for this email — sign in or reset instead" notice (anti-enumeration) | notice-only (no code token) |
| `PasswordReset` | The code emailed for the forgot-password flow | `{Code}`, `{ExpiryMinutes}` |
| `BadgeActivation` | The code emailed for passwordless badge activation (D-430) | `{Code}`, `{ExpiryMinutes}` |
| `BiometricStepUp` | The code emailed to enrol / step-up a biometric device key (D-486 / D-554) | `{Code}`, `{ExpiryMinutes}` |

## 4. UI

- `SimfBanner` for the page title, then a `SimfDataGrid` of
  `AdminEmailTemplateSummary` (one row per catalogue template) inside
  `.simf-page-wide` / `.simf-surface`.
- **List query.** `POST .../list` takes the standard `GridQuery` and applies it:
  `Skip`/`Top` page the result, `Total` is the count **after** filtering (not the
  page length), `Search` matches the type name and the subject, and `Sort` /
  `Filters` accept `type`, `subject`, `customised`, `version` and `updatedAt`.
  Keys match case-insensitively and an unrecognised key is a bilingual 400
  (`GRID_SORT_KEY_INVALID` / `GRID_FILTER_KEY_INVALID`), never a silently ignored
  clause. The rows come from the code catalogue joined to the override table
  rather than from a queryable, so the query is applied in memory instead of
  through the `ToGridPageAsync` seam; see the note on `ListAsync`.
- Grid columns: **Template** (the type + its human name) and **Override** — a
  `SimfPill` reading **"Customised"** when a DB override exists (`IsOverride=true`)
  or **"Default"** when it falls back to the built-in copy (`IsOverride=false`). A
  `Version` (the override revision) is shown for customised rows.
- No Add / Delete / Import (the catalogue is closed); each row opens the **editor**
  for that type.
- **Editor** (opened per row via `GET .../{type}`): Subject (English) + Subject
  (Arabic), Body (English) + Body (Arabic), a row of **token chips** for the type's
  allowed tokens, a live **bilingual Preview** panel, a **Save** button and a
  **Reset to default** button (both wrapped in `<AuthorizedAction Permission="EmailTemplates.Edit">`).
- **Token chips:** clicking a chip (e.g. `{Code}`) splices the single-brace
  placeholder into whichever body field currently holds the cursor (EN or AR) — it
  is not appended to the end and it does not cross into the other language field.
- **Live preview:** as the body changes, the editor calls
  `POST .../{type}/preview`, which renders the message with **sample values**
  (`{Code}` → `123456`, `{ExpiryMinutes}` → `10`) and returns
  `EmailTemplatePreviewResult { Subject, HtmlBody, UnknownTokens }`. When
  `UnknownTokens` is non-empty the editor shows an inline "unknown placeholder"
  warning and **disables Save**.
- **Reset to default:** removes the override for that type through a `SimfConfirm`
  gate; the email then reverts to the built-in catalogue copy.
- Toasts surface via a `SimfAlert`: success on Save / Reset, error on load / API
  failure.

## 4.5 Form fields

The editor edits an override for one `EmailTemplateType`. Fields and their
server-side rules:

| Field | Required | Rule | Notes |
|-------|----------|------|-------|
| Subject (English) | yes | non-empty | overrides the catalogue default subject |
| Subject (Arabic) | yes | non-empty | Arabic subject |
| Body (English) | yes | non-empty; only the type's allowed tokens | empty body or unknown `{token}` → 400 `EMAIL_TEMPLATE_INVALID` |
| Body (Arabic) | yes | non-empty; only the type's allowed tokens | empty body or unknown `{token}` → 400 `EMAIL_TEMPLATE_INVALID` |

The **type** itself is not a field — it is fixed by the row/route (`{type}` is an
`EmailTemplateType` name). Client-side the editor disables Save while the preview
reports `UnknownTokens`; the authoritative check is server-side on `PUT`.

## 5. Data flow + endpoints

All calls go through the BFF proxy at `/account/api/admin/email/templates/*`, which
forwards to the API at `/api/v1/admin/email/templates/*`. Each returns the
`ApiResult<T>` envelope. `{type}` is an `EmailTemplateType` **name**.

| Action | BFF route | API endpoint | Permission | Response |
|--------|-----------|--------------|------------|----------|
| List grid | `POST .../list` | `POST .../list` | `EmailTemplates.View` | `GridPage<AdminEmailTemplateSummary>` |
| Get detail | `GET .../{type}` | `GET .../{type}` | `EmailTemplates.View` | `AdminEmailTemplateDetail` |
| Save override | `PUT .../{type}` | `PUT .../{type}` | `EmailTemplates.Edit` | `AdminEmailTemplateDetail` (Version bumps) |
| Reset to default | `POST .../{type}/reset` | `POST .../{type}/reset` | `EmailTemplates.Edit` | `AdminEmailTemplateDetail` (override removed) |
| Preview | `POST .../{type}/preview` | `POST .../{type}/preview` | `EmailTemplates.View` | `EmailTemplatePreviewResult { Subject, HtmlBody, UnknownTokens }` |

- **List** returns all six types over the override table; each summary carries
  `IsOverride` (and `Version` when overridden). An empty override table still lists
  six rows, all `IsOverride=false`.
- **Get detail** returns the effective copy — the override if one exists, otherwise
  the catalogue default — plus `IsOverride` and the type's allowed token list.
- **Save (PUT)** writes/updates the override and **bumps `Version`**. It re-validates
  the body server-side (see §6).
- **Reset** **deletes** the override row (it does not persist a copy of the default),
  so a subsequent Get falls back to the catalogue default and the grid reads
  "Default" again.
- **Preview** renders the supplied draft with sample values without saving, and
  reports any `UnknownTokens` the body references.

## 6. Validation + error handling

- **`EMAIL_TEMPLATE_INVALID` (400)** — the body is empty, or it references a
  placeholder that is not in the type's allowed token set (e.g. `{Foo}`). The
  message is bilingual (EN + AR) and, for an unknown token, names the offending
  placeholder. Returned on `PUT`.
- **`EMAIL_TEMPLATE_NOT_FOUND` (404)** — an unrecognised `{type}` on any of
  `GET` / `PUT` / `reset` / `preview` (e.g. a hand-edited deep link).
- **Unknown-token guard is enforced twice** — the editor disables Save when the
  preview's `UnknownTokens` is non-empty (client guard), and `PUT` re-validates
  server-side (400).
- **Toast strategy:** success on Save ("Template saved." / "تم حفظ القالب.") and
  Reset ("Reset to default." / "تمت الإعادة إلى الافتراضي."); on any API failure the
  CP surfaces `ApiResult.Error.MessageForCurrentCulture()`, falling back to a
  bilingual "Could not load email templates." / "تعذّر تحميل قوالب البريد." when no
  message is present.

## 7. Edge cases

- **Fixed set, no CRUD.** The six types are the whole catalogue — there is no Add,
  Delete, Import or rename; only edit + reset per type.
- **Overrides-only + fallback.** A fresh install has an empty override table yet the
  page lists six templates; every send falls back to the built-in catalogue default
  until an override is saved.
- **Reset removes, not copies.** Reset deletes the override so the email reverts to
  the built-in copy; it never writes a duplicate of the default back into the table.
- **Per-type token sets.** `{Code}` / `{ExpiryMinutes}` are valid on the
  code-delivery templates; `AccountExists` is notice-only, so a `{Code}` in its body
  would be an unknown token. `{Foo}` is never valid on any type.
- **Preview never persists.** It renders a draft with sample values and reports
  `UnknownTokens`; it writes nothing.
- **View vs Edit.** A `View`-only admin can read and preview but cannot Save or
  Reset (the buttons are gated and the API denies 403).

## 8. i18n + RTL

The whole page and editor are bilingual and mirror to RTL under the Arabic locale
(banner, grid headers Template / Override, nav rail, editor fields, token chips,
preview, and the Save / Reset buttons). Each template stores **both** an English and
an Arabic subject + body, and the live preview renders both languages side-by-side.
Token placeholders are the same single-brace literals in both languages — the chip
inserts `{Code}` / `{ExpiryMinutes}` unchanged regardless of locale. Server error
messages are themselves bilingual (EN + AR).

## 10. Use cases

- Reword the sign-in OTP email in the organisation's own voice, preview it with the
  `123456` / `10` sample values, and save the override.
- Localise or soften the "account already exists" notice in both languages.
- Roll a template back to its shipped copy with Reset-to-default.
- Audit which of the six emails have been customised at a glance (the "Customised"
  vs "Default" pill on the grid).

## 11. E2E

See [`docs/tests/e2e/cp-admin-email-templates.md`](../../tests/e2e/cp-admin-email-templates.md):
E2E-EMT-001 golden path (open SignInOtp → insert `{Code}` chip → preview `123456` →
Save → Version bump + "Customised"), 002 six-row list with the `IsOverride` flag,
003 auth gate (anonymous → login/401), 004 auth gate (admin lacking
`EmailTemplates.View` → 403), 005 token-chip insertion, 006 live bilingual preview,
007 Save blocked on unknown `{Foo}` (`EMAIL_TEMPLATE_INVALID` + preview
`UnknownTokens`), 008 Save blocked on empty body (`EMAIL_TEMPLATE_INVALID`), 009
reset-to-default removes the override, 010 invalid `{type}` (`EMAIL_TEMPLATE_NOT_FOUND`),
011 server-500 fallback, 012 RTL, 013 Edit-permission gate (View-only → Save/Reset 403).

## 12. Related docs

- Permissions: `src/Shared/SIMF.Common/PermissionCatalog.cs` (`EmailTemplates`
  nested class) + `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`.
- The emails these templates back: sign-in OTP / email verification / password reset
  (Login-API auth flow), badge activation (D-430), biometric step-up (D-486 / D-554).
- API contract: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — the
  `ApiResult<T>` envelope + error model.
- Decisions: D-735 (this page).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-10 | D-735 | New page — DB-backed override editor for the six transactional identity emails (bilingual token templates, token chips, live bilingual preview with sample values, block-on-unknown-token, reset-to-default). `EmailTemplates.View` / `.Edit` permission split; overrides-only table with a code-catalogue fallback. |

_Last reviewed:_ 2026-07-10 by Claude (D-735 — Email templates admin editor).
