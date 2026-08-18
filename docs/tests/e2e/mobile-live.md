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
> surface carrying the LIVE badge + the gold-bordered organiser caption strip,
> the "يُبث الآن · {hall}" now-broadcasting block (session title + speakers as
> gold bullets), the ask-a-question entry,
> and the "الجلسات القادمة" upcoming-session cards (loaded non-blocking from the
> agenda list). The hall name, speakers line and upcoming cards render only when
> the wire carries them; this screen never fabricates the missing rows.
>
> **Organiser captions (P5 — D-439; re-labelled by A15):** the session detail
> carries optional bilingual `LiveCaptions` / `LiveCaptionsArabic` text — a
> STATIC admin-typed field that never changes during the broadcast. When present
> the gold-bordered caption strip shows the active-locale text in white; when
> blank it shows the muted placeholder hint (and YouTube CC supplies captions for
> a YouTube feed). **A15 (2026-07-26)** removed the strip's gold "AI" chip and
> its "live translation of the spoken word" placeholder: the app has no
> speech-to-text and no streaming translation, so both were a false capability
> claim. See also **A20** — the geographic-restriction notice is gone.
> The strip renders only on the live-feed branch (no stray strip on a
> recorded/not-live session). Same change fixed a pre-existing bug where editing a
> session via the admin PUT silently wiped its live feed URLs (regression test
> `Update_round_trips_all_live_fields`).
>
> **Live notice (FR-702 — owner decision 2026-07-31, D-815):** the session detail
> also carries optional bilingual `LiveNotice` / `LiveNoticeArabic` free text
> (≤512 per language) written per session in the Control Panel. When set, the app
> renders it as a calm informational banner **above** the player; when both sides
> are blank it renders nothing. **It restricts nothing.** SIMF-FDS-007 §5.1 used
> to specify FR-702 as a Riyadh-region restriction that showed a notice *instead
> of* the stream; the owner reversed that, so the feed plays for every viewer, no
> code reads a viewer's location, and the notice is shown *with* the stream. A20
> below removed the old hard-coded region card; this is what took its place.

