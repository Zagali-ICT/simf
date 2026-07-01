# E2E test catalogue — `Live broadcast` (`live`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> live read is already built + anonymous (D-271; additive `LiveStreamUrl` +
> `LiveSignLanguageUrl` on the session detail). The **Flutter screen is built**
> and widget-tested in
> `src/Mobile/simf_app/test/features/live/live_broadcast_screen_test.dart`
> (no-id empty, blank-id empty, not-live, recording note, sign-language note,
> 404, error→retry, Arabic). It reuses the public session detail wire contract
> via a small `LiveRepository` — no duplicate of the full detail model. The
> non-video paths are tested directly; real playback needs a device, so the
> video path is exercised manually only.
>
> **Figma parity (D-433):** the screen is now re-skinned to the KSA-Project frame
> **934:3450** on the shared navy shell — the navy header, the black player
> surface carrying the LIVE badge + the gold-bordered AI live-caption strip, the
> "يُبث الآن · {hall}" now-broadcasting block (session title + speakers as gold
> bullets), the gold region-restriction notice card, the ask-a-question entry,
> and the "الجلسات القادمة" upcoming-session cards (loaded non-blocking from the
> agenda list). The hall name, speakers line and upcoming cards render only when
> the wire carries them; this screen never fabricates the missing rows.
>
> **AI live captions (P5 — D-439):** the session detail now carries optional
> bilingual `LiveCaptions` / `LiveCaptionsArabic` text (an admin-set field — the
> provider is stubbed, manual entry for the POC). When present the gold-bordered
> caption strip shows the active-locale text in white; when blank it shows the
> muted placeholder hint (and YouTube CC supplies captions for a YouTube feed).
> The strip renders only on the live-feed branch (no stray strip on a
> recorded/not-live session). Same change fixed a pre-existing bug where editing a
> session via the admin PUT silently wiped its live feed URLs (regression test
> `Update_round_trips_all_live_fields`).

| | |
|--|--|
| **Page** | [`Page_025`](../../App/Page_025/README.md) |
| **Route** | `GET /api/v1/app/programme/sessions/{id}` · app screen #25 `/live?sessionId=` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Signed-in for the screen (D-577)** — the live screen is login-only: a guest sees an in-screen "need login" prompt + a Sign-in button, never the player. The read endpoint stays `AllowAnonymous` (the app gates the screen, not the API); use an approved Visitor token to reach the player. |
| **Last reviewed** | 2026-07-01 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB025-001 | No `sessionId` → "pick a session" empty state, no fetch | edge | P0 | authored ✓ (screen `no sessionId shows the pick-a-session empty state`) |
| E2E-MOB025-002 | A YouTube / HLS live URL → the player + LIVE badge | happy | P0 | manual (real playback needs a device — not widget-tested) |
| E2E-MOB025-003 | No stream + no recording → the not-live state | edge | P1 | authored ✓ (screen `no stream + no recording shows the not-live state`) |
| E2E-MOB025-004 | No stream but a recording → the recording note | edge | P1 | authored ✓ (screen `no stream but a recording shows the recording note`) |
| E2E-MOB025-005 | A sign-language URL → the sign-language note | edge | P2 | authored ✓ (screen `a sign-language url shows the sign-language note`) |
| E2E-MOB025-006 | Session 404 → not-found state | resilience | P1 | authored ✓ (screen `a 404 shows the not-found state`) |
| E2E-MOB025-007 | A read failure → error + Retry that re-fetches | resilience | P0 | authored ✓ (screen `a non-404 failure shows error + retry, which re-fetches`) |
| E2E-MOB025-008 | Both feeds set → the البث/لغة الإشارة toggle swaps the source (D-349) | happy | P1 | authored ✓ (screen `both feeds set shows the main / sign-language toggle` + `a single (main-only) feed shows no toggle`) |
| E2E-MOB025-009 | URL rule (D-349): YouTube (valid 11-char id) + HLS/MP4 accepted; no-id / other rejected; video-id parsing | unit | P1 | authored ✓ (`youtube_url_test.dart`) |
| E2E-MOB025-010 | A feed that fails to load → error + Retry surface, not an endless spinner (D-349) | resilience | P1 | authored ✓ (screen `an unplayable feed surfaces the error state with a retry`) |
| E2E-MOB025-011 | Figma 934:3450 re-skin — navy header "البث المباشر", black player surface, LIVE badge, AI-caption strip (D-433) | i18n/layout | P0 | _to author_ |
| E2E-MOB025-012 | Header line "يُبث الآن · {hall}" (now-broadcasting + hall name) (D-433) | happy | P0 | _to author_ |
| E2E-MOB025-013 | Session title + speakers/participants gold bullet line (D-433) | happy | P1 | _to author_ |
| E2E-MOB025-014 | "الجلسات القادمة" upcoming-session cards: title + gold HH:mm chip (D-433) | happy | P1 | _to author_ |
| E2E-MOB025-015 | Upcoming list empty / fails → strip hidden, live screen still works (D-433) | resilience | P1 | _to author_ |
| E2E-MOB025-016 | Gold region-restriction notice card "إشعار: …" (D-433) | happy | P2 | _to author_ |
| E2E-MOB025-017 | Ask-a-question entry "اطرح سؤالاً" → /live/question with sessionId (D-433) | happy | P0 | _to author_ |
| E2E-MOB025-018 | P5 — session with AI caption text → the strip shows the text (white), not the placeholder hint (D-439) | happy | P1 | authored ✓ (screen `P5 — a session with caption text shows it in the caption strip (white)`) |
| E2E-MOB025-019 | P5 — live session with no caption → the muted placeholder hint (D-439) | edge | P1 | authored ✓ (screen `P5 — a live session with no caption shows the placeholder hint`) |
| E2E-MOB025-020 | P5 — caption locale fallback: Arabic text under `ar`, English under `en` (D-439) | i18n | P1 | authored ✓ (screen `P5 — the caption renders the Arabic text under the ar locale`) |
| E2E-MOB025-021 | Login-gate (D-577): a signed-out guest sees the in-screen "need login" prompt + Sign-in button (never the player), and no session is fetched | auth | P0 | authored ✓ (screen `a signed-out guest sees the need-login gate, not the stream (owner, D-577)`) |

