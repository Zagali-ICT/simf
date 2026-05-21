# SIMF — System Concept Summary

> **Project:** SIMF — Saudi International Maritime Forum (الملتقى البحري السعودي الدولي)
> **Edition:** 4th edition — SIMF 2026
> **System version:** V1.0.0
> **Authoritative baseline:** **2026-05-20 client meeting** (see §2)
> **Document status:** Approved 2026-05-21 — part of the V1.0.0 documentation baseline
> **Updated:** 2026-05-20

This document is the consolidated concept reference for the SIMF system. Its
**authoritative baseline is the client meeting of 2026-05-20**; where the earlier
document set (dated 15-04-2024) conflicts with that meeting, the meeting wins.
This document does **not** contain architecture or code.

---

## 1. Document Control

| Item | Detail |
|------|--------|
| System owner | Ministry of Defense (وزارة الدفاع) / Royal Saudi Naval Forces — RSNF |
| Event | Saudi International Maritime Forum 2026 (Riyadh) |
| Event dates | **23–25 November 2026** (confirmed 2026-05-20) |
| Implementation vendor | STARTIME |
| Surfaces | Public Website, Mobile App (Flutter), Control Panel (Admin) |
| Primary languages | Arabic (primary, RTL) + English (LTR) |
| Nature of source visuals | Functional wireframe / structural mockup — **not** final UI |

---

## 2. Baseline & Change Log

### 2.1 Authoritative baseline
The **2026-05-20 client meeting** is the current authoritative requirements
baseline. The full meeting intake is preserved verbatim in **Appendix A**.

### 2.2 Items superseded by the 2026-05-20 meeting
The following items from the 15-04-2024 document set are **overridden**:

| # | Earlier document | Superseded by the meeting |
|---|------------------|---------------------------|
| 1 | DB = SQL Server 2016+ | **SQL Server 2022** |
| 2 | Login = email + Face ID + Nafath + OTP | **Email + password + email code only** — no Nafath, no Face ID |
| 3 | Phone number validated | **No phone-number validation** |
| 4 | User types: Visitor / Media / Sponsor / Speaker / VIP | **Restructured** — see §6 (Staff added; Moderator is an app user) |
| 5 | Delegations (الوفود) — full feature + Screen 21 | **Removed** |
| 6 | Cybersecurity page (Screen 39) | **Removed**; replaced by Policies + Terms & Conditions |
| 7 | Audience-comments / interview-request screens (26, 27) | **Removed as screens**; question & comment remain as features |
| 8 | AI provider = Google Gemini | **"Cognitive AI", CP-managed, 2 setting levels; provider not yet approved** |
| 9 | Event dates ambiguous (23–25 vs 27–29 Nov) | **23–25 November 2026** |

### 2.3 Confirmed unchanged
Three surfaces; dynamic content/categories/roles; Blazor + .NET + Flutter;
core registration fields; email verification code; security vetting & approval;
AI session summary; live / non-live broadcast; QR badge & access; 3D venue map;
booths/exhibitor directory; sponsor tiers; one-to-one meetings; "meet people
like you"; interests — **all remain in scope** (confirmed 2026-05-20).

---

## 3. Executive Summary

SIMF is an **integrated exhibition / forum management system** for a national
maritime defense forum organised by the Royal Saudi Naval Forces. It serves the
full event lifecycle: pre-event **registration and approval**, on-site **badge
and access control**, in-event **sessions, live broadcast, networking and
engagement**, and post-event **archive, statistics and feedback**.

Delivered as **three coordinated surfaces over one backend**:

1. **Website** — public marketing and registration site (Blazor, AR/EN).
2. **Mobile App** — attendee experience, 41 screens (Flutter, Android + iOS).
3. **Control Panel** — admin/operations console for the organising teams (Blazor).

Two themes run through every requirement:

- **Everything is dynamic** — content, categories, labels, colours, pages, roles
  and permissions are editable from the Control Panel **without code changes**.
- **Permission-driven** — the Control Panel and both apps are governed by roles
  and permissions; each user type has its own screens, permissions and use cases.

The event date is a **hard, immovable deadline**; the plan targets the system
being operational about two months before the forum.

---

## 4. Project Identity

