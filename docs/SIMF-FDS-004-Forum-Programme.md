# Feature Design Specification — Forum Programme

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-004 |
| Title | Feature Design Specification — Forum Programme |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-FDS-003, SIMF-SRS-001, SIMF-UCS-001, SIMF-API-001, SIMF-DAT-001, SIMF-RPM-001, SIMF-CON-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The forum programme feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for the forum programme — the themes,
the halls, the speakers and the sessions that make up the event, and the agenda
the attendee browses. It is the content the bookings, live broadcast,
engagement and statistics features all build on.

## 2. Scope

The feature covers:

- themes and pillars, with their sub-topics,
- halls and their seating,
- speakers, their profiles and their presentations,
- sessions — creating and managing them, linking a theme, a hall and speakers,
  and marking each live or non-live,
- the attendee agenda — browsing sessions by day, search, the session detail,
  add-to-calendar and reminders.

It does **not** cover seat booking (the Bookings & Attendance feature,
SIMF-FDS-005), the live video stream or session questions and comments (the
Engagement feature), or the AI session summary. This feature defines a session;
the others act on it.

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-401 themes and sub-topics | UC-23 Manage sessions |
| FR-402 sessions | UC-23 |
| FR-403 live / non-live sessions | UC-23 |
| FR-404 halls with seating capacity | UC-24 Manage halls and seating |
| FR-405 the seat grid | UC-24 |
| FR-406 speaker profiles | UC-25 Manage speakers |
| FR-407 speaker presentations | UC-25 |
| FR-408 the agenda, day filter, search | UC-08 Browse the agenda and a session |
| FR-409 add to calendar and reminders | UC-08 |

## 4. Feature overview

The programme is built bottom-up by the Scientific team in the Control Panel,
then read by attendees in the app:

```
Themes & pillars ─┐
Halls & seating  ─┼─▶ Sessions ─▶ Agenda (attendee app)
Speakers         ─┘
```

A session ties together a theme, a hall and one or more speakers, at a time.
The agenda is the attendee-facing view of all sessions.

## 5. Detailed behaviour — Control Panel

### 5.1 Themes and pillars

- A user holding the Themes & Pillars page (the Scientific team in the suggested
  configuration) creates and manages the **themes** — the five forum pillars —
  and their **sub-topics**.
- A theme has a title in Arabic and English and a display order. A sub-topic
  belongs to a theme and has a title in both languages.
- A theme is soft-deleted; a theme in use by a session is not removed while it
  is referenced.

### 5.2 Halls and seating

- A user holding the Halls & Seating page creates and manages **halls**.
- A hall has a name in Arabic and English and a **seating capacity**, expressed
  as a row count and a column count; the capacity is editable so it can grow or
  shrink between editions (SIMF-CON-001 section 7.3).
- Saving the capacity generates the hall's **seat grid** — one `Seat` per
  row-and-column position. Reducing the capacity removes only unallocated seats;
  a seat that is already assigned is not removed without warning.
- Each hall also carries a **geofence** used by the Badge & Access Control
  feature (SIMF-FDS-003 section 6); it is set here when the hall is created.

### 5.3 Speakers

- A user holding the Speakers page creates and manages **speaker profiles**.
- A speaker profile holds the name in Arabic and English, the rank, the bio,
  the academic qualifications, the training experience, the awards, a photo,
  and the speaker's country — the country drives the flag shown with the
  speaker.
- A speaker can have **presentation files** attached, each linked to the
  session it is presented in.

### 5.4 Sessions

- A user holding the Sessions page creates and manages **sessions**.
- A session has: a title and description in Arabic and English; a **theme**; a
  **hall**; a **session category** (a dynamic `Category`, for example a main
  session); a **start and end time**; and **one or more speakers**, each with a
  role in the session — speaker or host.
- A session is marked **live** or **non-live** (`BroadcastMode`). A live session
  is later streamed by the Engagement feature; a non-live session is not.
- A session's times must fall within the forum dates and the end must follow
  the start. Two sessions are not scheduled in the same hall at overlapping
  times.

## 6. Detailed behaviour — the attendee agenda

### 6.1 Browsing the agenda

- The agenda screen lists all sessions, with a **day selector** across the forum
  days and a **search** box (mockup Screen 16). Filtering and search narrow the
  list in place.
- The list shows each session's time, title and a short description; the active
  session is highlighted.

### 6.2 Session detail

- Tapping a session opens its detail (mockup Screen 17): the date and time, the
  title, the hall, the session category, the description, and the speaker
  cards.
- A speaker card opens the speaker's profile (mockup Screen 20) — the bio,
  qualifications, training experience and awards.
