# Feature Design Specification — Control Panel Configuration

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-012 |
| Title | Feature Design Specification — Control Panel Configuration |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-SRS-001, SIMF-UCS-001, SIMF-DAT-001, SIMF-RPM-001, SIMF-RDR-001, SIMF-VID-001, SIMF-CON-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The Control Panel configuration feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for Control Panel configuration — the
feature behind the requirement that SIMF is "everything dynamic". It lets the
organisers change content, categories, labels, colours and key settings without
a release, and it records every change.

## 2. Scope

The feature covers:

- dynamic content blocks — titles, texts, the in-app welcome message, banners,
  images, section labels, page content,
- dynamic categories, labels and their colours,
- venue tracks,
- registration open and close,
- the system configuration settings,
- the operation log.

It does **not** cover role and permission management — that is the Roles &
Permissions page, specified in SIMF-RPM-001 sections 8 and 12. It does not set
the brand palette and typography — those are fixed by SIMF-VID-001; this feature
manages content and per-category colours, not the brand identity itself.

Per decision D11, configuration is split across two Control Panel pages:
**Content & Categories** (the content team) and **System Configuration** (the
Technical team).

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-1203 dynamic content management | UC-28 Manage dynamic content and categories |
| FR-1204 dynamic categories, labels, colours | UC-28 |
| FR-216 registration open and close | UC-31 Open or close registration |
| FR-1205 the operation log | (all configuration changes) |
| FR-1206 the visual identity is applied | (brand — SIMF-VID-001) |
| FR-206 / D2 venue tracks | UC-03 (used at registration) |

## 4. Feature overview

```
Content & Categories page ─▶ content blocks · categories · labels · colours · venue tracks
System Configuration page ─▶ registration open/close · system settings
Operation Log page        ─▶ the history of every change
```

A change made here takes effect across the website and the app at runtime, with
no release.

## 5. Detailed behaviour

### 5.1 Dynamic content blocks

- A user holding the Content & Categories page manages **content blocks** — each
  a keyed piece of editable content: a heading, a body text, the in-app welcome
  message, a banner, an image, a section label, page content.
- A content block holds its value in Arabic and English; the website and the
  app read the block at runtime.
- Logos and images in a content block are stored as `Asset` records.

### 5.2 Dynamic categories, labels and colours

- The same page manages the **categories** — the dynamic lists the system uses:
  registration types, Visitor sub-types, the "Other" types, session categories,
  interests, news categories, and others.
- For each category the user can **add**, **hide** or **delete** an entry, set
  its name in Arabic and English, set its **display order**, and set its
  **colour**.
- Every category is one row in the `Category` table, tagged with its `Kind`
  (SIMF-DAT-001 section 5.12). The features that use a category read it at
  runtime, so a new entry appears without a release.

### 5.3 Venue tracks

- The page manages the **venue tracks / zones** — the "direction / track" a user
  picks after registration (decision D2). Each track has a name in Arabic and
  English.

### 5.4 Registration open and close

- A user holding the System Configuration page can **open and close
  registration** (FR-216).
- Registration **closes automatically** at the end of the last forum day.
- The open/closed state is the `RegistrationControl` record; the registration
  feature (SIMF-FDS-002) reads it.

### 5.5 System configuration

- The System Configuration page also holds the system and platform settings —
  the settings that are not content and not categories. Their full list is
  finalised with the low-level design; they are kept apart from the content
  configuration so the two teams do not overlap (decision D11).

### 5.6 The operation log

- Every change made through the Control Panel — a content edit, a category
  change, a registration open/close, an approval, a role change — is written to
  the **operation log**: who did it, what changed, and when (FR-1205).
- A user holding the Operation Log page can view the log. The log is
  append-only from the application's point of view; an entry is never edited or
  removed.

### 5.7 The visual identity

- The website and the app apply the visual identity in SIMF-VID-001 — the
  palette, the typography, the logo and the pattern.
- This feature manages **content and per-category colours**; it does not change
  the brand palette or typeface, which are fixed by SIMF-VID-001 and implemented
  in the theme tokens (SIMF-CPD-001).

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.12: `ContentBlock`,
`Category`, `VenueTrack`, `RegistrationControl`, `OperationLog`, `Asset`. The
operation log is written by every feature; this feature owns the log and its
viewer.

## 7. User interface

| Surface | Screens |
|---------|---------|
| Control Panel | Content & Categories, System Configuration, and Operation Log — per SIMF-CPD-001 |
| Website / Mobile app | No screen of their own; they read the content blocks, categories and tracks at runtime |

Control Panel screens follow SIMF-CPD-001; content is held in Arabic and
English; loading and error states are present; no string is hardcoded.

## 8. Validation rules

| Item | Rule |
|------|------|
| Content block | A key; a value in Arabic and English |
| Category entry | A name in Arabic and English; a colour; a display order; a `Kind` |
| Category in use | A category entry in use is hidden rather than hard-deleted |
| Venue track | A name in Arabic and English |
| Registration control | A single open/closed state; an automatic close at the end of the last forum day |
| Operation log | Append-only; an entry records the actor, the action, the target and the time |

## 9. Security considerations

- The Content & Categories, System Configuration and Operation Log pages are
  permission-controlled (SIMF-RPM-001); a user reaches each only with the right
  role.
- Every configuration change is itself written to the operation log, so the
  configuration has a full history.
- The operation log is append-only; no user, including an Administrator, edits
  or deletes a log entry through the application.
- A content block that carries an uploaded image restricts the file to expected
  types and a size limit.

## 10. Acceptance criteria

1. A user can edit content blocks — headings, texts, the welcome message,
   banners, images, labels — and the change shows on the website and the app
   without a release.
2. A user can add, hide and delete category entries, and set each entry's name,
   colour and order; a category in use is hidden, not hard-deleted.
3. A user can manage the venue tracks.
4. A user can open and close registration; registration closes automatically at
   the end of the last forum day.
5. The system configuration settings are managed on their own page, apart from
   the content configuration.
6. Every Control Panel change is written to the operation log, which can be
   viewed and is append-only.
7. The website and the app apply the SIMF-VID-001 visual identity.
8. All configuration content is held in Arabic and English.
9. The build is clean and the feature has unit, integration and end-to-end
   tests that pass.

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Edit a content block | the new value shows on the website and the app without a release |
| T-02 | Add a category entry with a colour | the entry appears wherever that category is used |
| T-03 | Hide a category entry in use | the entry is hidden, not removed; existing references hold |
| T-04 | Add and edit a venue track | the track is offered at registration |
| T-05 | Close registration | the registration form is no longer offered |
| T-06 | End of the last forum day | registration closes automatically |
| T-07 | Make several configuration changes | each is written to the operation log |
| T-08 | View the operation log | changes shown with actor, action, target and time |
| T-09 | Attempt to edit a log entry | not possible; the log is append-only |
| T-10 | A non-permitted user opens a configuration page | access is refused |
| T-11 | Render the configuration screens in Arabic and English | correct layout and direction; no hardcoded text |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the full list of content-block keys with the client | Section 5.1 |
| OI-2 | Confirm the full list of system-configuration settings in the low-level design | Section 5.5 |
| OI-3 | Confirm the operation-log retention period with the owner | Section 5.6 |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
