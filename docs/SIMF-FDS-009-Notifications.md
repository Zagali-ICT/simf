# Feature Design Specification — Notifications

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-009 |
| Title | Feature Design Specification — Notifications |
| Version | 1.1 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-SRS-001, SIMF-UCS-001, SIMF-DAT-001, SIMF-SAD-001, SIMF-RDR-001, SIMF-FDS-002, SIMF-FDS-005 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The notifications feature, build-ready. |
| 1.1 | 2026-05-21 | Engineering & Architecture Team | Architecture-review amendment (see Amendment A): asynchronous queued sending; the retry policy with backoff and circuit breaker (closes OI-3). |

---

## 1. Purpose

This is the build-ready specification for notifications — how SIMF reaches a
user across in-app messages, email, SMS and WhatsApp. Every other feature that
needs to tell a user something does it through this feature.

## 2. Scope

The feature covers:

- the single notification abstraction that the rest of the system uses,
- the four delivery channels — in-app, email, SMS, WhatsApp,
- the catalogue of notification types and the events that trigger them,
- the reminders,
- the channel-mix configuration,
- the in-app inbox.

It does **not** decide *when* a registration is approved or a booking is
confirmed — the owning features raise those events. This feature delivers them.
The WhatsApp, SMS and email providers are deferred (decision D7) and reached
through channel adapters.

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-901 four channels behind one abstraction | UC-19 Receive a notification |
| FR-902 registration, session, VIP, meeting notifications | UC-19 |
| FR-903 booking and attendance reminders | UC-19 |
| FR-904 the channel mix is configuration | (architecture) |

## 4. Feature overview

```
Any feature raises an event
        │
        ▼
  Notification service ── builds a Notification
        │
        ├─▶ In-app  (SignalR)
        ├─▶ Email
        ├─▶ SMS
        └─▶ WhatsApp
   channel mix per type = configuration
```

A feature does not know about channels; it raises an event. The notification
service turns the event into a `Notification` and sends it on the channels
configured for that type, recording a `NotificationDelivery` per channel.

## 5. Detailed behaviour

### 5.1 The notification abstraction

- The notification service exposes one operation to the rest of the system:
  *notify this recipient about this event type, with this content*.
- A calling feature — registration, bookings, engagement, networking — uses
  only that operation. It does not choose channels and does not format channel
  messages. This is the single abstraction from SIMF-SAD-001 section 9.1.

### 5.2 The four channels

Each channel is an adapter behind the abstraction:

| Channel | Delivery | Notes |
|---------|----------|-------|
| In-app | A `Notification` pushed to the device over SignalR; it lands in the in-app inbox | Always available |
| Email | An email through the email gateway | Also carries the verification and reset codes (SIMF-FDS-001) |
| SMS | A short message through the SMS gateway | For critical, time-sensitive alerts |
| WhatsApp | A message through the WhatsApp Business provider | Provider deferred — decision D7 |

A channel adapter records the outcome of each send as a `NotificationDelivery`
with a status, so a failed send is visible and can be retried.

### 5.3 Notification types and triggers

The notification types, the feature that raises each, and the typical channels:

| Type | Raised by | Typical channels |
|------|-----------|------------------|
| Email verification code | Authentication (FDS-001) | Email |
| Password reset code | Authentication (FDS-001) | Email |
| Registration submitted | Registration (FDS-002) | In-app, Email |
| Registration approved / rejected | Registration (FDS-002) | In-app, Email |
| Badge ready | Registration (FDS-002) | In-app |
| VIP invitation | PR, via the Control Panel | In-app, Email |
| Session reminder | Forum Programme (FDS-004) | In-app |
| Booking confirmed | Bookings (FDS-005) | In-app, Email |
| Session started — you did not attend / enter | Bookings (FDS-005) | In-app |
| Meeting request / confirmation | Networking (FDS-008) | In-app, Email |
| Match recommendation (score ≥ 80%) | Networking (FDS-008) | In-app |
| Live session starting | Engagement (FDS-007) | In-app |

The channels shown are the default; the real channel set per type is
configuration (section 5.4).

### 5.4 Channel-mix configuration

- For each notification type, the **channels it is sent on** are a configuration
  setting, not code (FR-904). An organiser changes whether a type also goes by
  SMS or WhatsApp without a release.
- Adding a new channel in future is a new adapter plus configuration; no calling
  feature changes (SIMF-SAD-001 section 9.1).

### 5.5 The in-app inbox

- In-app notifications land in the attendee's inbox (mockup Screen 33): items
  grouped by read and unread, each with an icon, a title, a body and a
  timestamp, with filters such as All, Sessions and VIP.
