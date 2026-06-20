# Feature Design Specification — Exhibition

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-006 |
| Title | Feature Design Specification — Exhibition |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-FDS-002, SIMF-FDS-004, SIMF-FDS-005, SIMF-SRS-001, SIMF-UCS-001, SIMF-DAT-001, SIMF-RPM-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The exhibition feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for the exhibition — the booths, the
sponsors, and the interactive venue map. It is how an attendee finds an
exhibitor, learns about a sponsor, and navigates the venue.

## 2. Scope

The feature covers:

- the booth directory — booth records and their detail,
- sponsors and their tiers,
- the interactive 3D venue map of halls, zones and booths,
- in-venue navigation to a booth or to an attendee's assigned seat,
- the attendee-facing browsing of all of the above.

It does **not** cover exhibitor registration or the approval that assigns a
booth — that is the Registration & Approval feature (SIMF-FDS-002 section 6.2).
This feature manages the booth record once it exists, and presents it. Halls
and seats come from the Forum Programme feature (SIMF-FDS-004); the assigned
seat comes from Bookings (SIMF-FDS-005).

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-603 the booth directory | UC-18 Browse exhibitors, booths and the venue map |
| FR-604 sponsors and their tiers | UC-18 |
| FR-605 the interactive 3D venue map and navigation | UC-18 |

Delegations are not part of this feature; the original module was removed from
scope (SIMF-CON-001 section 14) and deleted (D-277). It was re-introduced
2026-06-20 as a light additive flag on visitors/countries (D-473, req #10) —
managed from the `/admin/delegates` desk, not here.

## 4. Feature overview

```
Exhibitor approved + booth assigned (FDS-002)
        │
        ▼
   Booth record  ──┐
   Sponsors       ─┼─▶ Attendee browses · Venue map · Navigate
   Halls (FDS-004)─┘
```

## 5. Detailed behaviour

### 5.1 Booths

- A booth record exists once an exhibitor is approved and a booth is assigned
  (SIMF-FDS-002). The booth holds its hall, its booth number, the exhibitor's
  logo, an Arabic and English descriptor, and a booth contact — a name, a phone
  number and an email.
- A user holding the Booths page manages the booth records — the descriptor,
  the contact details, the logo. Logistics views the booths; PR manages them in
  the suggested configuration (SIMF-RPM-001 Appendix A).
- The attendee sees a **booth directory** (mockup Screen 22): a searchable list
  of booth cards, each with the hall and booth number, the logo, the name and
  descriptor, the contact, and a **directions** action.
- The directions action opens the venue map and routes the attendee to that
  booth (section 5.3).
- The phone and email on a booth card open the device dialler and mail
  composer.

### 5.2 Sponsors

- A user holding the Sponsors page manages **sponsors**. A sponsor has a name
  and a description in Arabic and English, a logo, and a **tier** — Strategic,
  Premium or Gold.
- The attendee sees the sponsors grouped by tier (mockup Screen 23): the
  strategic sponsor most prominent, then premium, then gold.

### 5.3 The venue map

- The venue map is an **interactive 3D (isometric) map** of the halls, the
  exhibition zones and the booths (mockup Screen 15).
- Each mappable thing — a hall, a zone, a booth — is a `VenueMapNode` with a
  position. A user holding the Venue Map page (Logistics in the suggested
  configuration) places and edits the nodes.
- The attendee can pan and zoom the map, tap a booth for a preview popup (logo,
  hall, booth number, a directions action and a view-details action), and see
  their own position.
- **Navigation:** the directions action starts in-venue guidance to the chosen
  booth. The map can also guide the attendee to their **assigned seat** for a
  booked session (SIMF-FDS-005), and a hall on the map links to its sessions.

### 5.4 Attendee browsing

The attendee reaches booths, sponsors and the map from the home dashboard and
the bottom-navigation Map slot. The browsing is read-only; the content is
managed in the Control Panel.

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.5: `Booth`,
`Sponsor`, `VenueMapNode`. It reads `ExhibitorProfile`, `Hall`, `Seat` and
`Asset` (logos).

## 7. User interface

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 15 the 3D venue map, Screen 22 the booth directory, Screen 23 the sponsors |
| Control Panel | Booths, Sponsors and Venue Map — list pages with create/edit, per SIMF-CPD-001 |
| Website | The public exhibitors, booths and sponsors pages |

Control Panel screens follow SIMF-CPD-001; mobile visuals are the external
designer's. Booth, sponsor and map content is held in Arabic and English; no
string is hardcoded; loading and error states are present.

## 8. Validation rules

| Field | Rule |
|-------|------|
| Booth number | Required; unique within its hall |
| Booth hall | Required; an existing hall |
| Booth contact | A name, a phone and an email |
| Booth descriptor (Ar / En) | Required in both languages |
| Sponsor name / description (Ar / En) | Required in both languages |
| Sponsor tier | Required; Strategic, Premium or Gold |
| Venue map node | A position, and a reference to the hall, zone or booth it marks |

## 9. Security considerations

- The Booths, Sponsors and Venue Map pages are permission-controlled.
- Booth and sponsor content is public to read on the website and to app guests;
  creating and changing it is authorised.
- Creating, editing and deactivating booths, sponsors and map nodes is written
  to the operation log.
- The attendee's position used for navigation is handled under the same
  location-privacy rules as SIMF-FDS-003 section 9.

## 10. Acceptance criteria

1. A booth record carries its hall, number, logo, descriptor and contact, and
   can be managed from the Control Panel.
2. The attendee sees a searchable booth directory; a directions action opens
   the map routed to that booth; phone and email open the device apps.
3. Sponsors can be managed with a tier; the attendee sees them grouped
   Strategic, Premium, Gold.
4. The venue map shows halls, zones and booths in 3D; the attendee can pan,
   zoom and tap a booth for a preview.
5. The map navigates the attendee to a booth and to an assigned seat.
6. A booth number is unique within its hall.
7. All content shows correctly in Arabic (RTL) and English (LTR).
8. The build is clean and the feature has unit, integration and end-to-end
   tests that pass.

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Manage a booth record in the Control Panel | descriptor, contact and logo saved |
| T-02 | Save a duplicate booth number in one hall | rejected as not unique |
| T-03 | Browse and search the booth directory | list narrows to the search term |
| T-04 | Tap a booth's directions action | the venue map opens routed to the booth |
| T-05 | Tap a booth's phone and email | the device dialler and mail composer open |
| T-06 | Manage sponsors across the three tiers | sponsors saved with their tier |
| T-07 | View the sponsors screen | sponsors grouped Strategic, Premium, Gold |
| T-08 | Pan, zoom and tap a booth on the venue map | preview popup with logo, hall, number, actions |
| T-09 | Navigate to an assigned seat from the map | route shown to the seat |
| T-10 | Render the exhibition screens in Arabic and English | correct layout and direction; no hardcoded text |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the venue-map source — a supplied 3D asset, or built from the hall/zone layout — and the map/location service (SIMF-SAD-001 OI-7) | Section 5.3 |
| OI-2 | Confirm whether a booth may host more than one exhibitor, or is one-to-one | Section 5.1 |
| OI-3 | Confirm the sponsor ordering within a tier | Section 5.2 |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
