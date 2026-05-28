# SIMF — Administrator Manual

| | |
|--|--|
| **Audience** | SIMF Control Panel administrators (operators of `/admin/*` and `/m/*`) |
| **Scope** | Every CP module + the auth + account-area surfaces |
| **Authority** | D-133 (2026-05-28) — vertical slice; remaining chapters listed in §1.4 |
| **Bilingual** | Yes — Arabic translations land alongside each chapter (translator-pending) |
| **Companion docs** | [`User-Manual.md`](User-Manual.md) (Website + Mobile), [`Developer-Guide.md`](Developer-Guide.md), [`docs/pages/PAGE-INDEX.md`](../pages/PAGE-INDEX.md) |

This manual is **the daily operator playbook**. Every module the administrator
can reach in the Control Panel has its own chapter that explains: what the
module is for, how to do the most common tasks, what each control does, and
what to do when something goes wrong. The chapters track 1-to-1 against
[`docs/pages/PAGE-INDEX.md`](../pages/PAGE-INDEX.md) — when a row there
shows ✅ Real, this manual has a chapter for it; when 🚧 Stub, the chapter
is a placeholder marked _(planned)_.

> **Reading this manual:** start with §2 if you've never signed in before;
> jump to a specific module chapter (§4 onwards) when you have a job to do.
> Every section ends with a **Troubleshooting** subsection that covers the
> top 3 things that go wrong.

---

## 1. Contents

1. Introduction (this section)
2. Sign in + first-time setup
3. Daily walkthrough: the Control Panel shell
4. People modules
   1. _(planned)_ Registration requests
   2. _(planned)_ Attendees
   3. Print badge desk
   4. _(planned)_ Roles & permissions
5. Programme modules _(planned)_
6. Exhibition modules _(planned)_
7. Engagement modules _(planned)_
8. Knowledge & AI modules _(planned)_
9. Content modules _(planned)_
10. System modules
    1. Admins (`/admin/admins`)
    2. Pending admins
    3. Others (`/admin/others`)
    4. Pending others
    5. Visitors (`/admin/visitors`)
    6. Pending visitors
    7. **Interests** (`/admin/interests`) — full chapter below as the first
       fully-authored module
    8. Visitor profile types
    9. Other profile types
    10. Reset user 2FA
    11. Logs viewer
12. Account-area surfaces
    1. Profile
    2. Notifications inbox
    3. TOTP pairing (first-time setup)
13. Security boundaries
14. Troubleshooting index
15. Glossary

### 1.4 Coverage status

The vertical slice authored under D-133 covers the entry-point chapters
(§1–§3), the **Interests** chapter (§10.7) as a reference for the rest, plus
the structural scaffolding above. The remaining chapters land in subsequent
D-133 expansion commits, one per module, each cross-referenced against
[`docs/pages/PAGE-INDEX.md`](../pages/PAGE-INDEX.md).

---

## 2. Sign in + first-time setup

### 2.1 What you need

- Your CP email address (provisioned by another administrator or self-registered
  + approved).
- Your password (sent to you out-of-band by the inviting admin, or set during
  self-registration).
- An authenticator app on your phone (e.g. Google Authenticator, Authy,
  Microsoft Authenticator). SIMF uses TOTP (Time-based One-Time Password) for
  the second factor — your authenticator generates a fresh 6-digit code every
  30 seconds.

### 2.2 First sign-in (TOTP pairing)

1. Open the Control Panel URL (provided by your team — typically
   `https://cp.simf.local`).
2. Enter your email + password → **Sign in**.
3. The browser sends you to `/account/totp-pairing`. The page shows a QR code
   and a manual-entry secret.
4. Open your authenticator app → **Add account** → **Scan QR code** → point
   the camera at the screen. The app starts generating 6-digit codes for SIMF.
5. Type the current 6-digit code into the Verify field on the page → **Pair**.
6. The page shows your 10 single-use **recovery codes**. Save them in a
   password manager or print them and store offline. Each recovery code works
   once and is your only way back in if you lose your phone.
7. The page sends you to the Dashboard.

### 2.3 Subsequent sign-ins

