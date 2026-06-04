# Feature Design Specification — Media, News and Archive

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-010 |
| Title | Feature Design Specification — Media, News and Archive |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-SRS-001, SIMF-UCS-001, SIMF-DAT-001, SIMF-RDR-001, SIMF-CON-001, SIMF-CPD-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The media, news and archive feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for the Media Center, the News, and the
archive of previous editions — the content the forum publishes about itself and
its history.

## 2. Scope

The feature covers:

- the Media Center — media coverage, social content and posts, the photo and
  video gallery, and the media partners,
- News items and their categories,
- the previous-editions archive and its post-event visibility control.

All of this content is created and managed in the Control Panel and presented,
read-only, on the website and the app. The section formerly called "Media
Coverage" is renamed **Media Center** (decision D6).

The exact field set of media and news items is **proposed here for the client
to review** (decision D6, open item OI-1); it is not treated as final until
confirmed.

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-1001 the Media Center | UC-32 Manage media, news and the archive |
| FR-1002 News and categories | UC-32 |
| FR-1003 the gallery and media partners | UC-32 |
| FR-1004 previous editions | UC-32 |
| FR-1005 archive visibility control | UC-32 |
| FR-1006 field sets confirmed per decision D6 | (open item OI-1) |

## 4. Feature overview

```
Marketing team (Control Panel)
        │  creates and manages
        ▼
  Media Center · News · Previous editions
        │  presented read-only
        ▼
   Website · Mobile app
```

## 5. Detailed behaviour

### 5.1 The Media Center

- A user holding the Media Center page — the Marketing team in the suggested
  configuration (SIMF-RPM-001) — manages the Media Center content.
- The Media Center holds **media items**, each of a kind: a post, a photo, a
  video, or a social-media post. A media item has a title and a body in Arabic
  and English, the media asset, and a publish date.
- It also holds the **media partners** — a directory of partner organisations,
  each with a name and a logo.
- The attendee sees the Media Center as the photo and video gallery, the social
  content and posts, and the media partners (mockup Screens 29–31).
- The proposed field set is in section 9; the client confirms it (OI-1).

### 5.2 News

- A user holding the News page manages **news items**. A news item has a title
  and a body in Arabic and English, an image, a publish date, and a
  **category**.
- The news categories are a dynamic `Category`; the categories seen in the
  source material are coverage, announcement, opening and cooperation — the
  client confirms the set (OI-1).
- The attendee sees the news as a stream, each item with its title, image, date
  and category, and can open an item to read it.

### 5.3 Previous editions — the archive

- A user holding the Previous Editions page manages the archive. For each past
  edition the archive holds: the forum title, a brief, the sessions, the place,
  the time, an image, a video, the previous speakers, and the edition's
  statistics (FR-1004, SIMF-CON-001 section 7.9).
- The attendee sees the archive as one card per year, with the edition's stats
  and a link into its gallery (mockup Screen 24).
- **Visibility control.** The current edition (SIMF 2026) does not appear in the
  archive until the event has ended; its visibility is controlled by the
  `IsVisible` flag on the edition, set from the Control Panel (FR-1005). Past
  editions are visible.

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.8: `MediaItem`,
`MediaPartner`, `NewsItem`, `Edition`, `EditionStat`, `EditionSpeaker`. It reads
`Category` (the news categories) and `Asset` (images, video, logos).

## 7. User interface

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 29 News, Screen 30 the photo and video gallery, Screen 31 media partners, Screen 24 the archive of previous editions |
| Control Panel | Media Center, News and Previous Editions — list pages with create/edit, per SIMF-CPD-001 |
| Website | The public news, media and previous-editions pages |

Control Panel screens follow SIMF-CPD-001; mobile visuals are the external
designer's. All content is held in Arabic and English; loading and error states
are present; no string is hardcoded.

## 8. Validation rules

| Field | Rule |
|-------|------|
| Media item kind | Required; post, photo, video or social post |
| Media item title / body (Ar / En) | Required in both languages |
| Media item asset | Required; matches the item kind |
| News item title / body (Ar / En) | Required in both languages |
| News item category | Required; an active news category |
| News item image | Required |
| Edition year | Required; unique |
| Edition fields | Title, brief, place, time required |
| Edition visibility | The current edition is hidden until the event ends |

## 9. Proposed field sets — for client review (OI-1)

The fields below are proposed; decision D6 leaves them to the client to
confirm.

- **Media item:** kind, title (Ar/En), body (Ar/En), media asset, source link
  (for a social post), publish date.
- **News item:** title (Ar/En), body (Ar/En), image, category, publish date.
- **Previous-edition statistics:** the figures listed in SIMF-CON-001 section
  7.10 — events, attendees, speakers, and the rest.

## 10. Security considerations

- The Media Center, News and Previous Editions pages are permission-controlled.
- Published media, news and archive content is public to read on the website
  and to app guests; creating and changing it is authorised.
- Creating, editing, deactivating and changing the visibility of content is
  written to the operation log.
- Social content brought in from social platforms is reviewed before it is
  published, so nothing inappropriate appears unchecked.

## 11. Acceptance criteria

1. The Marketing team can manage media items of each kind, and the media
   partners, in Arabic and English.
2. The Marketing team can manage news items with a category and an image.
3. The Marketing team can manage previous editions with their stats, speakers,
   media and details.
4. The current edition does not appear in the archive until its visibility is
   turned on after the event.
5. Past editions are visible to attendees and on the website.
6. The attendee sees the gallery, news stream, media partners and the archive
   correctly.
7. All content shows correctly in Arabic (RTL) and English (LTR).
8. The build is clean and the feature has unit, integration and end-to-end
   tests that pass.

## 12. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Create media items of each kind | post, photo, video, social post saved |
| T-02 | Manage the media partners | partners saved with name and logo |
| T-03 | Create a news item with a category and image | news item saved and published |
| T-04 | Browse the news stream and open an item | items listed; an item opens to read |
| T-05 | Manage a previous edition with stats and speakers | edition saved with all its content |
| T-06 | Current edition before the event ends | not shown in the archive |
| T-07 | Turn on the current edition's visibility after the event | it appears in the archive |
| T-08 | View the gallery, partners and archive in the app | content shown correctly |
| T-09 | Render the media, news and archive screens in Arabic and English | correct layout and direction; no hardcoded text |

## 13. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Client confirmation of the media-item and news-item field sets and the news categories (decision D6) | Section 9 |
| OI-2 | Confirm whether social content is pulled from social platforms automatically or entered by hand | Section 5.1 |
| OI-3 | Confirm how the previous-edition sessions and speakers relate to the live programme entities | Section 5.3 |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