| | |
|--|--|
| **Page** | [`Page_025`](../../App/Page_025/README.md) |
| **Route** | `GET /api/v1/app/programme/sessions/{id}` · app screen #25 `/live?sessionId=` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Signed-in for the screen (D-577)** — the live screen is login-only: a guest sees an in-screen "need login" prompt + a Sign-in button, never the player. The read endpoint stays `AllowAnonymous` (the app gates the screen, not the API); use an approved Visitor token to reach the player. |
| **Last reviewed** | 2026-07-31 (FR-702 live notice — informational, shown with the stream; D-815) |

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
| E2E-MOB025-011 | Figma 934:3450 re-skin — navy header "البث المباشر", black player surface, LIVE badge, organiser caption strip (A15: no "AI" chip) | i18n/layout | P0 | _to author_ |
| E2E-MOB025-012 | Header line "يُبث الآن · {hall}" (now-broadcasting + hall name) (D-433) | happy | P0 | _to author_ |
| E2E-MOB025-013 | Session title + speakers/participants gold bullet line (D-433) | happy | P1 | _to author_ |
| E2E-MOB025-014 | "الجلسات القادمة" upcoming-session cards: title + gold HH:mm chip (D-433) | happy | P1 | _to author_ |
| E2E-MOB025-015 | Upcoming list empty / fails → strip hidden, live screen still works (D-433) | resilience | P1 | _to author_ |
| E2E-MOB025-016 | **A20 —** NO geographic-restriction notice is shown on any live path (the "Riyadh region only" card is removed; nothing checks the viewer's location) | edge | P1 | authored ✓ (screen `A20 — no geographic restriction notice is shown to any viewer`) |
| E2E-MOB025-017 | Ask-a-question entry "اطرح سؤالاً" → /live/question with sessionId (D-433) | happy | P0 | _to author_ |
| E2E-MOB025-018 | P5 — session with AI caption text → the strip shows the text (white), not the placeholder hint (D-439) | happy | P1 | authored ✓ (screen `P5 — a session with caption text shows it in the caption strip (white)`) |
| E2E-MOB025-019 | P5 — live session with no caption → the muted placeholder hint (D-439) | edge | P1 | authored ✓ (screen `P5 — a live session with no caption shows the placeholder hint`) |
| E2E-MOB025-025 | **A15 —** the caption strip carries no "AI" chip and no live-translation promise; the placeholder names the organiser as the author | edge | P0 | authored ✓ (screen `A15 — the caption strip has no AI chip and no live-translation promise`) |
| E2E-MOB025-020 | P5 — caption locale fallback: Arabic text under `ar`, English under `en` (D-439) | i18n | P1 | authored ✓ (screen `P5 — the caption renders the Arabic text under the ar locale`) |
| E2E-MOB025-021 | Login-gate (D-577): a signed-out guest sees the in-screen "need login" prompt + Sign-in button (never the player), and no session is fetched | auth | P0 | authored ✓ (screen `a signed-out guest sees the need-login gate, not the stream (owner, D-577)`) |
| E2E-MOB025-022 | **Rate-on-live-close (item 8 / D-712, FDS-007 §C.4 GAP-B):** an approved attendee leaving the live screen for a session that carried a live feed opens `/rate?code=Session&targetId={id}` **once**; re-entering + leaving does not re-prompt (shared dedup with the D-690 after-view prompt). A non-live session and a signed-out guest are never prompted | happy | P0 | authored ✓ (screen `D-712 — leaving a watched live session opens the rate screen once` + `… non-live session … does not prompt` + `… guest is never prompted`) |
| E2E-MOB025-023 | **Fullscreen (owner item 14 / D-721):** the YouTube player shows a fullscreen button; entering fullscreen rotates to landscape, exiting restores portrait — a deliberate, owner-approved exception to the app-wide portrait lock. YouTube only; the HLS/MP4 fallback keeps its play-only control | happy | P1 | authored ✓ (unit `live_video_player_test.dart` orientation helper; real fullscreen playback is manual/device) |
| E2E-MOB025-026 | **FR-702 live notice (owner 2026-07-31 / D-815):** a session carrying `liveNotice` renders the informational banner ABOVE the player **and** the player still mounts — the notice is shown WITH the stream, never instead of it | happy | P0 | authored ✓ (screen `FR-702 — a session notice renders as the informational banner and the player still mounts`) |
| E2E-MOB025-027 | **FR-702 locale + fallback:** the banner shows `liveNoticeArabic` under `ar` and `liveNotice` under `en`; when only one language is authored, both locales read that side | i18n | P1 | authored ✓ (screen `FR-702 — the banner renders the Arabic notice under the ar locale` + repo `LiveSession.fromJson liveNotice (FR-702)`) |
| E2E-MOB025-028 | **FR-702 no notice / cleared notice:** a session with no notice — or one an admin has emptied, so both sides are null/whitespace — renders no banner and no reserved space; the player is unaffected | edge | P0 | authored ✓ (screen `FR-702 — a blank notice renders nothing (no empty banner)` + repo `a missing / blank notice is null (the banner is not rendered)`) |
| E2E-MOB025-024 | **Watch keep-alive (owner item 13 / D-726; moved into the shared player by item 27):** a signed-in viewer watching the stream (no touch) is kept active by a 60s keep-alive so the SessionGuard silently refreshes instead of showing the idle countdown; still bounded by the 24h cap; leaving cancels it | happy | P1 | authored ✓ (guard behaviour in `session_guard_test.dart`; keep-alive on the shared player in `live_video_player_test.dart`; multi-minute watch is device) |
| E2E-MOB025-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB025-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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
  And the player, header and ask-question button still render
```

**Evidence:** `_loadUpcoming` runs unawaited after the main read and catches
`ApiFailure` silently; the strip renders only when `_upcoming.isNotEmpty`.

### E2E-MOB025-016 — A20: no geographic-restriction notice

```gherkin
Scenario: No region-restriction claim on a live session
  Given a session with a live feed has loaded
  When the screen renders in Arabic or English
  Then no card claims the broadcast is limited to the Riyadh region
  And the strings "منطقة الرياض" / "Riyadh region" / "Notice:" are absent

Scenario: No region-restriction claim on the global main-live
  Given the screen opened with no sessionId and an organisation live URL
  Then no region-restriction card is rendered
```

**Evidence:** A20 (2026-07-26) — the app, API, CP and Website never read the
viewer's location, so the old gold "available only inside the Riyadh region per
the organising regulations" card (frame node 934:3619) was an unconditional
false claim. `RegionNoticeCard` and both l10n strings are deleted. **The product
decision A20 deferred was taken on 2026-07-31 (D-815): there is no geo-fence and
there never will be one** — FR-702 is an informational notice instead, covered by
E2E-MOB025-026..028 below. This scenario therefore stands permanently: the
absence of a region claim is the specified behaviour, not a temporary state.

### E2E-MOB025-026 / 027 / 028 — FR-702: the live notice is shown WITH the stream (D-815)

```gherkin
Feature: Live broadcast — the session's informational live notice (FR-702)
  As an attendee watching a session
  I want to read whatever the organisers want me to know about this broadcast
  So that I am informed — without ever being blocked from watching

Background:
  Given the Control Panel authored session "S-104" with a live YouTube feed
  # Nothing in the app, the API, the CP or the Website reads the viewer's
  # location. There is no region check to set up and none to assert against.

Scenario: A session with a notice shows it above the player, and still plays
  Given session "S-104" has liveNotice "This broadcast is provided by the forum organisers."
  And the device locale is English
  When the attendee opens /live?sessionId=S-104
  Then an informational banner above the player reads
       "This broadcast is provided by the forum organisers."
  And the banner is calm chrome — the shared navy card + muted note style, not an
       error, warning or blocking surface
  And the player surface is mounted below it and the stream plays as normal
  And no copy anywhere claims the broadcast is limited to a region

Scenario: The banner follows the active locale
  Given session "S-104" has liveNotice "English notice."
       and liveNoticeArabic "يقدَّم هذا البث من منظمي الملتقى."
  And the device locale is Arabic
  When the attendee opens /live?sessionId=S-104
  Then the banner reads "يقدَّم هذا البث من منظمي الملتقى."
  And "English notice." is not shown
  And the player surface is still mounted

Scenario: One language only falls back to the authored side
  Given session "S-104" has liveNoticeArabic "إشعار" and no English notice
  And the device locale is English
  Then the banner reads "إشعار"

Scenario: No notice renders nothing at all
  Given session "S-104" has liveNotice "   " and liveNoticeArabic ""
  When the attendee opens /live?sessionId=S-104
  Then no notice banner is rendered — no empty card and no reserved space
  And the player surface is mounted exactly as it is on a session that never had
       a notice

Scenario: Clearing the notice in the Control Panel takes the banner down
  Given session "S-104" is showing its notice on the live screen
  When an admin empties both notice inputs at /admin/sessions and saves
  And the attendee pulls to refresh the live screen
  Then the banner is gone
  And the stream is unchanged — it was never affected by the notice either way
```

> **This is a notification, not a gate — that is the whole point of the
> scenario.** SIMF-FDS-007 §5.1 originally read FR-702 as "the live stream is
> available only within the Riyadh region… an attendee outside the region sees
> the restriction notice **instead of** the stream". The owner reversed that on
> 2026-07-31 (D-815). Every scenario above therefore asserts the player is
> **present** alongside the banner: a run that shows the notice while the player
> is missing is a FAILURE, not a pass.

**Evidence:** `LiveSession.liveNotice` / `.liveNoticeArabic` decoded from the
shipped `GET /app/programme/sessions/{id}` payload (`liveNotice`,
`liveNoticeArabic`); `LiveSession.localizedNotice(isArabic)` picks the active
locale, falls back to the other and returns null when both are blank;
`LiveNoticeBanner` (`widgets/live_notice_banner.dart`) renders `SimfPageNote` on a
`SimfCard` and is emitted only when the notice is non-null. Screen tests
`FR-702 — a session notice renders as the informational banner and the player
still mounts`, `FR-702 — the banner renders the Arabic notice under the ar
locale`, `FR-702 — a blank notice renders nothing (no empty banner)` (each also
asserting `LivePlayerSurface` is mounted); decode tests
`live_repository_test.dart` → `LiveSession.fromJson liveNotice (FR-702)`. The
CP-side authoring + clearing is `cp-admin-sessions.md` E2E-SES-054..056.

### E2E-MOB025-025 — A15: the caption strip is an organiser note, not live AI translation

```gherkin
Scenario: No AI branding on the caption strip
  Given a live session has loaded
  Then the caption strip shows no gold "AI" chip
  And no copy promises live translation of the spoken word

Scenario: The placeholder names who writes the caption
  Given a live session with no admin-typed caption
  Then the strip reads "Caption text written by the organiser for this session
       appears here." (EN) / "يظهر هنا النص التوضيحي الذي يكتبه المنظّم لهذه
       الجلسة." (AR)
```

**Evidence:** A15 (2026-07-26) — the strip renders the static admin-typed
`Session.LiveCaptions` string, which never changes during a broadcast. Real
speech-to-text + streaming translation does not exist in the app (see the dead
`/app/ai/live-translation/chunk` endpoint, B4), so the AI chip and the
live-translation placeholder were a false capability claim.

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

### E2E-MOB025-023 — Fullscreen button, landscape while fullscreen (owner item 14 / D-721)

```gherkin
Scenario: The live YouTube feed can go fullscreen in landscape
  Given a live session with a YouTube liveStreamUrl
  When the approved viewer taps the player's fullscreen button
  Then the player enters fullscreen and the device rotates to landscape
  When they exit fullscreen
  Then the player returns inline and the device restores portrait

Scenario: The HLS/MP4 fallback keeps its play-only control
  Given a live session with a non-YouTube (HLS/MP4) liveStreamUrl
  Then the fallback video shows only its play/pause control (no fullscreen button)
```

> Portrait is the app-wide lock (`main.dart`); the live video is the one
> owner-approved exception (D-721). YouTube is the POC provider (D-349), so only
> that path gets the button (`YoutubePlayerParams(showFullscreenButton: true)` +
> `setFullScreenListener` toggling `SystemChrome` to landscape/portrait).

**Evidence:** unit test `live_video_player_test.dart` — `liveFullScreenOrientations`
(landscape in / portrait out, never left in landscape on exit). Real fullscreen
playback is **manual** (a device is needed — the IFrame webview has no headless
platform channel).

### E2E-MOB025-024 — Watch keep-alive: watching the stream does not sign you out (owner item 13 / D-726)

```gherkin
Scenario: A signed-in viewer watching the live stream stays signed in
  Given an approved viewer is on the live broadcast screen watching a feed
  And they do not touch the screen for several minutes (only watching)
  Then the app-wide SessionGuard treats them as active (the screen pings a
    60-second keep-alive) and silently refreshes the access token
  And no idle "stay signed in / sign out" countdown appears
  But the server's 24-hour absolute session cap (D-443) still applies — past it
    the refresh fails and the viewer is signed out to /sign-in

Scenario: Leaving the live screen stops the keep-alive
  When the viewer leaves the live broadcast screen
  Then the 60-second keep-alive timer is cancelled (dispose), so an idle app
    resumes the normal SessionGuard idle-timeout behaviour
```

> The keep-alive pings the shared `SessionActivity` clock; the app-wide
> `SessionGuard` (D-726) reads it. It suppresses only the idle countdown — it can
> never extend a session past the server 24h cap.
>
> **Item #27 (2026-07-22) — heartbeat moved into the shared player.** The
> keep-alive now lives INSIDE the shared `LiveVideoPlayer`
> (`widgets/live_video_player.dart`), NOT on `LivePlayerSurface` / the live
> screen. This covers every surface that shows the player — the live screen here
> AND the AI-summary recording / summary-video cards, which use `LiveVideoPlayer`
> directly (bypassing `LivePlayerSurface`), so a long recording there no longer
> trips the idle timeout either (see `mobile-ai-summary.md` E2E-MOB034-011). On
> init the player marks the session active and a 60s `Timer.periodic` re-marks;
> `dispose` cancels it.

**Evidence:** `session_guard_test.dart` (the guard's active→silent-refresh vs
idle→countdown behaviour) + `live_video_player_test.dart` (the shared player's
mount-marks / 60s-tick-re-marks / dispose-cancels keep-alive). The true
multi-minute watch behaviour is a device check.

---

_Last reviewed:_ `2026-07-10` by `SIMF Team` — **#7 (D-733): the "Ask a question"
entry is now LIVE-ONLY — shown only while the session is actually broadcasting (a
live feed is up); it is HIDDEN on the post-session recording view (a YouTube
archive is not a live broadcast, so no asking once the session is done). Widget
tests: not-live/recording hide the ask; the live-with-ask render is locked by the
live-broadcast golden.** _Prior:_ `2026-07-31` — **FR-702 settled by the owner
(D-815): the live notice is a NOTIFICATION shown with the stream, not a
restriction.** E2E-MOB025-026..028 added (notice present + player still mounted,
Arabic/fallback, blank/cleared → no banner); E2E-MOB025-016 (A20) re-stated as
permanent rather than pending a geo-fencing decision. _Prior:_ `2026-07-09` — D-726 added the watch keep-alive
(E2E-MOB025-024, owner item 13); D-721 added the fullscreen button
(E2E-MOB025-023, owner item 14); D-712 added the rate-on-live-close prompt
(E2E-MOB025-022, FDS-007 §C.4 GAP-B, owner item 8).