| Aspect | Detail |
|--------|--------|
| What it is | An integrated maritime-forum / exhibition management system |
| Who owns it | Ministry of Defense / RSNF |
| Who uses it | Forum attendees, exhibitors, moderators, staff, and the organising admins |
| Why it exists | Run the forum end-to-end: registration & approval, access control, sessions & engagement, media, and analytics |
| Related systems | SIMMOD, RSNF NEXUS, RSNF IMEX; candidate domains `simf-rsnf` |
| Out of scope | Physical entry/exit gate hardware and ERP integration |

---

## 5. Solution Surfaces

### 5.1 Website (Public)
- Marketing/information site: home with image slider, countdown timer, location,
  links to previous editions; themes, speakers, exhibitors, floor plan, sessions,
  sponsors, media coverage, FAQ.
- **Registration is performed from the website** (and the app).
- Bilingual AR/EN, RTL/LTR. The website is **not** permission-gated for public
  browsing; the Control Panel is fully permission-gated.

### 5.2 Mobile App (Flutter — Android + iOS)
- 41 screens, RTL Arabic-primary (see §8).
- App user types — Guest, Visitor, Exhibitor, Moderator, Staff — **each with its
  own screens, permissions and use cases**.
- Five-slot bottom navigation: Home · Agenda · Badge (centre FAB) · Map ·
  Media Coverage *(this last label is to be renamed — §7.8)*.

### 5.3 Control Panel (Admin)
- Permission-driven operations console for the organising teams (§7.11).

---

## 6. Actors & Roles

The system uses **two distinct user models**.

### 6.1 General-system user types (Website + Control Panel)
| Type | Notes |
|------|-------|
| Admin (مشرف) | Holds Control Panel permissions; manages the system |
| Visitor (زائر) | Sub-types: VIP, Normal (عادي), and more later |
| Exhibitor (عارض) | Exhibiting organisation |
| Staff (اسطاف) | Organising staff — workflow to be detailed |
| Other (آخر) | Catch-all type — to be defined |

### 6.2 Mobile-app user types
Guest (ضيف) · Visitor (زائر) · Exhibitor (عارض) · Moderator (محاور) · Staff.

> Each type has **its own screens, permissions and use cases**. The detailed
> per-type screen and permission maps are deferred (§15).

### 6.3 Control Panel teams (permission sets)
Admins manage the system through the Control Panel. Internal teams seen in the
source set: Security Team, Moderators, PR / Public Relations, Technical Team,
Organising/Admin, Scientific team, Logistics/Supply, Marketing & PR.

> Roles & permissions are **dynamic** — new roles can be added and permissions
> edited from the Control Panel. The final, authoritative per-type permission
> matrix is deferred (§15).

---

## 7. Functional Scope

### 7.1 Accounts, Registration & Approval
**App sign-up flow:**
1. Enter email → password → confirm password.
2. System sends a **verification code to the email**; user enters the code.
3. After verification, the user completes the remaining data.
4. **Choose type:** Visitor or Other.
5. **Personal data:** 4-part Arabic name; English name per passport; nationality;
   date of birth; place of birth.
6. **Identity:** Saudis → national ID number; non-Saudis → choose document type
   (**passport number** or **Iqama number**).
7. **Contact:** mobile inside KSA; mobile outside KSA (overseas visitors).
   **No phone-number validation.**
8. **Attachments:** ID image now; other attachments later as needed.
9. After registration, the user picks a **"direction / track" (التوجه/المسار)**
   *(meaning deferred — §15)*, receives a message containing contact details,
   and the request status becomes **"waiting for approval."**
10. A user may keep using the app **as a Guest without registering.**

**Login:** email + password only. *(No Nafath, no Face ID.)*

**Approval (Control Panel):** the request appears in the Control Panel → an admin
reviews it → approves → assigns the appropriate permissions → sets the **final
user type**.

**Registration control:** open/close registration, auto-close at the end of the
last forum day, manual toggle — from the Control Panel.

### 7.2 Badge & Access Control
- Entry **badge with a personal QR code**, colour-coded per category.
- **QR / barcode verification** at venue entry.
- **Attendee-to-attendee QR scan** to exchange and save contacts.
- On-site instant registration / badge reprint for non-badge holders.

