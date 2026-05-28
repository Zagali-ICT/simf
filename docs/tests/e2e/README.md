# SIMF E2E test catalogue

| | |
|--|--|
| **Authority** | D-133 slice 7 (2026-05-28) |
| **Format** | Gherkin-style scenarios, runner-agnostic (Chrome DevTools MCP today, Playwright after adoption) |
| **Coverage gate** | every ✅ Real row in [`docs/pages/PAGE-INDEX.md`](../../pages/PAGE-INDEX.md) maps to at least one P0 scenario here |
| **Companion** | [`Test-Guide.md`](../../manuals/Test-Guide.md) (how to run + how to extend) |

## How this folder is organised

- **`_TEMPLATE.md`** — copy this when you add a new per-page catalogue file.
- **`{cp|web|mobile}-{slug}.md`** — one file per page with the per-page
  Coverage matrix + scenarios.
- **Reference catalogue files (`cp-admin-interests.md`, `cp-auth-flow.md`)**
  are fully authored as the gold-standard examples. Use them as the
  shape for the rest.
- **This README** maps every page to its scenario IDs so the catalogue is
  browsable without opening 30 files.

## Per-page coverage index

Every page in `PAGE-INDEX.md` (✅ Real rows only) gets a row here.
`E2E-XXX-NNN` ids are stable — if a scenario is removed, the id retires
and is not reused.

### Control Panel — system / CRUD pages

| Page | File | Scenarios |
|------|------|-----------|
| `/admin/admins` | [`cp-admin-admins.md`](cp-admin-admins.md) _(pending file)_ | E2E-USR-001 .. 008 (full CRUD round-trip, bulk delete with reason, self-delete guard, duplicate, import 50-row XLSX, export selected, auth, RTL) |
| `/admin/admins/pending` | [`cp-admin-admins-pending.md`](cp-admin-admins-pending.md) _(pending file)_ | E2E-APN-001 .. 003 (approve, reject with reason, reason length gate) |
| `/admin/others` | [`cp-admin-others.md`](cp-admin-others.md) _(pending file)_ | E2E-OTH-001 .. 004 (walk-in Other, cross-kind ProfileTypeId rejected, cross-kind id on profile → 404, bulk delete) |
| `/admin/others/pending` | [`cp-admin-others-pending.md`](cp-admin-others-pending.md) _(pending file)_ | E2E-OPN-001 .. 003 |
| `/admin/visitors` | [`cp-admin-visitors.md`](cp-admin-visitors.md) _(pending file)_ | E2E-VIS-001 .. 008 (walk-in Saudi golden, walk-in non-Saudi Passport, Saudi ID validation, Details + ID image, cross-kind 404, bulk delete, export, RTL) |
| `/admin/visitors/pending` | [`cp-admin-visitors-pending.md`](cp-admin-visitors-pending.md) _(pending file)_ | E2E-VPN-001 .. 005 (D-128 approve-with-review, reject, reject length gate, ID image inline, stale row 404) |
| `/admin/interests` | **[`cp-admin-interests.md`](cp-admin-interests.md)** ✓ authored | E2E-INT-001 .. 007 |
| `/admin/profile-types/visitor` | [`cp-admin-profile-types-visitor.md`](cp-admin-profile-types-visitor.md) _(pending file)_ | E2E-VPT-001 .. 004 (Add → tile in wizard, edit name+color, in-use 409, cross-UserType reject) |
| `/admin/profile-types/other` | [`cp-admin-profile-types-other.md`](cp-admin-profile-types-other.md) _(pending file)_ | Mirror of E2E-VPT, ids E2E-OPT-001 .. 004 |
| `/admin/print-bag` | [`cp-admin-print-bag.md`](cp-admin-print-bag.md) _(pending file)_ | E2E-PRT-001 .. 006 (lookup known + unknown + Reset + Print + RTL + auth) |
| `/admin/reset-2fa` | [`cp-admin-reset-2fa.md`](cp-admin-reset-2fa.md) _(pending file)_ | E2E-2FA-001 .. 003 (reset normal user + email sent, self-reset rejected, email not found) |
| `/admin/logs` | [`cp-admin-logs.md`](cp-admin-logs.md) _(pending file)_ | E2E-LOG-001 .. 004 (pick project + file populates, tail polls, download streams, auth) |
| `/` (Dashboard) | [`cp-dashboard.md`](cp-dashboard.md) _(pending file)_ | E2E-DASH-001 .. 003 |

