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
    2. **Notifications inbox** — chapter authored below in §12.2
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

> Page reference: [`docs/pages/cp/admin-print-bag.md`](../pages/cp/admin-print-bag.md)

#### What it's for

The print desk reprints visitor badges by **QR id**. Visitors who lost or
damaged their badge come here; you look them up by QR id (scan or type),
the page renders the same colour-coded badge they had originally, and you
click Print.

#### Most common tasks

##### Reprint a badge

1. **People → Print badge** in the left nav (or paste `/admin/print-bag`).
2. Plug in your USB barcode scanner OR be ready to type the 12-character
   QR id.
3. Place the QR (or type it) into the **QR id** input.
4. Click **Search** (or press Enter).
5. The badge renders with the visitor's profile-type colour, name, QR SVG,
   and QR id.
6. Click **Print**. The browser print dialog opens with only the badge
   visible (the page header, nav, and toolbar are hidden by the print CSS).
7. Click **Reset** to clear the form for the next visitor.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Visitor not found" | QR id typo OR visitor was deleted | Re-scan; if the QR is damaged, look the visitor up by email on `/admin/visitors` and read their QR id from the Details modal |
| Print dialog shows the whole page instead of just the badge | Browser blocked the print stylesheet | Use Chrome or Edge; check **Print preview → More settings → Background graphics** is on |
| QR id input doesn't accept input | Browser autocomplete grabbed focus | Click directly in the input; the input has `autocomplete="off"` but some scanners send a leading tab |

#### What you cannot do here

- **Bulk-reprint** — one visitor at a time.
- **Edit the visitor while you're here** — go to `/admin/visitors` → find
  → Edit (when User Management ships).
- **Re-issue a different QR id** — the QR is minted at registration and is
  permanent. If a QR is compromised, the visitor must be re-registered.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-print-bag.md`](../pages/cp/admin-print-bag.md)
- Walk-in registration (where the QR is minted): [`docs/pages/cp/admin-visitors.md`](../pages/cp/admin-visitors.md)
- Decision: D-130.

### 4.4 _(planned)_ Roles & permissions — `/m/roles`

_(stub)_

---

## 10. System modules

### 10.1 Admins — `/admin/admins`

> Page reference: [`docs/pages/cp/admin-admins.md`](../pages/cp/admin-admins.md)

#### What it's for

Lists every account with the `Administrator` role. This is where you onboard
a new admin colleague, view someone's details, deactivate a departing admin,
or pull the admin roster into Excel.

#### Most common tasks

##### Invite a new administrator

1. **System → Admins** → **+ Add**.
2. Fill the modal: Email, Display name, Password (min 12 chars + 1 digit +
   1 upper + 1 lower + 1 special), TOTP-on-first-login (leave on).
3. Click **Create administrator**. The new row appears Approved.
4. Send the new admin their email + password out-of-band; they'll go through
   first-time TOTP pairing on first sign-in (§2.2).

##### View an admin's details

Click the **ⓘ Details** icon on the row → read-only modal with email,
display name, state, role.

##### Deactivate a departing admin

1. Either: select the row + **Delete** in the toolbar (bulk-delete modal
   asks for a 10–500 character reason)
2. Or: click the **🗑 Delete** icon on the row (same reason modal).
3. Type the reason (this is preserved in the audit log).
4. Click **Delete**. The row vanishes. Self-delete is silently skipped — you
   cannot delete your own account.

##### Bulk-delete several admins

Tick the rows you want → **Delete** in the toolbar → reason → Submit.
The toast tells you how many were deleted vs skipped (self-delete or
unknown id).

##### Duplicate an admin

Useful when you're standing up a sister account (e.g. shared service account).
Per-row **Duplicate** icon → enter the new email → Submit. The new account
has the same role + state, fresh QR.

##### Import / Export

- **Export** — select rows + Export, OR export the entire current query
  (no selection) → XLSX downloads.
- **Import** — Import → XLSX upload (≤ 5 MB) → review the result modal
  showing created / skipped / errors per row.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Edit button does nothing useful | Edit is a stub awaiting the User Management module | Use Delete + re-Add as a workaround |
| Bulk-delete reports "Deleted N, skipped 1" | Your own row was in the batch | Expected — self-delete is silently skipped |
| Import shows 50 errors | XLSX header row missing or wrong column names | Open the Export sample as the template, re-fill, re-upload |
| Toast: "Email already exists" | Trying to invite a duplicate | Find the existing admin first; if Deactivated, ask a developer (no re-activate exists yet) |

#### What you cannot do here

- **Edit existing admin fields** (Edit modal is a stub — awaits the User
  Management module).
- **Reset their 2FA** — go to `/admin/reset-2fa` (per-target reset).
- **Change their role** — admins are role-pinned at creation; you'd have to
  delete + recreate.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-admins.md`](../pages/cp/admin-admins.md)