1. Email + password → **Sign in**.
2. The browser sends you to `/login/totp`. Open your authenticator, read the
   current 6-digit code, type it in → **Verify**.
3. You land on the Dashboard or wherever you were trying to go.

### 2.4 Lost your phone

Click **Use a recovery code instead** on the TOTP page. Enter one of your
saved recovery codes → **Verify**. The code burns on use. As soon as you're
in, go to **My profile → Reset 2FA** (or have another administrator do it
via **System → Reset user 2FA**) to re-pair.

### 2.5 Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Invalid email or password" | Typo / Caps Lock / account not yet approved | Re-type carefully; if the account is still PendingApproval the message will say so explicitly |
| "Invalid verification code" | Phone clock drift (>30 s off) OR code expired before you submitted | Open your phone settings → set time automatically; type the next 6-digit code |
| Stuck on `/login/totp` after refresh | Cookie session expired during pairing | Click **Back to sign in**, re-enter email + password |
| Browser says "Account pending approval" | Another admin hasn't approved your account yet | Reach out to the admin team via the channel listed in your invite email |

---

## 3. Daily walkthrough: the Control Panel shell

When you sign in, the shell layout has four regions:

```
┌────────────────────────────────────────────────────────────────────┐
│ ☰ SIMF │ 2026                  العربية  Dark  🔔  You   [Sign out] │ ← header
├────────┬───────────────────────────────────────────────────────────┤
│ Nav    │                                                           │
│ rail   │             Page content (banner + body)                  │
│        │                                                           │
└────────┴───────────────────────────────────────────────────────────┘
```

**Header:**
- **☰** — collapse/expand the left nav.
- **SIMF / 2026** — home link → Dashboard.
- **العربية** — switch language (round-trips the same page in the other locale).
- **Dark / Light** — theme toggle. Persists per user.
- **🔔** — your notification bell. Number badge = unread count. Click → menu
  with the latest notifications + **View all** → `/account/notifications`.
- **You** — links to **My profile** (`/account/profile`).
- **Sign out** — ends the session.

**Left nav rail (9 groups):**
- Each group has a header (Overview, People, Programme, Exhibition, Engagement,
  Knowledge & AI, Content, Communications, System).
- A grey **SOON** tag next to a menu entry means the module is not built yet —
  clicking it shows a "Coming soon" placeholder. _(D-132)_
- The page you're currently on is highlighted with the brand accent.

**Page content:**
- Every page that's not a stub starts with a **branded banner** (page title
  on a sunken-surface strip).
- CRUD list pages render the canonical grid (Select-all toolbar, per-row
  checkboxes, Add / Edit / Details / Delete buttons, full pager).
- Modals overlay the page when you click Add / Edit / Details; the rest of
  the page dims and is inert until you close them.

### 3.1 The canonical CRUD list pattern

You'll meet the same grid pattern on every CRUD page (Admins, Others, Visitors,
Interests, Profile types, etc.). Once you know it, you know all of them:

| Affordance | Where | What it does |
|------------|-------|--------------|
| **Select all** | toolbar | Tick every row on the current page |
| **+ Add** | toolbar | Opens a modal to create a new row |
| **✎ Edit** | per-row | Opens a modal to edit that row |
| **ⓘ Details** | per-row | Opens a read-only modal with every field of that row |
| **🗑 Delete / Deactivate** | per-row | Removes the row (soft-delete in most cases) |
| Per-column **▲▼** | header | Sort the table |
| Per-column **Search** | under header | Filter by that column |
| **« ‹ 1 2 3 › »** | pager | First / Prev / numbered / Next / Last page |
| **Show 10/20/50/100** | pager | Page size |
| **Showing X–Y of Z** | pager | Where you are in the result set |

> **Pattern reference:** [`docs/dev/SIMF_TABLE_PATTERN.md`](../dev/SIMF_TABLE_PATTERN.md)
> is the authoritative spec.