## Scenarios

### E2E-MOB025-001 — No session selected

```gherkin
Feature: Live broadcast
  As a guest (signed out)
  I want a clear prompt when I open /live with no session
  So that I know to open a session first

Scenario: Opening /live without a sessionId
  Given the app opens /live with no sessionId query param
  Then the screen shows "No live session selected — open a session to watch."
  And no session detail read is issued
```

**Evidence:** screen test `no sessionId shows the pick-a-session empty state`
(+ `an empty sessionId is treated as no selection`).

### E2E-MOB025-021 — Login-gate (guest sees "need login")

```gherkin
Scenario: A signed-out guest cannot open the stream
  Given the app is signed out (a guest)
  When the app opens /live for a broadcasting session
  Then the player is NOT shown
  And the screen shows "Sign in to watch the live stream." with a Sign-in button
  And no session detail read is issued
  When the guest taps Sign-in
  Then the sign-in screen opens
  # /live is NOT router-redirect-gated (unlike /sessions + session detail under
  # D-576) — the gate is in-screen, so the guest still lands on the live screen.
```

**Evidence:** screen test `a signed-out guest sees the need-login gate, not the
stream (owner, D-577)`; router-gate test `D-577 — a signed-out guest hitting
/live is NOT redirected`.

### E2E-MOB025-002 — Live player (YouTube / HLS, D-349) + E2E-MOB025-008 — toggle

```gherkin
Scenario: A broadcasting session shows the player
  Given the session has a non-empty liveStreamUrl
  When the app reads GET /api/v1/app/programme/sessions/{id}
  Then a YouTube link plays via the youtube_player_iframe IFrame player,
       else an HLS/MP4 URL plays via video_player at the stream's aspect ratio
  And a LIVE badge is shown over the player

Scenario: Both feeds set show a source toggle
  Given the session has both liveStreamUrl and liveSignLanguageUrl
  Then a "البث / لغة الإشارة" toggle is shown
  And selecting "لغة الإشارة" swaps the player to the sign-language feed
```

**Evidence:** the toggle is widget-tested (`both feeds set shows the main /
sign-language toggle`, `a single (main-only) feed shows no toggle`); the URL rule +
video-id parser are unit-tested (`youtube_url_test.dart`). Real YouTube/HLS playback
is **manual** — it needs a device/emulator (no platform channel headless).

### E2E-MOB025-003 — Not live / E2E-MOB025-004 — Recording / E2E-MOB025-005 — Sign language