- Pattern: [`SIMF_TABLE_PATTERN.md`](../dev/SIMF_TABLE_PATTERN.md)
- API: `/admin/admins/*` endpoint group in [`SIMF-API-001`](../SIMF-API-001-API-Specification.md)
- Sibling: §10.2 Pending admins (approval queue for self-registered admins).

### 10.2 Pending admins — `/admin/admins/pending`

> Page reference: [`docs/pages/cp/admin-admins-pending.md`](../pages/cp/admin-admins-pending.md)

Queue of self-registered admin candidates. Per-row **Approve** (one-click,
no preview today — parity gap with the Visitor/Other equivalents) and
**Reject** (10–500 char reason). Always cross-check the candidate offline
before approving since there's no preview modal yet.

### 10.3 Others — `/admin/others`

> Page reference: [`docs/pages/cp/admin-others.md`](../pages/cp/admin-others.md)

Walk-in registration for non-visitor non-admin attendees (sponsor staff,
exhibitor reps, press, contractors). Same wizard as `/admin/visitors`,
except: no Interests section, and the profile-type tiles come from
**Other profile types** (not Visitor). Make sure at least one Other
profile-type is seeded under §10.9 before run-time.

### 10.4 Pending others — `/admin/others/pending`

> Page reference: [`docs/pages/cp/admin-others-pending.md`](../pages/cp/admin-others-pending.md)

Approval queue for Other-typed self-registrations. Same View / Approve-with-
review / Reject-with-reason flow as `/admin/visitors/pending` (§10.6).

### 10.5 Visitors — `/admin/visitors`

> Page reference: [`docs/pages/cp/admin-visitors.md`](../pages/cp/admin-visitors.md)

#### What it's for

The **event-day workhorse**. On the day of the forum, exhibition staff at the
registration desk use this page to register walk-in visitors face-to-face.
Off-day, admins use it to audit the visitor roster, view full profiles (with
ID-document image), export the attendee list, and reach the reprint desk.

#### Most common tasks

##### Register a walk-in visitor (event day)

1. **System → Visitors** → **+ Add**.
2. The walk-in wizard opens with **6 numbered sections** (D-131):
   1. **Badge type** — click the colour-coded tile for the visitor's
      category (General, VIP, Press, etc. — managed under
      **Visitor profile types**).
   2. **Identity** — Name on badge first, then Date of birth, then full
      English + Arabic names, then Place of birth. (Order tuned for desk
      conversation flow.)
   3. **Nationality and ID** — toggle Saudi / Non-Saudi.
      - Saudi → 10-digit national ID starting with 1.
      - Non-Saudi → pick country, then toggle Iqama (10 digits starting
        with 2) or Passport (≤ 20 chars).
   4. **Contact** — Saudi mobile (`+9665XXXXXXXX`) or international mobile,
      optional email. Email is OK to leave blank — the QR badge is the
      access key.
   5. **ID document** — optional photo of national ID / Iqama / passport
      (PNG/JPEG/WebP, ≤ 5 MB). Stored encrypted at rest (D-129).
   6. **Interests** — pick up to 10 topics the visitor cares about (drives
      the visitor's profile picker).
3. Click **Register**.
4. The success modal pops with the freshly minted badge: profile-type colour
   stripe, name, QR code, QR id. Click **Print badge** to send to the
   printer; click **Register another** to clear and continue.

##### View a visitor's full profile + ID image

Click the **ⓘ Details** icon on the row → modal shows every field including
the inline ID document image (decrypted on demand). Close when done.

##### Reprint a lost badge

Go to **People → Print badge** (`/admin/print-bag`) — see §4.3.

##### Bulk-delete + Duplicate + Import + Export

Same shape as Admins (§10.1).

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Invalid national ID" toast | Saudi ID doesn't start with 1 OR isn't 10 digits | Re-check from the physical ID; the regex is strict |
| "Invalid Iqama number" toast | Iqama doesn't start with 2 OR isn't 10 digits | Same |
| Walk-in succeeds but ID image not on Details | Upload failed silently (network blip) | Re-open Details after 5s; if still missing, the registration is still good — image upload is best-effort |
| Walk-in form shows wrong language | Browser language toggle | Use the `العربية` link in the header to flip |
| Visitor not found on Print badge desk | Maybe registered under wrong Kind (Other instead of Visitor) | Check `/admin/others` |

#### What you cannot do here

- **Edit a visitor's identity after walk-in** (Edit is a stub awaiting the
  User Management module).
