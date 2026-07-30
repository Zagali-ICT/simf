# Bilateral meetings (اللقاءات الثنائية) — `/meetings`

| | |
|--|--|
| **Route** | `/meetings` (route name `meetings`, `RouteNames.meetings` → `MeetingsScreen`, route #116) — Figma node `1408:9726` |
| **Layout** | SIMF app shell (`SimfPageShell`), back chevron + centred title |
| **Surface** | Mobile App (Flutter) |
| **Audience** | **VIP** approved attendee only |
| **Auth** | **Approved + VIP.** The Home tile is hidden for non-VIP; the route is role-gated to attendees; the page enforces VIP in-screen; and the backing meeting-request endpoint is VIP-only server-side (403). |
| **Pattern** | D-745 split (owner 2026-07-11): the Home "اللقاءات الثنائية" tile now opens this VIP meetings page — a **filtered view** of the same `GET /app/my-requests` feed (approved + upcoming meetings only). The full requests log stays on the history page ([`requests.md`](requests.md), retitled **طلباتي**). |
| **Status** | 🟢 Screen built (D-745, Figma `1408:9726`) |
| **Implements use case(s)** | See my confirmed, upcoming bilateral meetings; start a new meeting request (VIP); jump to my full requests history. |
| **Backend endpoints** | `GET /api/v1/app/my-requests` (feed — filtered client-side to accepted + upcoming meeting kinds; D-745 added append-only `speakerId` + `countryId` for the card photo + flag) · `POST /api/v1/app/speakers/{id}/meeting-requests` (create, VIP-only, via the sheet) · `GET /api/v1/app/speakers` + `…/{id}/available-slots` (the picker). |
| **Source file** | Flutter `features/meetings/` (screen + `MeetingCard` + `MeetingActionRow` + `upcomingMeetingsProvider`); the create sheet is the shared `features/speakers/widgets/meeting_request_sheet.dart`. Backend `MyRequestsService` + `AppRequestItem` contract. |
| **Tests** | [`docs/tests/e2e/mobile-meetings.md`](../../tests/e2e/mobile-meetings.md) (`E2E-MOBMEET-001..011`); widget `test/features/meetings/meetings_screen_test.dart`; golden `test/golden/meetings_golden_test.dart` (`meetings_1408-9726.png`). |
| **Last reviewed** | 2026-07-11 |

---

## 1. Purpose

The bilateral-meetings page is the **VIP** attendee's at-a-glance view of their
**confirmed, upcoming** bilateral meetings — speaker meetings and delegation
meetings that the team has **accepted** and whose slot has not yet passed. It is
deliberately **not** the requests log: pending, rejected, cancelled and past
requests do not appear here — they live on the requests-history page (طلباتي).
The page adds a **"طلب جديد"** action to start a new meeting request (the shared
speaker-picker sheet) and a **"السجل"** button that jumps to the full history.

This page was split from the unified requests feed by **D-745** (owner
2026-07-11) so that the Home "اللقاءات الثنائية" tile means *meetings*, while the
profile keeps *my requests (history)*.

## 2. Audience + permissions

- **Who can reach it:** an approved **VIP** attendee, from the Home
  "اللقاءات الثنائية" tile. The tile is **hidden for non-VIP** accounts.
- **Route gate:** role-gated to attendees (Visitor / Exhibitor) — guest / staff /
  moderator are redirected home. VIP is **not** an `AppRole`, so it is enforced
  in-screen (and server-side), not by the router role table.
- **In-screen gate:** a non-VIP who still reaches `/meetings` sees the VIP-only
  state ("حجز فترة اجتماع متاح لضيوف كبار الشخصيات فقط"), never the list or the
  create button.
- **Server gate:** `POST …/meeting-requests` returns **403** for a non-VIP; the
  sheet surfaces the VIP-only message.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with cards) | `test/golden/goldens/meetings_1408-9726.png` | ✅ golden (Arabic, 375×760) |
| Empty state | `docs/screenshots/meetings-empty.png` | _pending on-device capture_ |
| VIP-only state | `docs/screenshots/meetings-vip-only.png` | _pending_ |

> Figma reference frame: `1408:9726`.

## 4. UI affordances

### 4.1 Header
Back chevron + centred title **اللقاءات الثنائية** / "Bilateral meetings".

### 4.2 Action row (Figma 1408:9736)
Two equal pills (shared `RequestActionButton`):

| Pill | Style | Action |
|------|-------|--------|
| طلب جديد / New request | beige outline | opens the meeting-request sheet (speaker picker → slot → send) |
| السجل / Log | gold filled | navigates to the requests-history page (طلباتي, `/requests`) |

### 4.3 Meeting card (one per approved-upcoming meeting)

| Element | Source | Notes |
|---------|--------|-------|
| Headline | kind | "طلب لقاء مع متحدث" (speaker) / "طلب اجتماع وفد" (delegation) |
| Rank | `subtitle` | speaker's rank; omitted when null (delegation) |
| Flag badge | `countryId` | 48×48 green well + nationality flag emoji |
| Photo | `speakerId` | speaker photo via `/app/assets/SpeakerPhoto/{id}/image`; anchor placeholder for a delegation (no speaker) |
| Name | `title`/`titleArabic` | gold; the speaker name / target country |
| Chevron | `speakerId != null` | tap the card → the speaker profile; absent for a delegation |
| Date + clock | `eventDate` | the meeting slot, "07:45 AM · اليوم" today else the date |

## 5. The create flow (shared sheet)