- The session detail also shows the attendee's assigned seat where they have a
  booking; the booking itself is the Bookings & Attendance feature.

### 6.3 Calendar and reminders

- From the session detail the attendee can **add the session to their device
  calendar** — a system action on the device.
- The attendee can **set a reminder**, which schedules a local notification on
  the device before the session starts.

## 7. Data

The feature uses these entities from SIMF-DAT-001 section 5.4: `Theme`,
`SubTopic`, `Hall`, `Seat`, `Session`, `Speaker`, `SessionSpeaker`,
`SpeakerPresentation`. It reads `Category` (the session category) and `Asset`
(speaker photos and presentation files). The `Hall` geofence addition is
SIMF-FDS-003 OI-2.

## 8. User interface

| Surface | Screens |
|---------|---------|
| Control Panel | Themes & Pillars, Halls & Seating, Speakers, and Sessions — each a list page with create/edit forms, per SIMF-CPD-001 section 13 |
| Mobile app | Screen 16 Agenda, Screen 17 Session detail, Screen 18 My Seat (the seat map; the booking is FDS-005), Screen 19 Speakers, Screen 20 Speaker profile, Screen 37 About the forum / pillars |
| Website | The public agenda, speakers and themes pages |

Control Panel screens follow SIMF-CPD-001; mobile visuals are the external
designer's. All content is held in Arabic and English; no string is hardcoded;
loading and error states are present on every data screen.

## 9. Validation rules

| Field | Rule |
|-------|------|
| Theme title (Ar / En) | Required in both languages |
| Sub-topic title (Ar / En) | Required in both languages; belongs to a theme |
| Hall name (Ar / En) | Required in both languages |
| Hall capacity | Required; row and column counts are positive integers |
| Speaker name (Ar / En) | Required in both languages |
| Speaker country | Required; drives the flag |
| Session title / description (Ar / En) | Required in both languages |
| Session theme, hall, category | Required; each an existing active record |
| Session start / end | Required; within the forum dates; end after start |
| Session hall booking | The hall is free for the session's time window |
| Session speakers | At least one speaker, each with a role |

## 10. Security considerations

- The Themes, Halls, Speakers and Sessions pages are permission-controlled; a
  user reaches them only with the right role (SIMF-RPM-001).
- The public agenda, speakers and themes content is open for reading on the
  website and to app guests; creating and changing it is authorised.
- Creating, editing and deactivating programme records is written to the
  operation log.

## 11. Acceptance criteria

1. The Scientific team can create and manage themes and their sub-topics in
   Arabic and English.
2. A hall can be created with a seating capacity; saving generates the seat
   grid; the capacity can be changed and the grid adjusts safely.
3. A speaker profile can be created with all its fields, a photo and a country.
4. A session can be created linking a theme, a hall, a category, a time window
   and speakers, and marked live or non-live.
5. A session cannot be saved with an end before its start, outside the forum
   dates, or in a hall already booked for that time.
6. The attendee agenda lists sessions, filters by day and searches in place.
7. The session detail shows the description, hall, category and speakers, and
   a speaker card opens the speaker profile.
8. An attendee can add a session to their device calendar and set a reminder.
9. All programme content shows correctly in Arabic (RTL) and English (LTR).
10. The build is clean and the feature has unit, integration and end-to-end
    tests that pass.

## 12. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Create a theme with sub-topics | theme and sub-topics saved in both languages |
| T-02 | Create a hall with a capacity | hall saved; seat grid generated |
| T-03 | Reduce a hall's capacity with seats assigned | unallocated seats removed; assigned seats kept with a warning |
| T-04 | Create a speaker profile | profile saved with photo and country |
| T-05 | Create a session linking theme, hall, category, speakers | session saved |
| T-06 | Save a session with end before start | rejected with a clear error |
| T-07 | Save a session in a hall already booked for that time | rejected as a hall clash |
| T-08 | Mark a session live, another non-live | `BroadcastMode` set correctly |
| T-09 | Browse the agenda and filter by day | list narrows to the chosen day |
| T-10 | Search the agenda | list narrows to the search term |
| T-11 | Open a session detail and a speaker profile | detail and profile shown correctly |
| T-12 | Add a session to the calendar and set a reminder | calendar entry created; local reminder scheduled |
| T-13 | Render the agenda and programme screens in Arabic and English | correct layout and direction; no hardcoded text |

## 13. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm whether the seat grid is a simple rows×columns block or supports irregular hall layouts | Section 5.2 |
| OI-2 | Confirm the session categories list with the client | Section 5.4 |
| OI-3 | Confirm whether speakers may belong to past editions as well (link to `EditionSpeaker`) | Section 5.3 |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