- **Mass-register from XLSX while populating profile fields** — the import
  XLSX covers email + display name + profile-type, not the full profile.
  Use the walk-in form for profile-complete registrations.

#### Cross-references

- Page reference: [`docs/pages/cp/admin-visitors.md`](../pages/cp/admin-visitors.md)
- Print desk: §4.3 + [`admin-print-bag.md`](../pages/cp/admin-print-bag.md)
- Walk-in wizard component: `WalkInRegistrationForm.razor`
- Decisions: D-114, D-127, D-128, D-129, D-130, D-131.

### 10.6 Pending visitors — `/admin/visitors/pending`

> Page reference: [`docs/pages/cp/admin-visitors-pending.md`](../pages/cp/admin-visitors-pending.md)

#### What it's for

Queue of self-registered visitors waiting for your approval. Approval mints
the QR badge and unlocks event entry; rejection records a reason for audit.

#### Approve a visitor (review-before-approve, D-128)

1. **System → Pending visitors**.
2. Click **View** OR **Approve** on the row. Both open the same modal
   preloaded with the visitor's full profile (Name EN/AR, nationality,
   DOB, place of birth, identity type + number, mobile, interests count,
   ID-document image inline if uploaded).
3. Read carefully — this is the moment to catch fraud / typos / wrong
   profile-type.
4. If everything checks: click **Confirm and Approve**. The modal closes,
   row vanishes, toast confirms `Approved {email}`. Visitor can now sign
   in + their QR badge is live.
5. If something's wrong: click **Cancel** and either Edit (when available)
   or Reject with a reason.

#### Reject a visitor

1. Click **Reject** on the row.
2. Type a clear reason (10–500 chars) — the visitor reads this verbatim on
   `/account/rejected` and the audit log keeps it forever.
3. Click **Reject**. Toast confirms `Rejected {email}`.

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Approve button shows "Visitor not found" | Another admin already approved/rejected | Refresh the list |
| Reject Submit disabled | Reason < 10 or > 500 chars | Type more / less |
| View modal shows "no profile filled yet" | Visitor created account but didn't open the profile page | Reach out + ask them to complete `/account/profile` first |

#### What you cannot do here

- **Bulk-approve / bulk-reject** — the toolbar checkboxes render per D-132
  for consistency, but no bulk endpoint exists yet. One row at a time.
- **Edit the visitor's profile** — that's a User Management feature, not
  shipped.

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

> Page reference: [`docs/pages/cp/admin-profile-types-visitor.md`](../pages/cp/admin-profile-types-visitor.md)

#### What it's for

Visitor profile types are the **colour-coded tiles** that appear at the top
of the walk-in registration wizard on `/admin/visitors`. Each row has a
bilingual name, a PageColor (the tile + badge stripe), and an active flag.
Add a new type before run-time when a new attendee category is announced
(e.g. "Press", "VIP", "Speaker").

#### Most common tasks

1. **System → Visitor profile types** → **+ Add**.
2. Fill: Name (EN), Name (AR), PageColor (paired text + colour-picker swatch
   — pick from the picker or type `#rrggbb` / `var(--brand-blue)`).