```gherkin
Scenario: A session with no stream and no recording is off-air
  Given liveStreamUrl is null and hasRecording is false
  Then the screen shows "This session is not broadcasting right now."

Scenario: A session with a recording shows the recording note
  Given liveStreamUrl is null and hasRecording is true
  Then the screen shows "A recording of this session is available."

Scenario: A sign-language stream is announced
  Given liveSignLanguageUrl is non-empty
  Then the screen shows "Sign-language interpretation is available."
```

**Evidence:** screen tests `no stream + no recording shows the not-live state`,
`no stream but a recording shows the recording note`,
`a sign-language url shows the sign-language note`.

### E2E-MOB025-006 — Not found / E2E-MOB025-007 — Error+retry

```gherkin
Scenario: A missing session shows the not-found state
  Given GET /api/v1/app/programme/sessions/{id} returns 404
  Then the screen shows "This session was not found"

Scenario: A failed read offers a retry
  Given the session read fails (non-404)
  Then an error + Retry are shown, and Retry re-runs the read
```

**Evidence:** screen tests `a 404 shows the not-found state`,
`a non-404 failure shows error + retry, which re-fetches`.

### E2E-MOB025-011 — Figma 934:3450 re-skin (header + player band + LIVE badge + AI-caption strip)

```gherkin
Feature: Live broadcast — Figma 934:3450 parity (D-433)
  As a guest (signed out)
  I want the live screen to match its KSA-Project frame
  So that the broadcast feels like the rest of the navy shell

Scenario: A broadcasting session renders the re-skinned frame
  Given the session has a non-empty liveStreamUrl
  When the app opens /live?sessionId={id} in English
  Then the navy KSA header shows the centred title "Live broadcast" with a circled back chevron
  And a full-bleed black player surface carries the player
  And a red "LIVE" badge is pinned to the top-start of the player
  And a gold-bordered AI live-caption strip below the player reads
       "Live captions of the spoken word appear here…" with a gold "AI" badge

Scenario: Arabic renders the badge + caption strip in Arabic
  Given the device locale is Arabic
  Then the LIVE badge reads "مباشر"
  And the caption strip reads "الترجمة الفورية للنص المنطوق تظهر هنا..."
  And the header reads "البث المباشر"
```

**Evidence:** screen render at frame 934:3450; the badge/caption text are
bilingual (`liveNowLabel` مباشر/LIVE, `liveCaptionHint`). Real player composition
is **manual** (a device is needed for the platform view).

### E2E-MOB025-012 — Now-broadcasting header line "يُبث الآن · {hall}"

```gherkin
Scenario: The now-broadcasting block shows the hall name when live
  Given the session is live (liveStreamUrl non-empty) and its hall is "القاعة الرئيسية"
  When the screen renders in Arabic
  Then the now-broadcasting line reads "يُبث الآن · القاعة الرئيسية"

Scenario: The English line uses the English hall + label
  Given the session is live with hall "Main Hall"
  And the device locale is English
  Then the line reads "Now broadcasting · Main Hall"

Scenario: A session with no live feed uses the plain session label
  Given liveStreamUrl is null (recording or not-live state) and the hall is unknown
  Then the header line reads only "الجلسة" / "Session" with no "·" hall suffix
```

**Evidence:** `_broadcastLabel` composes `liveNowBroadcasting`/`liveSessionLabel`
+ `session.localizedHall`; the hall comes from the wire (D-433) and is omitted
when null.

### E2E-MOB025-013 — Session title + speakers/participants bullet line

```gherkin
Scenario: The session title and speakers render as gold bullets
  Given the session title is "مستقبل الأمن البحري" and speakers are "العميد م. الزهراني، النقيب الحربي"
  When the screen renders in Arabic
  Then a gold bullet shows the title "مستقبل الأمن البحري"
  And a second beige bullet shows the speakers line "العميد م. الزهراني، النقيب الحربي"
  And both lines are right-aligned with a leading "·" dot

Scenario: A session with no speakers omits the speakers bullet
  Given the session carries a title but no speakers
  Then only the title bullet is shown and no speakers line appears
```

**Evidence:** `_GoldBullet` for `session.localizedTitle` (always) and
`session.localizedSpeakers` (only when non-null) — frame nodes 934:3616 / 934:3617.

### E2E-MOB025-014 — "الجلسات القادمة" upcoming-session cards

