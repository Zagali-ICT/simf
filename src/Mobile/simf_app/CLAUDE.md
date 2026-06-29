# CLAUDE.md — SIMF Flutter App · Engineering Standards

> **Precedence.** This file is **SUBORDINATE** to `~/.claude/CLAUDE.md` (the global
> rule set §0–§20) and to `d:/SIMF/System/V1.0.0/CLAUDE.md` (the SIMF project rules:
> controlled docs, D-110 freeze, Data/Identity DB split, permission system, E2E
> catalogue, change Definition-of-Done). Where any of them conflict, **the higher
> layer wins.** This file only adds Flutter-app clean-code specifics; it never
> overrides an approval gate, a freeze, or a security rule.

The coding constitution for this repo. Claude Code reads this every session.
Read it fully before editing. This is a mature codebase (~166 Dart files,
~46.9k lines). Refine it; do not re-architect it. When in doubt, match the
existing pattern in the file you're editing, and FLAG instead of guessing.

Stack: Flutter · Riverpod · go_router · `simf_data_pkg` (data) · `AppL10n`
(ar/en, RTL-first) · `SimfTokens` (design tokens). Arabic is the primary
language; every screen must be correct in RTL. The app is **portrait-locked**
(`main.dart`) — "responsive" here means flexible within portrait (phones +
tablets), not landscape/two-pane.

---

## 0. Preserve these — changing them is a regression
- Riverpod is the ONLY state management. Never add Provider/Bloc/GetX.
- UI never calls the network. Data goes through `data/` repositories and
  `simf_data_pkg`. No `http`/`dio`/API calls in any widget or `*_screen.dart`.
- Doc comments that record a design decision, a Figma node, or a backend
  contract (e.g. "D-368", node ids) stay. Never delete them.
- `AppL10n` is the localization system. Do not migrate to gen_l10n/.arb unless
  asked. Every user-facing string goes through `AppL10n` — no raw string
  literals in widgets.
- `SimfTokens` (`app/theme/tokens.dart`) is the single source of truth for
  color, typography, spacing, radius. Use it.
- **Wire contract (D-219) is frozen.** The JSON field names/types in
  `features/*/data` models — every `fromJson`/`toJson` key the deployed app
  decodes against the live API — must NOT change. Reshape widgets/providers
  freely, but a renamed serialized key silently breaks the shipped app. Treat
  "wire-contract diff = empty" as a hard rule for any data-touching screen.

If a change would touch any of the above, STOP and flag it.

---

## 1. File & folder structure

Feature-first. Every feature follows the SAME shape:

```
lib/
├── app/                      # app-level: router, theme, l10n, app shell
│   ├── theme/                # tokens.dart, app_theme.dart  (Figma source of truth)
│   ├── localization/         # AppL10n
│   ├── widgets/              # app-wide shared widgets (shell, nav, logo) — the Simf* catalogue
│   └── router.dart, route_names.dart
├── core/                     # cross-feature utilities, no single-feature logic
│   ├── widgets/              # shared primitives (buttons, fields, empty/error states)
│   ├── responsive/           # breakpoints, ResponsiveLayout, max-width anchor
│   ├── net/, env/, sharing/  # infra
│   └── utils/
└── features/<feature>/
    ├── data/                 # models + repositories (already the convention)
    ├── widgets/              # feature-local widgets (one widget per file)
    └── <name>_screen.dart    # screens live at the feature root (existing convention)
```
Tests mirror this under `test/` (e.g. `test/features/auth/sign_in_screen_test.dart`).

Rules:
- **One public widget per file.** A `_Private` helper widget may share the
  screen file only if it is <60 lines and used once; otherwise its own file in
  `widgets/`.
- File names: `snake_case.dart`. Types: `PascalCase`. Screens end in `_screen`.
- **Names must describe the real thing** (see §13.1). No placeholder/legacy
  prefixes (`Ksa*`, `Page_NNN`, generic `temp`/`demo`).
- No file over ~400 lines. No `build()` over ~50 lines. These are guidelines —
  don't shred a cohesive file to hit a number, but the 500–2245 line screens in
  this repo all violate the intent and must be split.
- Every feature you touch gets normalized to the shape above (create `widgets/`
  when extracting). Don't do a repo-wide move in one pass — only the feature
  you're working in.

---

## 2. Component & widget design