### 7.3 Forum Content
| Concept | Description |
|---------|-------------|
| Forum / Edition | The forum and its editions; previous editions archived (§7.9) |
| Themes / Pillars (المحاور) | Five named pillars: (1) changes in the global strategic environment & maritime supply-chain security; (2) threats to energy supply chains & the global economy; (3) seabed protection & international security; (4) maritime-transport cybersecurity — challenges & solutions; (5) AI & modern technologies in seabed & supply-chain security. Each has sub-topics |
| Sessions (الجلسات) | Time, hall, category, description, speakers; per-day schedule; reminders; add-to-calendar. May be **live or non-live** |
| Halls / Rooms (القاعات) | Created with a defined **seating capacity**, editable from the Control Panel |
| Seats & assignment | Seat grid per hall; assigned seat; "My Seat" map with guidance |
| Speakers (المتحدثون) | Profile: bio, qualifications, training experience, awards; rank; **photo**; **country flag**; linked to sessions and presentations |
| Booths / Exhibitors (الأجنحة) | Booth directory: hall + booth number, logo, descriptor, contact, phone, email, map directions |
| Sponsors (الرعاة) | Tiered: Strategic, Premium, Gold |
| ~~Delegations (الوفود)~~ | **Removed** (2026-05-20) |

### 7.4 Venue Map & Navigation
- Interactive **3D isometric map** of halls, exhibition zones and booths.
- Booth markers, user position, zoom; booth preview with **directions**.
- In-app navigation to a booth or to the attendee's assigned seat.

### 7.5 Live Broadcast & Session Engagement
- **Live broadcast** of sessions, with AI translation / live captions and a
  language picker; **geographic restriction** (Riyadh region).
- Sessions are **live or non-live**; an **AI session summary** is produced.
- **Questions to the moderator** inside a session. Question availability is
  **time- and location-gated:** questions **open** on arrival at the hall and
  **5 minutes before** the session, and **close at session end**.
- **Comments** pass two moderation gates: **(1) AI filtering**, then
  **(2) admin review/approval** via the Control Panel.
- *Screen-level note:* the standalone "send question / request interview /
  audience comments" screens (26–28) are removed **as screens**; the question
  and comment **features remain**, integrated into the session/live experience.
  The interview-request feature is dropped.

### 7.6 Networking & Cognitive AI
| Feature | Description |
|---------|-------------|
| One-to-one meetings (اللقاءات الثنائية) | Attendee sends a meeting request → approved by the PR team |
| Meet people like you (قابل أشخاص مثلك) | Matchmaking by shared interests and sessions; produces a match score; ≥ 80% → auto session recommendation + instant push notification |
| Interests (الاهتمامات) | Topics chosen by the user; feed the matchmaking |
| Cognitive AI (الذكاء الاصطناعي المعرفي) | Managed from the Control Panel; has basic settings with **two setting levels**. Provider **not yet approved** (Gemini proposed, not accepted) |
| AI session summary | NLP-generated summary per session |
| AI comment filtering | First-stage automatic filter on comments (see §7.5) |
| Accessibility AI | Sign-language / speech conversion, live captions/translation |

### 7.7 Notifications & Reminders
- **Channels:** SMS, Email, WhatsApp, In-App — behind a single notification
  abstraction (channel mix is configuration, not code).
- **Reminders include:** "session started — you did not attend", "session
  started — you did not enter", "booking confirmed", registration/approval
  updates, session reminders, VIP invitations, meeting confirmations.

### 7.8 Media Coverage & News
- A **Media Coverage** section is added to the main menu, including
  **social media** and **posts**; all content is entered and managed from the
  Control Panel. *(The section/label "Media Coverage" is to be **renamed** — new
  name TBD.)*
- **News** is managed from the Control Panel.
- Also under media: photo & video gallery, media partners.

### 7.9 Previous Editions (Archive)
For each previous year the system stores and displays: forum title, brief,
sessions title, place, time, image, video, previous speakers list, statistics.
Current-edition archive visibility is controlled from the Control Panel.

### 7.10 Statistics & Dashboards
- Per-day statistics: registrations, badges printed, registered VIP count,
  media badges printed, total check-ins.
- Overall: themes, topics, speakers, participating countries, total
  registrations/badges, attendance per day, total attendance, broadcast hours,
  total audience questions.
- Live attendance tracking via the entry gates; GPS-presence tracking of
  movement, dwell time and routes.

### 7.11 Control Panel
Permission-driven. From the Control Panel admins:
- Approve users, set the final user type, grant permissions.
- Manage content, sessions, comments, the Cognitive AI, media coverage, news,
  and previous years.