3. Save. The new tile appears in the walk-in wizard on next page load.
4. Edit / Deactivate identically to the canonical CRUD pattern (§3.1).

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Tile color shows navy in walk-in despite saving "red" | PageColor isn't a valid `#rrggbb` | Use the picker; or paste a 6-digit hex |
| Delete fails with "Profile type in use" | Visitors are linked to it | Deactivate instead (soft); the existing visitors keep their link, new walk-ins won't see it |
| "Duplicate name" | Same EN name as an existing type (case-insensitive) | Use a slightly different name |

### 10.9 Other profile types — `/admin/profile-types/other`

> Page reference: [`docs/pages/cp/admin-profile-types-other.md`](../pages/cp/admin-profile-types-other.md)

Identical to §10.8 but for the Other-typed walk-in wizard at `/admin/others`.
Both pools are completely separate (a "Press" Visitor tile and a "Press" Other
tile are independent rows; that's intentional so the two walk-in flows can
diverge over time).

### 10.10 Reset user 2FA — `/admin/reset-2fa`

> Page reference: [`docs/pages/cp/admin-reset-2fa.md`](../pages/cp/admin-reset-2fa.md)

When a user loses both their authenticator and all 10 recovery codes:

1. **System → Reset user 2FA**.
2. Type the user's email substring → pick the match → click **Reset 2FA**.
3. Confirm in the modal.
4. The server wipes their authenticator + recovery codes + active sessions
   and emails them out-of-band. They re-pair on next sign-in.

You cannot self-reset here — use **My profile → Reset my 2FA** instead.

### 10.11 Logs viewer — `/admin/logs`

> Page reference: [`docs/pages/cp/admin-logs.md`](../pages/cp/admin-logs.md)

Read-only tail of every project's log files. Pick **Project** (Api,
ControlPanel, Website) → **File** (per day) → **Lines** (50/100/500/1000)
→ optionally tick **Auto-refresh** (5 s poll). The body shows the tail in
a monospaced `<pre>` block. **Download** streams the full file to disk.

Use this when:

- An admin reports an error → find the matching timestamp in the Api log.
- Investigating a sign-in failure → look for `401` / `Authentication is required`
  in the corresponding project log.
- A walk-in registration failed mysteriously → check Api log for
  `AdminWalkInRegistrationRequestValidator` errors.

---

---

## 12. Account-area surfaces

### 12.1 My profile — `/account/profile`

> Page reference: [`docs/pages/cp/account-profile.md`](../pages/cp/account-profile.md)

Reach via the **You** link in the top header.

| Card | What you can do |
|------|-----------------|
| Identity | Read your email (locked) + edit your display name |
| Avatar | Upload + crop a new avatar (PNG/JPEG/WebP, ≤ 2 MB) |
| Security | Reset your own 2FA (re-pair flow) + regenerate your 10 recovery codes |
| Sessions | See every active session + revoke any of them |

### 12.2 Notifications inbox — `/account/notifications`

> Page reference: [`docs/pages/cp/account-notifications.md`](../pages/cp/account-notifications.md)

#### What it's for

Your personal inbox. The header **🔔 bell** shows the unread count;
clicking it opens a small menu with the latest few + a **View all** link
that lands here.

#### Most common tasks

| Want to | Do |
|---------|----|
| See every notification | Just open the page |
| Read the full body of one | Click the ⓘ **Details** icon |
| Dismiss one | Click the 🗑 **Delete** icon |
| Dismiss several at once | Select the rows + **Delete** in the toolbar |
| Mark every unread as read (but keep them) | **Mark all as read** below the grid |

#### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Bell shows N unread but page is empty | Filter or pager hides them | Clear filters; check page 1 |
| Bulk-delete is slow | No bulk endpoint — fires N delete requests | Select fewer rows at once (≤ 25 = visible page) |
| Notification body is `??????` for one language | Translation missing on that row | Open Details to see the other-language variant |

### 12.3 TOTP pairing — `/account/totp-pairing`

> Page reference: [`docs/pages/cp/account-totp-pairing.md`](../pages/cp/account-totp-pairing.md)

You only land here at first sign-in OR after Reset-my-2FA. Walkthrough is
in §2.2 above — scan QR / manual-entry secret / 6-digit verify / save the
10 recovery codes.

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
