# Confirm delegation meeting (تأكيد اجتماع الوفد) — mobile `/meeting-confirm?requestId=`

| Field | Value |
|---|---|
| Route | `/meeting-confirm?requestId=` (`RouteNames.meetingConfirm`, page #117) · role-gated to `_attendee` = `{AppRole.visitor, AppRole.exhibitor}`; per-user eligibility (being a member of the **target** delegation) is enforced server-side, not here |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/meetings/meeting_confirm_screen.dart` (`MeetingConfirmScreen`, 241 lines, `ConsumerStatefulWidget`) |
| Widgets | None — the confirm and decline buttons are private builders (`_confirmButton` / `_declineButton`) on the screen, each keyed for tests (`delegation-meeting-confirm` / `delegation-meeting-decline`) |
| Figma node | **None bound.** No frame exists for this screen (it is a notification-deep-link leaf); the layout is token-built, not Figma-derived |
| Shell | `SimfPageShell` (title تأكيد اجتماع الوفد) |
| API | `POST /app/delegation-meeting-requests/{id}/confirm` and `POST /app/delegation-meeting-requests/{id}/decline` — both with an empty body, both returning the same `DelegationMeetingSummary` |
| Providers | `delegationsRepositoryProvider` (`features/delegations/data/delegations_repository.dart`); no `FutureProvider` — the screen holds `_outcome` / `_error` / `_submitting` in local state because it performs writes, not a read |
| Tests | `test/features/meetings/meeting_confirm_screen_test.dart` (5). No golden (no bound frame). E2E [`mobile-meeting-confirm.md`](../../../tests/e2e/mobile-meeting-confirm.md) |
| Status | ✅ Real — the bi-meeting rework's other-party confirm screen; **B8** added the decline action |

## 1. Purpose

The other party's one-tap answer to a bilateral delegation meeting request:
confirm it, or decline it. Reached from a `MeetingRequested` notification.

> **Contract.** Eligibility and state are enforced **server-side**; the screen only
> maps the outcome — **403** = you are not the other party, **409** = the request is
> not awaiting confirmation, anything else = generic retry. The success summary
> carries **no requester PII** (stripped server-side).

## 2. Audience & access

Attendee roles only in the router (visitor + exhibitor — this keeps guest, staff
and moderator out of the screen). The real gate is the server: only an eligible
member of the **target** delegation may confirm or decline.

**A30 — this screen is delegation-only.** The speaker double-opt-in link lands on
the **Website's** anonymous `/meeting/confirm?token=` page instead, so driving a
speaker meeting through here is a 403 / 409 by design. The Arabic title is
deliberately "تأكيد اجتماع الوفد" rather than a generic "confirm meeting", so the
two are not confused.

## 3. Entry point

There is **no in-app push** to this route. It is reached only by a notification
`clickUrl` deep link: `/meeting-confirm?requestId=…`, allow-listed in
`features/notifications/notifications_screen.dart` `_allowedClickPaths` (only the
path is matched; the query string is ignored, D-678). The notification kind is
`MeetingRequested` (`features/notifications/notification_filters.dart:32`).

`requestId` is read off `state.uri.queryParameters['requestId']`, defaulting to
`''` — which is what drives the "missing" state below.

## 4. UI & behaviour

Three mutually exclusive bodies, chosen in `build`:

1. **Missing id** (`requestId.isEmpty`) → centred `SimfEmptyState`
   (`Icons.event_busy_outlined`, `l10n.meetingConfirmMissing`).
2. **Confirm view** (`_outcome == null`) → a gold `Icons.handshake_outlined`, the
   intro paragraph, the inline error when one is set, then the gold **confirm**
   button and the outlined **decline** button beneath it.
3. **Success view** (`_outcome != null`) → `Icons.check_circle_outline` or
   `Icons.cancel_outlined` depending on `_declined`, the done heading, the
   `"requestingCountry — targetCountry"` parties line, the subject, and the slot
   formatted `2026-11-24 · 10:00 ص` (ISO date · 12-hour local time via
   `formatDateIso` + `formatDateTime12h` on `saudiOf(slotStart)`).

Both bodies are `ListView`s with `AlwaysScrollableScrollPhysics` even though
neither is long — the physics is what keeps them well-behaved inside the shell.

`_submit(decline:)` is single-flighted by `_submitting`; while it runs the confirm
button's label swaps to `l10n.loadingLabel` and both buttons drop their tap.

## 5. Actions

| Control | Handler | Backend | Outcome |
|---|---|---|---|
| Back | `backOrHome(context)` | — | Pops, or Home |
| تأكيد (gold) | `_confirm` → `_submit(decline: false)` | `POST …/{id}/confirm` | `_outcome` set, `_declined = false` → success view |
| رفض (outlined) | `_decline` → `_submit(decline: true)` | `POST …/{id}/decline` | `_outcome` set, `_declined = true` → success view; server notifies the requester and releases the hall slot |

The decline button is deliberately **secondary** (surface fill, beige hairline
border) so declining never reads as the primary path but is always reachable.

## 6. Error mapping

| HTTP | Message |
|---|---|
| 409 | `l10n.meetingConfirmNotAwaiting` — the request is not awaiting confirmation (already answered, cancelled, or expired) |
| 403 | `l10n.delegationNotAllowed` — the caller is not the other party |
| anything else | `l10n.meetingDeclineFailed` when declining, `l10n.meetingConfirmFailed` when confirming |

The message is rendered inline above the buttons, not as a toast, so it survives
long enough to read and the user can retry in place. `_error` is cleared at the
start of every submit.

## 7. Data contract (`DelegationMeetingSummary`)

Both endpoints return the same shape (D-219 frozen keys): `requestingCountry` ·
`targetCountry` · `subject` · `slotStart` (nullable, parsed by
`parseWireOrNull`). No requester name, email or phone — the PII strip is
server-side and this model has nowhere to put it.

The parties line is suppressed when it degrades to a bare `—` (both country names
empty), and the subject and slot blocks each hide when absent.

## 8. i18n / RTL

`AppL10n`: `meetingConfirmTitle` (تأكيد اجتماع الوفد) · `meetingConfirmIntro` ·
`meetingConfirmButton` · `meetingConfirmDone` · `meetingConfirmMissing` ·
`meetingConfirmNotAwaiting` · `meetingConfirmFailed` · `meetingDeclineButton` ·
`meetingDeclineIntro` · `meetingDeclineDone` · `meetingDeclineFailed` ·
`delegationNotAllowed` · `loadingLabel`. Everything is centre-aligned, so the body
needs no directional padding; the slot line's date is ISO and its time is
localised 12-hour (`ص` / `م` in Arabic).

## 9. Findings (recorded, not changed)

1. **No pull-to-refresh, and correctly so** — the screen performs a write and has
   no load or error branch to re-run. It is one of the reviewed exemptions in
   `test/repo/pull_to_refresh_coverage_test.dart`.
2. **The success view is terminal.** There is no "back to my meetings" action; the
   user must use the back chevron, which pops to the notification list.
3. **A stale deep link is indistinguishable from an ineligible one** to the user —
   both a request already answered and a request belonging to someone else land on
   an inline sentence with the confirm button still enabled underneath.