- Manage registrations (accept/refuse), speakers, booths, one-to-one meetings,
  users (level & type).
- **Dynamic configuration:** titles, texts, logos/images, in-app welcome
  message, banners, sections & labels, brand colours, pages — all without code.
- **Dynamic categories:** registration types, section/page names, user
  categories, interests, sessions — add/delete/hide, with a per-category colour.
- Registration open/close control; archive visibility control.
- **Operation log (Logs)** of changes and approvals.

---

## 8. Mobile App — Screen Inventory (41 Screens)

| Section | Screens | Coverage |
|---------|---------|----------|
| A — Onboarding & Authentication | 1–12 | Splash, onboarding, login, sign-up (1–2), OTP (+ alternate photo-verification variant), visitor seat pick, sponsor details, terms, registration confirmed, registration status, guest mode |
| B — Main App | 13–20 | Home, My Area, 3D map, agenda, session detail, my seat, speakers, speaker profile |
| C — Content & Events | 21–24 | ~~Delegations~~, booths, sponsors, archive |
| D — Live Broadcast & Engagement | 25–28 | Live broadcast, send question*, request interview*, audience comments* |
| E — Media Coverage | 29–31 | News, photo/video gallery, media partners |
| F — Badge & Notifications | 32–33 | Entry badge & QR, notifications |
| G — Smart Features | 34–37 | AI session summary, meet people like you, AI assistant, about the forum/pillars |
| H — Settings & Legal | 38–41 | Accessibility, ~~cybersecurity~~, rate the forum, more |

`*` Screens 21, 26, 27, 39 are removed/cancelled per the 2026-05-20 baseline;
the alternate Screen 06 photo-verification variant exists in the mockup
(commented out) and includes an explicit "exception for women".

---

## 9. Registration Workflow

```
Sign up (email + password) → email verification code → complete data
   → choose type (Visitor / Other) → personal + identity + contact data
   → attachments → pick "direction / track"
   → request submitted  →  status: "waiting for approval"
        │
        ▼  (Control Panel)
   Admin reviews → approves → assigns permissions → sets final user type
        │
        ▼
   Badge issued  →  QR / barcode verification at the venue  →  entry
```

A user may use the app as a **Guest** at any time without registering.
On-site: search the system → if registered, **reprint badge**; else **instant
on-site registration** → QR verification → entry.

---

## 10. Technology & Architecture

| Layer | Technology |
|-------|------------|
| Backend platform | **.NET 10** (Core) |
| API framework | **FastEndpoints** (REPR pattern, OpenAPI) |
| Web UI | **Blazor** (+ MudBlazor component library — project standard) |
| Mobile | **Flutter** — Android + iOS |
| Database | **SQL Server 2022** |
| Hosting | Windows Server 2022, local Saudi hosting via STC; HTTPS/SSL |
| Real-time | **SignalR** — live chat, presence, push, live list updates |
| Logging | **Serilog** — structured audit streams |
| Cognitive AI | Provider **not yet approved** (Gemini proposed); CP-managed, 2 setting levels |
| DevOps | **Azure DevOps** — Repos, Boards, Pipelines, Test Plans |

**Notification channels:** SMS, Email, WhatsApp, In-App behind one abstraction.
**Environments:** Dev → Test → Staging → Production, test-gated promotion.

---

## 11. Security & Compliance

### 11.1 Defense-in-depth
- Token headers: App Key + Device Type + Language + JWT per request.
- JWT 30-minute expiry; rotating refresh tokens; 30-day sessions.
- **Admin MFA** via TOTP (Google Authenticator).
- Anti-forgery (SFC) tokens on state-changing requests; rate limiting
  (per-IP / user / endpoint); full **RBAC** — no endpoint implicitly open.
- Encryption: AES-256 (fields), RSA (data), SSL/TLS (transport).
- Anti-spoofing on camera/photo capture, with an explicit **exception for
  women** (alternate verification, no photo upload).
- GPS Presence for location-based feature gating and attendance.

### 11.2 Regulatory compliance
NCA **Secure Application Development Standard** applies — SSDLC, DevSecOps,
secured source-code repository, static/dynamic analysis, peer review,
vulnerability assessment and penetration testing before and after deployment,
defect register, CI/CD-integrated testing. Referenced: NCA **ECC-1:2018**,
**CSCC-1:2019**, **OWASP Top 10 2021**, **OWASP ASVS**, and MoD information
security policy.