### 3.2 Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Menu item shows SOON tag | Module not built yet (22 of 36 modules as of D-132) | Ask the team for an ETA; check [`PAGE-INDEX.md`](../pages/PAGE-INDEX.md) |
| Page shows "Coming soon" | Same — you clicked a stub | Same |
| "Authentication is required" toast | Cookie / token expired (rare — D-121 handles this) | Refresh the page; if it persists, sign out and back in |
| Browser tab title is wrong | Manual chapter / page doc out of date | File a ticket with the page route |

---

## 4. People modules

### 4.1 _(planned)_ Registration requests — `/m/registration-requests`

_(stub — see [`PAGE-INDEX.md`](../pages/PAGE-INDEX.md))_

### 4.2 _(planned)_ Attendees — `/m/attendees`

_(stub)_

### 4.3 Print badge desk — `/admin/print-bag`

_(chapter pending — D-133 expansion. Page reference: [`docs/pages/cp/admin-print-bag.md`](../pages/cp/admin-print-bag.md))_

### 4.4 _(planned)_ Roles & permissions — `/m/roles`

_(stub)_

---

## 10. System modules

### 10.1 Admins — `/admin/admins`

_(chapter pending — D-133 expansion. Page reference: [`docs/pages/cp/admin-admins.md`](../pages/cp/admin-admins.md))_

### 10.2 Pending admins — `/admin/admins/pending`

_(chapter pending)_

### 10.3 Others — `/admin/others`

_(chapter pending)_

### 10.4 Pending others — `/admin/others/pending`

_(chapter pending)_

### 10.5 Visitors — `/admin/visitors`

_(chapter pending)_

### 10.6 Pending visitors — `/admin/visitors/pending`

_(chapter pending)_

### 10.7 Interests — `/admin/interests`

> Page reference: [`docs/pages/cp/admin-interests.md`](../pages/cp/admin-interests.md)

#### What it's for

Interests are the topics visitors pick from when they fill their profile
(e.g. "Naval engineering", "Maritime law", "Logistics"). The list shows in
the visitor self-service profile picker. Keeping it accurate is your job:
add a topic when a new stream is announced, deactivate one that stopped
being relevant, reorder them so the most popular sit at the top.

#### Most common tasks

##### Add a new interest

1. **System → Interests** in the left nav (or paste `/admin/interests` in
   the URL bar).
2. Click **+ Add interest** in the toolbar.
3. Fill the modal:
   - **Name (English)** — the visitor-facing label, 1–128 characters, must
     be unique across the system.
   - **Name (Arabic)** — the Arabic translation, 1–128 characters.
   - **Display order** — a number ≥ 0; lower numbers sort first in the
     visitor picker.
4. Click **Create interest**.
5. The modal closes, the grid reloads, and a green toast says
   `Interest "..." was created.` The new row appears in the list.

##### Edit an interest

1. Find the row (use the column filters if the list is long).
2. Click the **✎ Edit** icon in the row's Actions column.
3. Adjust the fields. Edit also lets you tick / untick **Active — show in
   the visitor picker** to deactivate without deleting.
4. Click **Save changes**. Toast: `Interest "..." was updated.`

##### View an interest's full details (read-only)

1. Click the **ⓘ Details** icon in the row.
2. Modal lists Name, Name (Arabic), Display order, and Active state.
3. Click **Close** when done.

##### Deactivate an interest

1. Click the **🗑 Deactivate** icon in the row.
2. The action is immediate (no confirm modal — soft-delete only, no data is
   destroyed). Toast: `Interest "..." was deactivated.`
3. The row now shows a grey **Inactive** pill. Visitors will not see this
   interest in their picker on next load, but visitors who had already
   linked to it keep the link.
4. To reactivate: Edit the row → tick **Active** → Save.

##### Reorder the visitor picker

1. Decide the order you want.
2. Edit each row and adjust **Display order** to a number that places it
   correctly (e.g. set the top-of-list interest to `0`, the next to `10`,
   the next to `20` — leave gaps so future interests can slot in
   without renumbering everything).
3. The next time a visitor opens their profile picker, they see the new order.

#### What the page looks like

![Interests canonical view](../screenshots/d132-interests-canonical.png)