- **Single responsibility.** A screen composes; it does not also define 15
  widgets and 5 business rules. Extract sub-widgets to `features/<f>/widgets/`,
  truly generic ones to `core/widgets/`.
- **Build shared components, don't copy.** Before writing a button, field,
  card, dialog, empty-state, or error-state, search the shared catalogue in
  `lib/app/widgets/` (the `Simf*` shell — `SimfPageShell`, `SimfCard`,
  `SimfListRow`, `SimfErrorState`, `SimfEmptyState`, `SimfPullToRefresh`, …) and
  `core/widgets/`. If it doesn't exist and is needed twice, create it there with
  a clear API. (See §7 for the DRY rule.)
- Widgets are **stateless and const wherever valid.** Lift state to a Riverpod
  provider, not into a giant StatefulWidget.
- Constructors: `const`, `super.key`, required named params. No positional
  params for widgets.
- No business logic, formatting, or data shaping inside `build()`. Compute in a
  provider/notifier or a pure helper, pass the result in.

---

## 3. Responsive & adaptive layout (currently missing — add it)

Must look correct on small phones, large phones, and tablets — **in portrait**
(the app is portrait-locked; landscape/two-pane is out of scope unless the lock
is lifted by owner decision).

- **Breakpoints** in `core/responsive/breakpoints.dart`:
  `compact < 600 ≤ medium < 905 ≤ expanded < 1240 ≤ large`.
  Use these names; never hardcode `if (width > 600)` inline.
- **Flexible width, not fixed (see §13.7).** Content blocks (cards/banners/tiles/
  buttons/forms) use `width: double.infinity`/`Expanded`/`Flexible` + token
  margin/padding, and drop `maxWidth:` content caps — but **KEEP intrinsic fixed
  sizes for icons/avatars/badges/QR/sweep/spacers**. The owner is on a tablet.
- **Max-width anchor.** Content must NOT stretch edge-to-edge on wide screens.
  Wrap page bodies in a `MaxWidthBody` (build it in `core/responsive/`) that
  centers and caps content (e.g. `maxWidth: 560` for forms, `840` for reading
  content).
- **Scale with tokens**, not raw numbers. Use `SimfTokens` spacing; no
  `SizedBox(height: 17)`.
- **Text scaling.** Respect `MediaQuery.textScaler`; no fixed-height boxes that
  clip enlarged text. Verify at 1.0 and 1.3 scale.
- **RTL correctness.** `EdgeInsetsDirectional`, `start`/`end`,
  `AlignmentDirectional` — never `left`/`right`. Every screen mirrors correctly.
- **Safe areas & keyboard.** `SafeArea` + handle `viewInsets` on every form.

When refactoring a screen, wrap its body in the responsive anchor and convert
fixed widths / `left`/`right` to directional/adaptive equivalents.

---

## 4. Lists & per-screen performance (currently missing — add it)

- **Lazy by default.** Every scrolling list uses `ListView.builder` /
  `SliverList` / `GridView.builder`. Never `ListView(children: [...])` for
  data-driven or long lists (30 non-builder lists exist — convert them).
- **Pull-to-refresh on every data screen (see §13.6).** Reuse the existing
  `SimfPullToRefresh` + `SimfPullableHost`.
- **Pagination / load-more.** Lists backed by a paginated API load the next
  page on scroll; don't fetch all rows at once.
- **Stable item identity:** `ValueKey`/`PageStorageKey` where reordering or
  scroll restoration matters.
- **Images:** cached + sized (`cacheWidth`/`cacheHeight` or
  `cached_network_image`). Never load full-res into a thumbnail. For bearer/
  self-signed image URLs, fetch bytes via the authenticated Dio client — never a
  raw `Image.network` (D-422).
- **const everywhere valid** — biggest cheap win across this many widgets.
- **`RepaintBoundary`** around expensive isolated subtrees (e.g. live
  broadcast). Don't rebuild static headers on scroll. Scope providers so one
  item change doesn't rebuild the whole list.
- **Per-screen perf budget** recorded in the page doc header (§9): list
  behavior (lazy/paginated), heavy subtrees, anything to watch.

---

## 5. Figma fidelity (the hard requirement)