### Control Panel — auth + account flows

| Page(s) | File | Scenarios |
|---------|------|-----------|
| `/login` + `/login/totp` + `/login/recovery` + `/forgot-password` + `/auth/pending` + `/auth/rejected` | **[`cp-auth-flow.md`](cp-auth-flow.md)** ✓ authored | E2E-AUTH-001 .. 010 |
| `/account/profile` | [`cp-account-profile.md`](cp-account-profile.md) _(pending file)_ | E2E-PRF-001 .. 005 (update display name, avatar upload + crop, self-reset TOTP, regenerate recovery codes, revoke session) |
| `/account/notifications` | [`cp-account-notifications.md`](cp-account-notifications.md) _(pending file)_ | E2E-NTF-001 .. 007 (default mix read+unread, Details modal, per-row delete, bulk dismiss, mark all read, empty state, RTL) |
| `/account/totp-pairing` | [`cp-account-totp-pairing.md`](cp-account-totp-pairing.md) _(pending file)_ | E2E-TPP-001 .. 004 (scan QR + verify, manual-entry, wrong code retry, continue after codes) |

### Website

| Page | File | Scenarios |
|------|------|-----------|
| `/account` | [`web-home.md`](web-home.md) _(pending file)_ | E2E-WEB-HM-001 .. 002 |
| `/login` (Web) | [`web-login.md`](web-login.md) _(pending file)_ | E2E-WEB-LGN-001 .. 004 (visitor signs in, admin rejected on Web login, pending → /account/pending, rate limit) |
| `/login/verify` | [`web-otp-verify.md`](web-otp-verify.md) _(pending file)_ | E2E-WEB-OTP-001 .. 003 |
| `/forgot-password` (Web) | [`web-forgot-password.md`](web-forgot-password.md) _(pending file)_ | Mirrors E2E-FPW-001 .. 003 |
| `/reset-password` (Web) | [`web-reset-password.md`](web-reset-password.md) _(pending file)_ | E2E-WEB-RST-001 .. 004 |
| `/account/profile` (Web) | [`web-account-profile.md`](web-account-profile.md) _(pending file)_ | E2E-WEB-PRF-001 .. 005 (fill + save, QR card when Approved, no QR when Pending, Notifications link from header, RTL) |
| `/account/notifications` (Web) | [`web-account-notifications.md`](web-account-notifications.md) _(pending file)_ | E2E-WEB-NTF-001 .. 003 |

### Stub modules (D-134)

Stub modules (`/m/registration-requests` etc.) have no E2E coverage today
— catalogue rows added once the matching module ships. See
[`PAGE-INDEX.md`](../../pages/PAGE-INDEX.md) for the stub list.

## How to add a new catalogue file

1. Copy `_TEMPLATE.md` → `cp-{slug}.md` (or `web-` / `mobile-`).
2. Fill the front-matter (Page link, route, runner, auth).
3. Fill the Coverage matrix — one row per scenario with stable id
   `E2E-{NS}-{NNN}`. Pick a 3-letter namespace per page that doesn't
   collide with existing ones (`INT` Interests, `USR` Admins, `VIS`
   Visitors, `OTH` Others, `VPN`/`OPN`/`APN` pending pages, `VPT`/`OPT`
   ProfileTypes, `PRT` Print bag, `2FA` Reset 2FA, `LOG` Logs, `DASH`
   Dashboard, `AUTH` Auth flow, `PRF` Profile, `NTF` Notifications,
   `TPP` TOTP pairing, `WEB-*` Website variants, `FPW` Forgot password).
4. Author each scenario in Gherkin shape — keep step language tool-agnostic.
5. Add the row to this README's per-page coverage index.
6. Add a row in the linked per-page reference doc (`docs/pages/cp/{slug}.md`
   §11) pointing back at the catalogue file + scenario ids.

## Coverage status snapshot — 2026-05-28

- **Authored fully:** `cp-admin-interests.md`, `cp-auth-flow.md`,
  `_TEMPLATE.md`
- **Scoped (matrix in this README, file pending):** 22 more CP files +
  9 Website files
- **Total scenario count:** ~120 across all 31 pages once fully authored.

D-133 slice 7 commits the template + 2 worked examples + this index.
Subsequent commits fill the remaining 31 per-page catalogue files using
the same shape — each one is ~80–150 lines.