"طلب جديد" opens `MeetingRequestSheet` (Figma 1776:5036) with the **speaker
picker** (D-745): a selectable list of every speaker showing the **photo + name +
country flag + rank** (no longer a bare dropdown). A **type-to-filter search**
sits above the list (D-746, key `meeting-speaker-search`): typing filters the
speakers by **name or rank** (the same case-insensitive match as the المتحدثون
list); a query that matches nobody shows the shared "لا نتائج مطابقة" hint. A
speaker already selected stays visible even when it does not match the query, so
the picker never hides the target the request is submitted to.
Selecting a speaker loads their **real availability** day-cards + time-slots
(D-709); with a subject and a picked slot the request is sent
(`POST …/meeting-requests`). Booking a slot is VIP-only server-side.

**G3 (D-801, owner 2026-07-30) — a slot is now mandatory.** When the chosen target
has **no free slot** — no active future availability window, or every slot already
past or taken — the sheet shows the "لا توجد فترات متاحة حالياً" notice **and the
Send button is disabled**; the subject-only request D-767 R1 allowed is gone. The
server backs the same rule with **409 `SPEAKER_MEETING_NO_AVAILABILITY`**
(delegation twin: `DELEGATION_MEETING_NO_AVAILABILITY`). A **failed** slot fetch is
a separate state — "تعذر تحميل القائمة." plus a **Retry** — so a network blip is
never presented as the target having no availability.

## 6. Data flow

```
Home "اللقاءات الثنائية" tile (VIP only) → /meetings
  → MeetingsScreen watches currentUserIsVipProvider
      → non-VIP: VIP-only state
      → VIP: upcomingMeetingsProvider = myRequestsProvider filtered to
             (status == Accepted) && meeting-kind && (no slot | slot in future)
  → GET /app/my-requests (approved-only) → filter client-side → MeetingCards
طلب جديد → MeetingRequestSheet → POST …/meeting-requests → invalidate feed
السجل → /requests (the full history, طلباتي)
```

`AppRequestItem { kind, id, title, titleArabic, status, eventDate?, createdAt,
canCancel, subtitle?, speakerId?, countryId? }` — `speakerId`/`countryId` are the
D-745 append-only additions for the card photo + flag (wire contract preserved).

## 7. States (loading / error / empty / gate)

- **Loading:** a spinner while the VIP check / feed is in flight.
- **Error:** the shared error surface ("تعذّر تحميل طلباتك") with retry + pull-to-refresh.
- **Empty:** "لا توجد مقابلات بعد." below the action row (which stays available).
- **VIP-only:** the premium-gate message for a non-VIP viewer.

## 8. i18n + RTL

All strings localized (AR/EN): title (اللقاءات الثنائية / Bilateral meetings), the
طلب جديد/السجل row, the card kind headlines, the empty state, the VIP-only message.
Under Arabic the header, the buttons, and the cards (headline right, flag badge
inline-end, speaker photo inline-start) mirror right-to-left; the golden locks it.

## 9. Edge cases + known limitations

- **Approved + upcoming only.** An accepted meeting with **no slot yet**
  (`eventDate == null`) still shows (it is not "done"); a **past-dated**
  accepted meeting drops off. Pending / rejected / cancelled never appear here.
- **Status-filter chips are intentionally omitted** — the list is single-status
  (accepted), so chips would be redundant. They remain on the history page.
- **Delegation cards** carry the target-country flag but no speaker photo
  (anchor placeholder) and no chevron (no speaker to open).
- **Consistency deviation:** the meeting card renders real feed data; the speaker
  photo + flag rely on the D-745 additive wire fields.

## 10. Related E2E test scenarios

See [`docs/tests/e2e/mobile-meetings.md`](../../tests/e2e/mobile-meetings.md)
(`E2E-MOBMEET-001..011`): the golden list, the create flow, the rich picker, the
السجل → history nav, the approved+upcoming filter, card → speaker profile, empty
state, both VIP gates (tile hidden + in-screen), server-500, and RTL.

## 11. Related docs

- Requests history (sibling): [`docs/pages/mobile/requests.md`](requests.md).
- Speaker profile (the other create entry): [`docs/pages/mobile/speaker-profile/`](speaker-profile/README.md).
- Decisions log: **D-745** (this split + the additive `speakerId`/`countryId`
  wire fields + the VIP tile gate). Related: D-500 (the requests feed), D-590
  (the rank subtitle), D-709 (real availability slots), D-729 (VIP-only create),
  D-740 (speaker identity cells).

## 12. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-11 | D-745 | Split the Home "اللقاءات الثنائية" tile into a VIP-only meetings page (Figma `1408:9726`); the requests feed retitled **طلباتي** and kept in My-Area. Added append-only `AppRequestItem.speakerId` + `countryId` for the card photo + flag (no migration — enriched the existing speaker join). The create-sheet speaker picker became a photo/name/country list. Home tile hidden for non-VIP. |
| 2026-07-11 | D-746 | Added a **type-to-filter search** above the create-sheet speaker picker (name/rank, mirroring the المتحدثون list; shared "لا نتائج مطابقة" hint). App-only; no wire/schema change. |
| 2026-07-30 | D-801 (G3) | A meeting request **cannot** be sent when the target has no free slot — supersedes D-767 R1's subject-only request. Both sheets require a picked slot and disable Send on an empty slot list; the API 409s `SPEAKER_MEETING_NO_AVAILABILITY` / `DELEGATION_MEETING_NO_AVAILABILITY`. A failed slot fetch now shows a load error + Retry instead of the no-availability notice. No schema/wire change (additive error codes). |

---

_Last reviewed:_ 2026-07-11 by SIMF Team (D-745 — bilateral meetings split).