- An unread count is shown on the home screen's bell; the inbox updates live
  over SignalR.
- Opening a notification marks it read and, where the notification points
  somewhere, deep-links to that target — a session, a meeting, the registration
  status.

### 5.6 Reminders

- **Session reminders** are sent before a session the attendee has saved or
  booked.
- **Attendance reminders** — "the session started and you did not attend" and
  "the session started and you did not enter" — are sent by the Bookings
  feature when a booked session starts with no matching hall-arrival record
  (SIMF-FDS-005 section 5.6).
- A device-local reminder set by the attendee on a session (SIMF-FDS-004
  section 6.3) is scheduled on the device and is separate from a server
  notification.

## 6. Data

The feature uses `Notification` and `NotificationDelivery` from SIMF-DAT-001
section 5.9. One `Notification` produces one `NotificationDelivery` per channel
it is sent on.

## 7. User interface

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 33 the notifications inbox; the bell and unread count on Screen 13 |
| Control Panel | The Notifications page — compose a notification and send it on the configured channels; the channel-mix configuration |

Mobile visuals are the external designer's; Control Panel screens follow
SIMF-CPD-001. Notification titles and bodies are localised, Arabic and English,
and are sent in the recipient's language; no string is hardcoded.

## 8. Validation rules

| Item | Rule |
|------|------|
| Notification type | One of the catalogue types |
| Recipient | An existing user |
| Channel mix | At least one channel per type; only configured channels are used |
| Content | A title and a body, in the recipient's language |
| Delivery record | One `NotificationDelivery` per channel, with a status |

## 9. Security considerations

- A notification is sent only to its intended recipient; an attendee sees only
  their own inbox.
- SMS, email and WhatsApp carry only what the recipient may receive; sensitive
  detail is not put in a channel that is not appropriate for it.
- The verification and reset codes (FDS-001) go by email only.
- Outbound sends and failures are recorded as `NotificationDelivery` rows;
  organiser-composed notifications are written to the operation log.
- The channel providers are reached through adapters; provider credentials are
  configuration secrets, not in the repository (SIMF-SES-001 section 4.4).

## 10. Acceptance criteria

1. A feature can raise a notification event without knowing about channels.
2. A notification is delivered on every channel configured for its type, and a
   `NotificationDelivery` records each send and its outcome.
3. In-app notifications reach the inbox live over SignalR; the unread count
   updates.
4. Opening a notification marks it read and deep-links to its target.
5. The channel mix for a type can be changed by configuration without a
   release.
6. The session and attendance reminders are sent correctly.
7. A notification is sent in the recipient's language.
8. A failed channel send is recorded and can be retried.
9. The inbox screens render in Arabic (RTL) and English (LTR); no hardcoded
   text.
10. The build is clean and the feature has unit, integration and end-to-end
    tests that pass.

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | A feature raises a notification event | `Notification` created; sent on the configured channels |
| T-02 | A type configured for in-app and email | a `NotificationDelivery` for each channel |
| T-03 | In-app notification arrives | it appears in the inbox live; unread count updates |
| T-04 | Open a notification with a target | marked read; deep-links to the target |
| T-05 | Change a type's channel mix in configuration | later sends follow the new mix; no release |
| T-06 | Booked session starts with no attendance | the attendance reminder is sent |
| T-07 | Session reminder before a saved session | the reminder is sent |
| T-08 | A channel send fails | the failure is recorded; the send can be retried |
| T-09 | Recipient language is Arabic vs English | the notification arrives in that language |
| T-10 | Render the inbox in Arabic and English | correct layout and direction; no hardcoded text |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the email, SMS and WhatsApp providers as decision D7 closes | Section 5.2 |
| OI-2 | Confirm the default channel mix per notification type with the client | Section 5.3 |
| OI-3 | Confirm the retry policy for a failed channel send | Sections 5.2, 9 |
| OI-4 | Confirm document classification with the owner | Control block |

---

## Amendment A — Architecture review (2026-05-21)

The scalability review of 2026-05-21 amends this feature.

**Asynchronous sending.** Notification sending is asynchronous: a feature raises
the event and persists the `Notification`; a **background worker drains the
channel sends**. A user-facing request (sign-up, booking) never blocks on an
email / SMS / WhatsApp round-trip — which makes the large fan-out (one
notification to tens of thousands of recipients) safe.

**Retry policy (closes OI-3).** A failed channel send is retried with bounded
backoff; each channel adapter has a connection and request timeout and a
circuit breaker, so a slow or unavailable gateway does not exhaust resources.

---

End of document.