```gherkin
Scenario: Upcoming sessions render as cards with a gold time chip
  Given the agenda has upcoming sessions ["كلمة الافتتاح" at 10:00, "حلقة نقاش: الردع البحري" at 11:30]
  And those are read (non-blocking) after the live session loads
  When the screen renders in Arabic
  Then a section header reads "الجلسات القادمة"
  And a card shows "كلمة الافتتاح" with a gold "10:00" chip at the inline-end
  And a card shows "حلقة نقاش: الردع البحري" with a gold "11:30" chip
  And the current session is excluded from the upcoming list

Scenario: The English header reads "Upcoming sessions"
  Given the device locale is English
  Then the section header reads "Upcoming sessions"
```

**Evidence:** `_loadUpcoming` (excludeSessionId = current), `_UpcomingCard` +
`_TimeChip` (local HH:mm, ltr) — frame nodes 934:3621 / 934:3628 / 934:3630.

### E2E-MOB025-015 — Upcoming strip is optional chrome

```gherkin
Scenario: No upcoming sessions hides the strip
  Given the upcoming-sessions read returns an empty list
  Then the "الجلسات القادمة" header and its cards are not rendered
  And the rest of the live screen renders unchanged

Scenario: An upcoming-sessions read failure does not break the screen
  Given the live session read succeeds
  And the upcoming-sessions read fails with an ApiFailure
  Then the failure is swallowed, no error surface is shown
  And the player, header, notice card and ask-question button still render
```

**Evidence:** `_loadUpcoming` runs unawaited after the main read and catches
`ApiFailure` silently; the strip renders only when `_upcoming.isNotEmpty`.

### E2E-MOB025-016 — Gold region-restriction notice card

```gherkin
Scenario: The region notice card renders bold label + body
  Given any session detail has loaded
  When the screen renders in Arabic
  Then a solid gold card shows a bold "إشعار:" label followed by
       "البث المباشر متاح داخل منطقة الرياض فقط حسب لوائح التنظيم."

Scenario: English region notice
  Given the device locale is English
  Then the card reads "Notice:" followed by
       "Live broadcasting is available only inside the Riyadh region per the …"
```

**Evidence:** `_RegionNoticeCard` (`liveRegionNoticeLabel` + `liveRegionNoticeBody`)
— frame node 934:3619. Static notice; shown on every loaded state.

### E2E-MOB025-017 — Ask-a-question entry → send-question

```gherkin
Scenario: Ask a question navigates to the Q&A page with the session id
  Given a session with id "S-104" has loaded
  When the user taps the gold "اطرح سؤالاً" button
  Then the app pushes /live/question with queryParameters sessionId=S-104

Scenario: The button label is bilingual
  Given the device locale is English
  Then the button reads "Ask a question"
```

**Evidence:** `_AskQuestionButton` → `_askQuestion` →
`context.pushNamed(RouteNames.sendQuestion, queryParameters: {sessionId})`;
label `liveAskQuestion` (اطرح سؤالاً / Ask a question) — frame L-3 Q&A affordance.

### E2E-MOB025-018 / 019 / 020 — AI live captions (P5 — D-439)

```gherkin
Scenario: A session with admin-set caption text shows it in the strip
  Given a live session whose LiveCaptions = "Welcome to the opening session."
  And the device locale is English
  When the live screen loads
  Then the gold-bordered caption strip shows "Welcome to the opening session."
  And the text is rendered in the white surface colour (not the muted hint)
  And the placeholder hint "Live captions of the spoken word appear here…" is NOT shown

Scenario: A live session with no caption shows the placeholder hint
  Given a live session with a stream URL but no LiveCaptions text
  When the live screen loads
  Then the caption strip shows the muted placeholder "Live captions of the spoken word appear here…"

Scenario: The caption follows the active locale (fallback to the other side)
  Given a live session with LiveCaptions = "English caption." and LiveCaptionsArabic = "الترجمة العربية."
  And the device locale is Arabic
  When the live screen loads
  Then the caption strip shows "الترجمة العربية."
  And it does NOT show "English caption."
```

**Evidence:** `LiveSession.localizedCaption(isArabic)` (active-locale value, falls
back to the other when blank, null when both empty) → `_PlayerSurface(caption:)`
→ `_CaptionStrip` renders `caption ?? hint`, white when a real caption is present
and the frame's soft caption colour (`captionText` = #DDE4F0) for the placeholder.
The strip is built only on the live-feed branch (`mainUrl != null`). Provider
stubbed — the text is an admin-set field on the session (manual entry for the POC).
Frame node 934:3613. Widget tests: `live_broadcast_screen_test.dart` (`P5 — …` cases).

---

_Last reviewed:_ `2026-06-19` by `SIMF Team`.