> Note: the in-app *cybersecurity policy page* (Screen 39) is removed from the
> app menu per the 2026-05-20 baseline; the **engineering compliance obligations
> above remain fully in force.**

### 11.3 Source-code & delivery obligations
- Full, modifiable source code handed over by **25/01/2026**.
- Security clearance from authorities / NCA-accredited firms before publishing.
- MoD cyber-centre secure-code review before penetration testing.

---

## 12. Delivery Plan (Agile)

| Metric | Value |
|--------|-------|
| Total duration | 18 weeks |
| Delivery milestones | 12 delivery points |
| Main phases | 9 |
| Continuous testing | 22 days |
| Schedule window | ~17 May 2026 → ~20 September 2026 |
| Goal | System operational ~2 months before the forum |

- **Scrum**, two-week sprints; Azure DevOps CI/CD (Commit→Build→Test→Deploy→
  Monitor); branch policies enforce peer review.
- Quality: zero warnings, root-cause fixes, peer review, no duplication, full
  documentation, freeze governance.
- Three test layers: Unit (per method), Integration (per endpoint), E2E (per
  scenario).
- Milestones M1 design (day 6) · M2 UI/UX (wk7) · M3 app dev (wk12) · M4 testing
  (wk16) · M5 live ready (wk16) · M6 store publish (wk17) · M7 operation (wk18).
- Team: 20 core + 14 testers — AI Specialist ×1, .NET Devs ×2, Flutter ×1,
  DevOps ×1, UI/UX ×1, QA/Testers ×22.
- **Governance:** all requirements before testing; change freeze (changes before
  testing, none after publish); Backend runs in parallel from day 1; test
  environment from day 1; live environment two full weeks before publish; an
  approved baseline is binding on both parties.

---

## 13. Domain Glossary (Arabic ⇄ English)

| Arabic | English | Meaning |
|--------|---------|---------|
| الملتقى | Forum | The SIMF event |
| المحاور | Themes / Pillars | The five thematic pillars |
| الجلسة | Session | A programme session (live or non-live) |
| القاعة | Hall / Room | A venue room with seating capacity |
| المتحدث | Speaker | A presenting speaker (shown with country flag) |
| المحاور (دور) | Moderator | App user who manages session questions |
| الأجنحة | Booths | Exhibitor stands |
| العارض | Exhibitor | An exhibiting organisation |
| الرعاة | Sponsors | Strategic / Premium / Gold |
| الزائر | Visitor | General attendee (VIP / Normal / …) |
| الضيف | Guest | Unregistered app user |
| اسطاف | Staff | Organising staff |
| البادج | Badge | QR entry badge |
| التوجه / المسار | Direction / Track | Chosen after registration — to be defined |
| الحجز | Booking | Session/seat reservation |
| التغطية الإعلامية | Media Coverage | Media section (to be renamed) |
| لوحة التحكم | Control Panel | The admin console |
| الذكاء الاصطناعي المعرفي | Cognitive AI | CP-managed AI with two setting levels |

---

## 14. Scope Exclusions & Removed Items

- **Delegations (الوفود)** — removed (feature + Screen 21).
- **Cybersecurity page (Screen 39)** — removed from the app; replaced by
  **Policies** and **Terms & Conditions for registration**.
- **Audience-comments / interview-request screens (26, 27)** — removed as
  screens; question & comment features remain; interview-request dropped.
- **Nafath login** and **Face ID** — removed.
- **Phone-number validation** — not required.
- **Out of system scope:** physical entry/exit gate hardware, ERP integration.
- "Media Coverage" and "Profile" — to be **renamed** (new names TBD).

---

## 15. Deferred — To Be Detailed Later

Carried from the 2026-05-20 meeting; required before/within the DDD design:

1. Visitor types in detail.
2. The "Other" user type — definition.
3. Meaning of "direction / track" (التوجه/المسار).
4. Permissions per user type.
5. Screens per case in the app.
6. Exhibitor approval cycle.
7. Moderator workflow.
8. Staff workflow.
9. Booking & attendance detail.
10. Hall-arrival verification mechanism.
11. Question open/close mechanism.
12. AI comment-filtering rules.
13. Cognitive-AI setting levels (the two levels).
14. Media Coverage detail.
15. News detail.
16. Statistics detail.
17. Legal text for Terms & Conditions and Policies.
18. New names for the renamed "Media Coverage" and "Profile".