Every screen must match its Figma node: **strings, font, color, spacing.**
The authoritative node map is **`docs/pages/FIGMA-NODE-MAP.md`** (see §13.5 for
the node list and the ASK-don't-guess rule).

### 5.1 Single source of truth
- All colors, text styles (family/size/weight/line-height/letter-spacing),
  spacing, and radii come from `SimfTokens` — the code mirror of Figma's
  variables/styles.
- **Zero raw `Color(0x…)` in widgets** (~79 exist today — remove all; the count
  excludes `tokens.dart`, which legitimately defines them). If a Figma color
  isn't a token yet, add it to `tokens.dart` with the Figma variable name, then
  use the token. Base palette (from node 922-2824): BG `#192B41`, text
  `#FFFFFF`, gold `#C9A84C`, deep `#01132D`, paragraph `#C2B8A2`.
- **Zero raw `TextStyle(fontSize:…)` in widgets** (~526 exist today — remove
  all). Use a named token style (`SimfTokens.titleM`, `bodyR`, …). The font
  family is set ONCE in the theme — never per-widget.

### 5.2 Per-screen Figma audit (run for each screen)
For the screen's Figma node, produce a diff, then fix it:
1. **Strings:** every visible string matches Figma copy exactly (ar + en) and
   goes through `AppL10n`. List any that differ or are hardcoded.
2. **Typography:** each text element's family/size/weight/line-height equals
   the Figma layer's; mapped to a token style.
3. **Color:** each fill/stroke/text color equals the Figma value via a token.
4. **Spacing/layout:** padding, gaps, radii, component sizes match Figma
   auto-layout values using token spacing.
5. Output the mismatch checklist BEFORE editing; fix; confirm parity after.

### 5.3 Accessing Figma
Use the **Figma MCP server** (connected) to read the actual node values for the
node id in `FIGMA-NODE-MAP.md`. Never invent a color/size — if a Figma value (or
the node itself) is unknown, **STOP and ASK the owner** (§13.5).

---

## 6. State & data
- Screen state → Riverpod provider/notifier in the feature. Widgets read, don't
  own, shared state.
- Data flow: widget → provider → repository (`data/`) → `simf_data_pkg`.
- Surface async as explicit `loading / data / error` via `AsyncValue`. No blank
  screens on error; use the shared `SimfErrorState` / `SimfEmptyState` widgets.

---

## 7. DRY, duplication & dead/unused pages
- **DRY rule:** extract a shared widget/helper on the **second real
  occurrence**, never speculatively. A little duplication beats the wrong
  abstraction. Shared widgets → `core/widgets/` or `app/widgets/`; shared logic
  → `core/utils/`.
- **De-duplicate:** unify near-identical widgets/forms (the several auth/
  sign-up screens, repeated card layouts) into one parameterized component.
- **Dead code:** delete commented-out *code*. Keep doc/intent comments — when
  unsure, leave it and list it for review.
- **Unused pages:** a screen with no route in `router.dart` and no push from
  anywhere is dead. List candidates; **delete only after owner confirmation**
  (dead-page deletion is owner-gated under the D-110 freeze).
- **Unused symbols/imports/assets:** remove (analyzer + `dart fix` help).

---

## 8. Write it like a human, not an AI
- Comment WHY, never WHAT. No comment that restates the next line.
- No `try/catch` unless a failure is genuinely expected and handled (~113 exist
  — don't add reflexive ones).
- No defensive null-checks for values that can't be null.
- No speculative abstraction. Match the pragmatic data+presentation layering;
  do NOT add a `domain/` layer or interfaces "for cleanliness" unless asked.
- Natural, varied naming consistent with existing code.
- No leftover `print`/`debugPrint`, no commented-out code, no orphan `TODO`s.

---

## 9. Document every page
Each screen file starts with a doc header (this repo already does this well —
keep the style):
```
/// <Screen name> — <Arabic title> · route: <route_names const>
/// Purpose: one line.
/// Data: providers/repositories it reads; API endpoints behind them.
/// Figma: node id(s) this implements (from FIGMA-NODE-MAP.md).
/// Perf: list behavior (lazy/paginated), heavy subtrees, budget notes.
/// Contract: anything backend- or design-mandated that must not change.
```
Maintain the **existing** indices — `docs/pages/PAGE-INDEX.md` (route → doc →
test) and the inline Figma-node comment — do NOT introduce a rival
`docs/SCREENS.md`. The full per-page doc lives in the page's folder (§13.2).

---

## 10. Test every page
Under `test/` mirroring the `lib/` path:
- **Widget test:** renders in ar and en; loading→data→error (repository faked);
  key interactions work.
- **Golden test** where layout fidelity matters: compact + expanded, RTL — to
  lock Figma parity and catch responsive regressions. Use the verified harness
  (`test/golden/golden_fonts.dart`, FontLoader Inter/Cairo/MaterialIcons, fixed
  surface size per breakpoint, single host); regen with
  `flutter test --update-goldens`.
- **Provider/unit tests** for non-trivial notifiers and pure helpers
  (`phone_validation`, `plate_validation`, formatting).
- Fake the repository; never hit the network in tests.
- Run `flutter test` **from the `simf_app` package root** before a module is
  done. The pre-existing `simf_auth_pkg` signUp failure is a known baseline — do
  not count it as a regression.

---

## 11. Lint gate (see analysis_options.yaml)
- Adopt `very_good_analysis` at **warning** severity with a recorded baseline; do
  NOT flip rules to `error` globally (see the ADOPTION note in
  analysis_options.yaml — ~445 relative imports + 526 inline styles would flood
  day one and make the gate unsatisfiable).
- The per-module gate is **"zero NEW analyzer issues + zero issues in the touched
  module's files"** — not repo-wide zero, until Phase 6.
- **NEVER run `dart format .`** — this repo's Flutter 3.44 "tall" formatter strips
  the trailing commas `require_trailing_commas` demands and explodes single-line
  collections (e.g. `router.dart`) into huge diffs. Hand-write trailing commas;
  run only `dart fix --apply` (does not reflow) on the files you touched.
- Don't disable a lint to silence a warning; fix the code. A genuinely-wrong rule
  gets a deliberate `// ignore: name — reason`, flagged for review.

---

## 12. Rules of engagement (how to work)
1. **One page/module at a time** (one feature at a time). Never clean the whole
   repo in one diff.
2. **Plan first.** Output plan + file list, wait, then apply file by file
   (honour the global §11 pre-approval format).
3. **Behavior-preserving by default.** If behavior changes, say so and wait.
   Visual change happens only in the Figma level, and only toward the Figma value.
4. Small, reviewable diffs; one-line explanation per significant change.
5. Characterize → compile → analyze → test → live-render after each step;
   commit (targeted pathspec) so it can roll back.
6. If something looks deliberate (contract, design note, workaround), flag it.

---

## 13. SIMF per-page HARD rules (owner additions, 2026-06-29)

Every page you work on must satisfy ALL of these before it is "done". They extend
§1–§12 and feed the per-page Definition of Done in REFACTOR_PLAN.md.

### 13.1 Real naming — no `KSA`/placeholder names
Rename every `Ksa*` symbol to a `Simf*` (shared shell) or descriptive (auth) name
matching the existing `SimfLogo`/`SimfBottomNav`/`SimfTokens` convention. The
shared-shell rename is a **foundation step** (done once, behavior-frozen — see
ROADMAP Phase 0); page-local names are renamed as you reach each page. **When
renaming a page, update atomically in this order:** (a) `RouteNames` const →
(b) `_routes` metadata in `router.dart` → (c) `_screenFor`/`_auxScreenFor`
builder → (d) `_tabRouteNames` + role-gating if applicable → (e) file + class
rename → (f) all `pushNamed`/`goNamed` references → (g) the page's doc folder
name. Rename Dart symbols/classes/files freely; treat route **PATHS** and JSON
keys as contracts (change a path only after checking deep-link/notification
routing).

### 13.2 Per-page doc folder — real-named, full info
When you finish a page, its docs live in one **real-named folder** (rename
`Page_NNN` → the real screen name) holding the page's **spec + API + how-to-use +
testing**, consolidating today's `docs/App/Page_NNN/` + flat
`docs/pages/{surface}/<slug>.md` + `docs/tests/e2e/<surface>-<slug>.md`. Content
mirrors the existing template (header table, purpose, audience/permissions,
screenshots, UI affordances, data flow + endpoints, validation, edge cases,
i18n/RTL, accessibility, E2E scenarios, changelog). Update `PAGE-INDEX.md` +
`docs/tests/e2e/README.md` in the same changeset (project D-133/D-245/D-246).

### 13.3 Freeze-after-done
A page that passes the full per-page DoD is **FINAL** — no rework after. So the
DoD must be fully satisfied (every gate below) before you declare it done.

### 13.4 Shared widgets + stateless, professional code
Use the `lib/app/widgets/` `Simf*` catalogue; never copy a shared widget into a
screen. Widgets stateless + const where valid; state in Riverpod; no business
logic in `build()`. (Reinforces §2.)

### 13.5 Figma pixel-by-pixel parity — ASK, never guess
Source of truth: `docs/pages/FIGMA-NODE-MAP.md` (Figma file
`PSXHhY0UVTAPSaIOf9uNKd` "KSA-Project"; open a node at
`https://www.figma.com/design/PSXHhY0UVTAPSaIOf9uNKd/KSA-Project?node-id=<NODE>`).
Read the bound node via the Figma MCP. **Almost every screen already has a node**
(list below). The "ASK the owner, never guess" rule applies to a screen with **no
mapped node** and to any documented deliberate deviation (don't override it).

Bound nodes: Home signed-in `758-1134` · Home guest `758-2910` · Home highlights
`758-1238/1239` (deliberate deviation — carousel, no golden) · My Area `758-1283`
· Venue Map `758-1358` · Badge QR `758-1469` (= top-nav spec source) · Sponsors
`922-2824` · Booths `922-2458` · Speakers `908-1744` · Speaker profile `908-2110`
· Programme `883-2308` · Session detail `889-2450` · Archive + detail `925-3079` ·
Live `934-3450` · Send question `934-3636` · My sessions `1388-7621 / 1388-9067` ·
Notifications `758-2491` (deviation: VIP star) · Delegations `1426-10771` · Scan
contact `758-4380 / 758-4735` · standard top-nav `758-1469 / 922-2824` · bottom
nav `206-1732`.

**No node yet — STOP and ASK before building/refactoring:** Registration status
(#11) · Share my contact (FDS-014) · My contacts (FDS-014).

**Removed/dissolved — do NOT build as standalone screens:** Media gallery (#30,
embedded in Home) · Audience comments (#28, removed) · Guest mode (#12 = Home
`758-2910`).

**Header cluster (owner 2026-06-28):** sub-pages show back + title + hairline only
(`SimfPageShell.showHeaderActions = false` default); the bell/language/theme/menu
cluster lives on the Home greeting header only. This supersedes the 2026-06-18
"cluster on every page" rule.

### 13.6 Pull-to-refresh on every data-loaded page/list/menu
This is **already implemented repo-wide** (D-520 + D-532; a grep audit of all 49
screens confirmed it, 2026-06-28). So the rule during refactor is **PRESERVE it —
never drop it when restructuring a screen, and VERIFY (don't assume) it still fires
after a split** — and apply it to any genuinely new data screen. Reuse the shared
`SimfPullToRefresh` (ex-`KsaRefresh`) + `SimfPullableHost` (ex-`KsaPullable`, hosts
short empty/error states in a viewport-tall always-scrollable box). Hooks:
StatefulWidget `_load()` → `onRefresh: _load`; Riverpod → `ref.invalidate(provider);
await ref.read(provider.future)`. The refreshable child must use
`AlwaysScrollableScrollPhysics`. **Exception:** `registration_status_screen` is
intentionally NOT wrapped (a gate screen whose explicit "Re-check" button already polls).

### 13.7 Flexible/responsive width (within portrait)
The owner runs on a **tablet** — content sized to the 375px phone frame leaves dead
gutters. **Content blocks** (cards, banners, tiles, buttons, forms) stretch to the
available width via `width: double.infinity` / `Expanded` / `Flexible` + token
margin/padding, and **drop `maxWidth:` content caps** (known offenders: auth/profile
form `maxWidth:400`, sponsor tagline `maxWidth:215`, chatbot bubble `maxWidth:288`).
Grids use `crossAxisCount`/`maxCrossAxisExtent`, not fixed item widths. **KEEP fixed
(do NOT stretch): genuine fixed-size elements — icons, avatars, flag/badge boxes, QR
squares, the decorative sweep, small spacers.** "Remove fixed widths" means
content-sizing widths/caps, NOT intrinsic element sizes. Verify on the tablet (no
dead side gutters; content fills the frame as in Figma). Keep the portrait lock; no
landscape/two-pane. (Reinforces §3.)

### 13.8 Page-by-page, pixel-by-pixel verification — no "fixed without test"
A page is verified only with real evidence: golden equality (pre/post), the page's
E2E scenarios driven, and a live on-device render compared to the Figma node.
Never assert "fixed"/"matches Figma" without showing the comparison. No guessing.
