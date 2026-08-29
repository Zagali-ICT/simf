# SIMF-REG-001 — Registration and Profile Rules

**Status:** controlled · **Owner:** SIMF programme owner · **Issued:** 2026-08-29

The binding rules for how a person becomes an attendee: which fields the app
demands, what makes a profile complete, when a badge exists, and the two desk
modes that change all of it.

---

## How to read this document

Every rule below states **where it is enforced** and **which test fails if it
breaks**. That is not decoration. Six documents in this repository were found on
2026-08-29 asserting things the code had stopped doing — including one that
described a fix the code had reversed ten days earlier — and the difference
between those and this one is that each rule here is pinned.

**If you change a rule, change its test in the same commit.** A rule whose test
still passes after you changed the behaviour was never really written down.

Two words are used precisely and are not interchangeable:

| Term | Means |
|---|---|
| **Required** | The save is REJECTED. The API answers 400 and nothing is written. |
| **Needed to complete** | The save SUCCEEDS. The profile is stored but reads incomplete, so it cannot be approved and no badge is issued. |

---

## 1. The app sign-up form

**Every field is required except the plate number.**

| Field | Rule | Enforced | Pinned by |
|---|---|---|---|
| Arabic name | required, ≥2 parts, Arabic letters only | app + server + DB | `UserProfileTests` |
| English name | required, ≥2 parts | app + server + DB | `UserProfileTests` |
| Gender | **required** — may not be Unspecified | app (defaults Male) + server | `POST_rejects_a_profile_with_no_gender_picked` |
| Nationality | required | app + server | `UserProfileTests` |
| National ID / Iqama / passport | required; Saudi → national ID, otherwise Iqama **or** passport | app + server | `UserProfileTests` |
| Organisation | required; picking "Other" then requires the free-text name | app + server | `POST_rejects_a_profile_missing_a_required_text_field` |
| Job title (English) | required, ≤100 | app + server | `POST_rejects_a_profile_missing_a_required_text_field` |
| Job title (Arabic) | required, ≤100 | app + server | `POST_rejects_a_profile_missing_a_required_text_field` |
| Mobile | required | app + server | `UserProfileTests` |
| Date of birth | required, 18 or older | app + server | `UserProfileTests` |
| Place of birth | required | app + server | `POST_rejects_a_profile_missing_a_required_text_field` |
| Interests | 1–10, saved on the second step | app + server | `UserProfileTests` |
| **Plate number** | **OPTIONAL** — see §2 | app + server | `POST_with_no_plate_stays_valid_and_GET_returns_null` |

**Gender defaults to Male** on an empty form. It is a required *answer*, not a
required *action*: the visitor may change it, but cannot submit with none. That
matters because §3's face-photo rule keys off it — before 2026-08-29 the server
accepted Unspecified, and a caller that never picked a gender skipped the photo.

## 2. The plate number

Optional. But a plate that **is** entered must carry **at least one letter AND
at least one digit** — 1–3 letters from the 17-letter set (one script, Latin or
Arabic) and 1–4 digits, in either order, never interleaved.

`ABJ` and `1234` are **rejected**. `A1` is the floor. Enforced in the app
(`plate_validation.dart`) and the server (`SaudiPlate`), which hold the identical
pattern and must not drift. Pinned by `SaudiPlateTests` — including two
deliberately inverted tests that assert the letters-only and digits-only forms
are refused, because those were accepted until 2026-08-29 and the tightening must
not be silently undone.

## 3. Photographs and the ID document

These two are stated together because they are enforced **differently on
purpose**, and reading them as one rule is the mistake this section exists to
prevent.

| | Who | Enforcement | Pinned by |
|---|---|---|---|
| **Face photo** | males only | **Required** — 400 `VISITOR_FACE_IMAGE_MISSING` | `POST_upsert_rejects_a_male_profile_without_a_face_photo` |
| **ID document image** | everyone | **Needed to complete** — the save succeeds | `POST_upsert_without_an_id_document_reads_incomplete` |

**Women do not supply a face photo.** They may; the code permits it. It is not a
prohibition. Pinned by `POST_upsert_accepts_a_female_profile_without_a_face_photo`.

**The face-photo rule applies to audience registrants**, not literally every male.
An operational profile type — a gate operator, a moderator — is exempt, because
holding a male operator to it would divert him to the visitor "complete your
profile" form on sign-in and he could never reach his own home screen.

**Why the ID image is not a hard reject.** The desk and bulk paths legitimately
have neither an ID image nor a face photo: a badge printed at an offline desk
carries a name and a number, and a bulk badge order carries no person at all.
Hard-rejecting the image server-side would break the very paths that exist to
register people quickly. So the app demands it, and the completeness flag stops
an incomplete profile being approved — which means **no badge**, which is the
outcome that actually matters.