![Add interest modal](../screenshots/d132-interests-add-modal.png)

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Interest "X" was created.` doesn't appear in the list | The list filter is hiding it | Clear the column filters; reload the page if needed |
| Toast: "An interest with this name already exists" (or Arabic equivalent) | The English name is not unique (case-insensitive) | Pick a different name; you can still set the existing one's Display order if it should sort higher |
| Toast: "The operation could not be completed." | Server or network error | Reload; if it persists, check the [Logs viewer](#108-logs-viewer) |
| Add modal won't submit (button stays disabled) | One of the three fields failed client validation | The form shows the field-level error in red under the field |
| `/admin/interests/new` shows 404 | This deep-link was deleted in D-132 | Use the **+ Add interest** button on the list page |

#### What you cannot do here

- **Bulk-deactivate** — the checkboxes render for consistency but no bulk
  action is wired. Deactivate one row at a time.
- **Reorder by drag-and-drop** — use the Display order field.
- **Delete permanently** — Deactivate is soft-only. Permanent delete is not
  exposed; if a row must vanish, ask a developer.
- **Translate to a third language** — only English + Arabic are supported.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-interests.md`](../pages/cp/admin-interests.md)
- API spec: [`SIMF-API-001`](../SIMF-API-001-API-Specification.md) — `/admin/interests` endpoints
- E2E tests: [`docs/tests/e2e/cp-admin-interests.md`](../tests/e2e/cp-admin-interests.md) _(pending)_
- Pattern: [`docs/dev/SIMF_TABLE_PATTERN.md`](../dev/SIMF_TABLE_PATTERN.md)
- Use cases: `SIMF-UCS-001 § UC-INT-*` _(pending)_

### 10.8 Visitor profile types — `/admin/profile-types/visitor`

_(chapter pending)_

### 10.9 Other profile types — `/admin/profile-types/other`

_(chapter pending)_

### 10.10 Reset user 2FA — `/admin/reset-2fa`

_(chapter pending)_

### 10.11 Logs viewer — `/admin/logs`

_(chapter pending)_

---

## 13. Security boundaries

- Every `/admin/*` page requires the `Administrator` role AND an `Approved`
  account state. Pending or Rejected admins cannot reach admin pages even
  with a valid cookie.
- Every state-changing API call is row-audited (D-109) — the actor, the
  before/after row, the timestamp.
- TOTP is mandatory for every CP user — there is no "remember this device"
  bypass for admin accounts.
- Recovery codes are one-time-use. If you use one, generate fresh codes via
  **My profile → Recovery codes** on your next session.
- The session refresh is automatic up to the cookie's 8-hour lifetime
  (D-121); after that you sign in again.

## 14. Troubleshooting index

| Looking for | Section |
|-------------|---------|
| Can't sign in | §2.5 |
| Stuck on TOTP page | §2.5 |
| Lost phone | §2.4 |
| Menu item shows SOON | §3.2 |
| Add interest fails | §10.7 |
| Where's the logs viewer | §10.11 |

## 15. Glossary

- **TOTP** — Time-based One-Time Password. The 6-digit code your authenticator
  generates every 30 seconds.
- **PendingApproval** — account state for self-registered users awaiting
  admin approval.
- **Approved** — fully signed-off account; the only state that can sign in.
- **Rejected** — admin actively turned the account down; can sign in to see
  the rejection reason but nothing else.
- **Soft-delete** — sets `IsActive = false` on the row; data is preserved.
- **Modal** — overlay dialog over the page; ESC or the × button closes it.
- **BFF** — Backend-For-Frontend; the `/account/api/*` routes that proxy
  to the real `/api/v1/*` API.
- **QR badge** — the encrypted QR code minted for each Approved visitor.
  Scanning it at the venue gate proves identity.
- **Walk-in** — a visitor registered at the on-site registration desk by
  staff (`/admin/visitors` → Add).
- **Lookup table** — a small reference list (e.g. Interests, Profile types)
  managed via simple CRUD; no workflow.

---

_Last reviewed:_ 2026-05-28 by Claude (D-133 vertical slice).
_Next milestone:_ author one more module chapter (Admins or Visitors)
per follow-up commit so the operator has a multi-module reference before the
remaining 25+ chapters land.