---

## 16. Open Confirmations

1. **Cognitive AI provider** — Gemini is not approved; a provider decision is
   pending.
2. **Live-broadcast provider/platform** — to be confirmed.
3. **WhatsApp Business provider** — to be confirmed.
4. **SQL Server 2022 edition/licensing** for the target host — to be confirmed.

---

## 17. Source Documents Index

Located in `D:\SIMF\System\15-04-2024` plus the 2026-05-20 meeting intake:

| # | Document | Type |
|---|----------|------|
| 0 | 2026-05-20 client meeting intake | **Authoritative baseline** (Appendix A) |
| 1 | SIMF_Screen_Guide_and_User_Journey.docx | 41-screen spec & journeys |
| 2 | Technology-Methodology-Approval.pptx / .pdf | Proposal — stack, security, methodology |
| 3 | Technology-Methodology-Approval-Checklist.xlsx | Approval checklist |
| 4 | flowcharts الملتقى البحري.pdf | 3 registration flowcharts |
| 5 | متطلبات الفريق التقني.pdf | 41 technical-team requirements |
| 6 | متطلبات التطبيق … Mockup.pdf | App/site requirements + Mockup notes |
| 7 | Overall Time & Plan.pdf | 18-week plan, milestones, risks |
| 8 | مرحلة التحليل والتصميم … اصدار2.pdf | Analysis & design |
| 9 | معيار أمن تطوير التطبيقات.pdf | NCA Secure Application Development Standard |
| 10 | Mockup.html | Interactive 41-screen structural mockup |
| 11 | New Text Document.txt | Pre-review agreement points |
| 12 | دليل هويه البصريه د copy.pdf | Visual identity guide — captured in SIMF-VID-001 |

> All visual material is a **functional wireframe**, not final UI.

---

## Appendix A — 2026-05-20 Client Meeting Intake (verbatim)

Requirements collected at the meeting, as received, without analysis.

1. **System type** — an integrated exhibition/forum management system.
2. **Technologies** — Blazor (UI), .NET (backend), Flutter (mobile),
   SQL Server 2022 (database).
3. **User types** — *General system:* Admin, Visitor, Exhibitor, Staff, Other.
   *Visitor types:* VIP, Normal, others later. *Mobile app:* Guest, Visitor,
   Exhibitor, Moderator, Staff. Each type has its own screens, permissions and
   use cases.
4. **Control Panel** — works via permissions; admins manage the system; from it:
   approve users, set user type, grant permissions, manage content, sessions,
   comments, AI, media coverage, news, previous years.
5. **App registration** — Step 1: email, password, confirm password, email
   verification code, enter code. Then complete the rest of the data. Choose
   type: Visitor or Other. Personal data: 4-part Arabic name, English name per
   passport, nationality, DOB, place of birth. Identity: Saudis → national ID;
   non-Saudis → choose passport or Iqama number. Contact: mobile inside KSA,
   mobile outside KSA. Attachments: ID image, others later. After registration:
   pick a direction/track; receive a message with contact data; status =
   waiting for approval. App is usable without registration as a guest.
6. **Approval from the Control Panel** — request appears in the Control Panel;
   admin reviews, approves, assigns permissions, sets the final user type.
7. **Sessions & moderators** — questions to moderators inside a session;
   questions open on arrival at the hall / 5 minutes before, close at session
   end. Speakers shown with country flag, data and photo. Comments pass two
   stages: AI filtering, then admin review/approval via the Control Panel.
8. **Broadcast** — sessions may be live or non-live; AI session summary exists.
9. **List/naming changes** — rename "Media Coverage" and "Profile" (new names
   later); delete Delegations and Cybersecurity; add Policies and Terms &
   Conditions for registration.
10. **Main menu** — add Media Coverage (social media, posts, CP-managed); News
    (CP-managed); Cognitive AI (CP-managed, basic settings, two levels).
11. **Previous years** — per year: forum title, brief, sessions title, place,
    time, image, video, previous speakers list, statistics.
12. **Notifications** — reminders, e.g. session started and user not present /
    not entered; booking confirmed.
13. **Needs detail later** — see §15.

*End of document.*
