# Page 016 — Design (الأجندة · Sessions agenda)

Flutter screen design. **As built (D-378, commit `8a0387f`):** rebuilt to the
KSA-Project Figma frame **215:767 "Calander"** on the shared `KsaPage` shell
(`lib/app/widgets/ksa_shell.dart`). RTL, Arabic-primary. Source:
`lib/features/sessions/sessions_screen.dart` (the old mockup-era screen is
parked in `lib/features/_legacy_mockup/`).

Last updated: 2026-06-13 — KSA Wave-2 redesign (D-378).

> **Rename history (D-271 → D-378):** D-271 renamed the screen الأجندة →
> الجلسات. The D-378 KSA rebuild re-titles the visible header + bottom-nav
> label back to **الأجندة / Agenda** (`l10n.navAgenda`) and gives the pills the
> frame copy **أجندة الفعالية / الأجندة القادمة**. Behaviour (fetch-once +
> client-side filters) is unchanged; the API route stays
> `/app/programme/sessions`; the Flutter route stays `/sessions`.

## Layout (top → bottom, as built)
1. **Shell header** (`KsaPage`) — back chevron (`ksaBackOrHome`: pop, else
   home) + title **الأجندة / Agenda** (`l10n.navAgenda`), over the decorative
   rotated sweep (`showSweep: true`).
2. **Search field** (frame node 218:722) — a bordered `TextField`:
   `navyDeep` fill, beige hairline border (`beigeBorder`, 0.5; gold `accent`
   when focused), white text, hint **البحث / Search**
   (`l10n.sessionsSearchHint`), trailing (suffix) search icon. Filters the
   list **per keystroke, client-side**.
3. **View pills row** (frame node 218:723) — a `navyDeep` container holding
   two equal-width 48px pills, in row order
   **[أجندة الفعالية / Event agenda | الأجندة القادمة / Upcoming agenda]**
   (`l10n.sessionsViewForum` / `l10n.sessionsViewUpcoming`). The **active**
   pill is solid **gold** (`accent` fill + border); the inactive one is a
   bordered `navyDeep` card. **Default view = Upcoming.** Client-side switch
   (L-1) — no refetch.
4. **Day strip** (frame node 218:844) — a **white** rounded container,
   horizontally scrollable, one cell per **programme day** (data-driven from
   the cached sessions — `sessionDays`, L-1a; the strip is hidden when there
   are no days). Each cell = a 3-letter **English** weekday (MON…SUN, LTR, as
   in the frame) over the day number. The **selected** cell inverts to
   **navy** with white text; **Fri/Sat** weekday labels render **red**
   (`danger`) when unselected. **Re-tapping the selected day clears the
   filter** — the frame has no "all days" pill.
5. **Section header** — **المواعيد / Schedule**
   (`l10n.sessionsScheduleSection`, `KsaSectionHeader`).
6. **Session list** — a vertical `ListView` of `KsaCard` rows (frame node
   218:845), each:
   - inline-start: a **two-line bordered time chip** (frame node 221:718,
     52×48, `KsaCard` chrome) — `hh:mm` over `AM`/`PM` (12-hour,
     device-local start time, forced **LTR**), white bold text;
   - centre: the **gold** (`accent`) title prefixed by a **zero-padded row
     index** (`01`, `02`, …, a client sequence over the filtered list), then
     the **grey** (`beigeBorder`) description (2-line ellipsis; hidden when
     null);
   - inline-end: a forward chevron (`Icons.chevron_left` — points "forward"
     in RTL).
   There is **no** active/next-session highlight, no theme-colour tint, no
   category chip and no speaker mini-rows on the list row (the frame row is
   time/number/title/description only).
7. **Bottom nav** — the KSA five-slot bar (`SimfBottomNav`): Home ·
   **الأجندة / Agenda (active — `SimfTab.sessions`, label `l10n.navAgenda`)** ·
   gold QR centre action (badge) · Map · Profile.

## Data binding
- Bind the list to the **cached programme** (one
  `GET /app/programme/sessions` fetch in `initState`). The search / pills /
  day strip mutate a **client-side filtered view** of the cache
  (`filterSessions`) — no refetch.
- Row: `time = startUtc` rendered device-local (`startLocal`, `hh:mm` + `a`),
  `index` = the 1-based position in the **filtered** list (zero-padded),
  `title = localizedTitle` (AR/EN with cross-language fallback),
  `description = localizedDescription`.
- The decoded item also carries `categoryName*`, `primaryThemeColor`, `status`
  and the `speakers[]` cards (incl. the D-271 country flag + photo) — **none
  of these render on the list row**; they ride the cache so the session
  detail (Page_017) can preview without an extra fetch.
- Tapping a row → **Session detail (17)** via
  `pushNamed(RouteNames.sessionDetail, sessionId)`.

## States
- **Loading** — a centred `CircularProgressIndicator` while the fetch runs.
- **Empty** — `KsaEmptyState` (event-busy icon) with **لا توجد جلسات / No
  sessions** (`l10n.sessionsEmpty`) — shown for an empty programme **and**
  for a pill/day/search combination with no matches.
- **Error** — the fetch failed (`ApiFailure`) → `KsaErrorState` with
  **تعذّر تحميل الجلسات. / Could not load the sessions.**
  (`l10n.sessionsError`) and a gold **إعادة المحاولة / Retry** button that
  **re-runs the fetch**.
- The cache is **in-memory for the screen's lifetime** (fetched per visit);
  there is no persisted offline store — a cold offline open lands on the
  error + retry state.

## RTL / localization
- Whole screen mirrored RTL (directional paddings; the chevron is
  `Icons.chevron_left`, i.e. "forward" in RTL).
- Title, hall, description and category use the paired AR/EN fields per
  active locale with a cross-language blank fallback.
- The **time chip** is forced **LTR** (`hh:mm` over `AM`/`PM`) and the day
  strip's weekday labels are **3-letter English** (MON…SUN) in both locales —
  exactly as the frame draws them. Times render in the device tz.
- Header + nav label **الأجندة / Agenda**, pills **أجندة الفعالية /
  الأجندة القادمة**, section **المواعيد / Schedule**, hint **البحث / Search**
  follow the active locale (D-378 strings in `app_l10n.dart`).