## 4. Approval and the badge

**No approval, no QR** — on the self-service and interactive-desk paths.

A profile is submitted, an administrator approves it, and the QR is minted at
approval. Pinned by `Newly_created_staff_land_in_PendingApproval_with_no_QR`.

**Two paths are exceptions, by design:**

- **Auto-approve mode** (§6) approves an on-site *audience* visitor at the desk
  and mints the QR there. That is the mode's entire purpose.
- **Bulk badge orders and the offline desk** mint a badge id up front, because
  the badge is physically printed before anyone knows whose it is. The person is
  attached later.

Do not write "no approval, no QR" as an unqualified law. It is true where a human
submits a profile and waits, and false where a badge is manufactured first.

## 5. Profile type

**A self-registering visitor is Normal.** The app assigns it and hides the
picker; the server refuses any other audience self-pick. An administrator changes
it afterwards from the Control Panel, and the admin's choice wins.

Pinned by `POST_rejects_a_non_Normal_audience_profile_type_self_pick`.

**One caveat worth knowing:** the default is the *client's*. A request that omits
the profile type entirely persists NULL and waits for an administrator — the
server does not substitute Normal. That is deliberate (the server refuses to
guess someone's tier), but it means "the default is Normal" is a statement about
the app, not about the API.

## 6. The two desk modes

Both are turned on and off by an administrator at `/admin/walk-in-mode`, during
an event, with no deploy. Gated by `WalkInMode.View` / `WalkInMode.Manage` — its
own permission, because auto-approve relaxes an approval gate.

| Mode | Effect |
|---|---|
| **Quick register** | The desk captures a reduced field set: any one name, and one identity document. Nothing else is demanded — not organisation, not mobile, not profile type. |
| **No approval needed** | An on-site **audience** visitor is approved and given a badge at the desk. The partner/Other desk and app self-registration always queue. |

**The master switch is not a Control Panel setting.** Both modes resolve as
`IsArmed(now) && flag`, and `IsArmed` — walk-in mode enabled, inside its window —
stays in deployment configuration. An admin may turn a mode **off** mid-event but
cannot **arm** walk-in registration on an estate that never enabled it. That
still costs server access, which is a stronger control than any permission.

Ships **disarmed**: every switch defaults false.

**Precedence**, pinned in both directions by `WalkInModeSettingsTests`: an
explicit admin override wins over configuration; any other state — no row, blank,
unparseable — defers to configuration; nothing reaches past the master switch.

Both desk forms — the Control Panel walk-in form and the tablet
`/staff/register-visitor` screen — read the mode and ask only for the floor while
quick register is live. A failed read leaves them demanding the full set, which
is always safe to submit.

## 7. Bulk creation

An administrator generates a batch of badges from a small input in the Control
Panel. What is generated is **placeholder badge profiles, not visitors**: each
row carries no personal data, no email and no account. The real person's details
are filled in when the badge is assigned.

That is deliberate. An earlier design minted a dormant passwordless account per
badge, so a 1000-badge order created 1000 sign-in-capable identities that nobody
had vetted.

## 8. Where a person can be registered

| Surface | Route |
|---|---|
| The app, by the visitor | `POST /app/account/user-profile` |
| Control Panel walk-in desk | `POST /admin/visitors/register-onsite` |
| **Tablet**, by staff | `/staff/register-visitor` in the staff app → `POST /app/staff/visitors/register-onsite` |
| Offline badge desk, batched | the offline upload, per row |
| Bulk badge order | Control Panel, placeholder rows only |

Only the first two of these carry the §1 field rules. The desk paths carry the
quick floor when quick register is on, and their own reduced checks when it is
off — an identity number **can be captured by an admin but never corrected by
one**, because the edit forms carry no identity fields.

---

## Known gaps, stated rather than hidden

- **A desk-registered visitor can exist with no ID document at all**, permanently,
  and nothing complains. §3's "everyone" holds for people who register through
  the app. If that should change, it is a code change, not a documentation one.
- **The identity number cannot be corrected after capture.** No edit form carries
  the field.
- **There is no cross-profile uniqueness on an identity number** (D-945). The same
  passport may appear on two profiles; only `(ProfileId, Kind)` bounds one profile
  to one document per kind. Removed on owner instruction because it blocked
  registration outright and the desk had no way to release a number.

---

*Related:* `docs/decisions/DECISIONS_LOG.md` D-945 / D-946 / D-947 ·
`docs/tests/e2e/cp-walk-in-mode.md` · `docs/tests/e2e/cp-admin-visitors.md`
