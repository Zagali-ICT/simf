# CLAUDE.md — SIMF Flutter App · Engineering Standards

> **Precedence.** This file is **SUBORDINATE** to `~/.claude/CLAUDE.md` (the global
> rule set §0–§20) and to `d:/SIMF/System/V1.0.0/CLAUDE.md` (the SIMF project rules:
> controlled docs, D-110 freeze, Data/Identity DB split, permission system, E2E
> catalogue, change Definition-of-Done). Where any of them conflict, **the higher
> layer wins.** This file only adds Flutter-app clean-code specifics; it never
> overrides an approval gate, a freeze, or a security rule.

The coding constitution for this repo. Claude Code reads this every session.
Read it fully before editing. This is a mature codebase (718 Dart files,
68,927 lines under `lib/`, measured 2026-08-18). Refine it; do not
re-architect it. When in doubt, match the existing pattern in the file you're
editing, and FLAG instead of guessing.

> **Date every count in this file, and re-measure before trusting one.** The
> numbers below have all moved at least once, and a stale one reads as current:
> this file claimed 166 files and 526 inline styles long after they were 587
> and 117, and it then carried the 2026-08-13 figures unchanged through a
> clean-code round that moved every one of them. Nothing gates this block —
> the ratchets in `test/repo/` count code, and none of them can see prose — so
> re-measuring it belongs in the SAME changeset as the round, not after.
>
> **Scope, so the next reader reproduces the same numbers:** `lib/` only —
> not `test/`, not `integration_test/`, not the two local packages under
> `packages/`.
>
> Measured 2026-08-18 (re-measured after the 400-line round): Dart files
> **718** · lines **68,927** · raw
> `Color(0x)` outside `tokens.dart` **0** · relative imports **0** · inline
> `TextStyle(` outside `app/theme/` **116**, **none** of which still carries a
> raw numeric size · `ListView(` sites **43** (most are static content pages
> and correct as written) · `catch` sites **73** · files over 400 lines **3**
> · `// ignore:` sites **9** · private widget classes **0** ·
> `flutter analyze` **0 errors, 0 warnings, 0 infos** ("No issues found!") ·
> `tool/conventions` **0** ("No violations found").
>
> Reproduce, from `src/Mobile/simf_app`:
> `find lib -name '*.dart' | wc -l` ·
> `find lib -name '*.dart' -exec cat {} + | wc -l` ·
> `grep -rn 'Color(0x' lib | grep -v app/theme/tokens.dart | wc -l` ·
> `grep -rn 'TextStyle(' lib | grep -v '^lib/app/theme/' | wc -l` ·
> `grep -rnE 'fontSize: *[0-9]' lib | grep -v '^lib/app/theme/' | wc -l` ·
> `grep -rn 'ListView(' lib | wc -l` · `grep -rnw catch lib | wc -l` ·
> `grep -rn '// ignore:' lib | wc -l` ·
> `grep -rn "^import '\.\./\|^import '\./" lib | wc -l` (relative imports) ·
> `grep -rnE "^class _\w+ +extends +(StatelessWidget|StatefulWidget|ConsumerWidget|ConsumerStatefulWidget|HookWidget|HookConsumerWidget)" lib | wc -l`
> (private widget classes — the loose form of this grep counted the 53
> framework-required `_FooState extends ConsumerState` classes the rule
> exempts, and read as 53 violations against a checker reporting none) · `flutter analyze` · and, from `tool/conventions`,
> `dart run bin/simf_conventions.dart`. Files over 400 lines are enumerated by
> `test/repo/feature_shape_test.dart`.

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
- **Data-layer boundary (SIMF-MAA-001 §5/§6/§9.1; D-545).** `simf_data_pkg` holds
  the *transport only* — the one `dio` client, interceptors, `ApiResult`/`ApiFailure`,
  storage — and is the only place that depends on `dio`. Each feature's **repository
  + DTOs stay in `features/<f>/data/`**: the repo calls `SimfApiClient` and owns its
  endpoint path + `fromJson` mapping. Do NOT move repositories/DTOs into the package,
  and never let a feature import `dio`.
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
    ├── data/                 # models + repositories + this feature's providers
    ├── widgets/              # feature-local widgets (one widget per file)
    ├── <helper>.dart         # feature-local pure helpers (see below)
    └── <name>_screen.dart    # screens live at the feature root (existing convention)
```
Tests mirror this under `test/` (e.g. `test/features/auth/sign_in_screen_test.dart`).

**This shape is enforced, not merely described.** `test/repo/feature_shape_test.dart`
fails the build on four rules, and it is the authority — this section is where
you read WHY, not a second copy to keep in step:

1. no top-level public provider declared in a `*_screen.dart`;
2. no widget class declared at a feature ROOT (a `*_screen.dart` is the one
   exception — screens live at the root by convention);
3. no file under `lib/features/` importing `package:dio` or `package:http`;
4. no file under `lib/` over 400 lines.

The first three known-offender lists are **empty** on 2026-08-18, so those
rules hold outright and a first offender reddens the build. The fourth now pins
just **3** files, none of them a screen or a model:
`app/localization/app_l10n.dart` 2730 · `app/theme/tokens.dart` 1437 ·
`app/router.dart` 1236. Each is a single flat table — bilingual strings, design
values, the route list — and splitting one serves the number while making the
code worse, which is what the "don't shred a cohesive file" half of the rule
exists to protect.

**Entries come off those lists as the work lands and are NEVER added.**

Rules:
- **One public widget per file**, with one exception the codebase already
  relies on: a file may hold a **named cohesive group** whose members are
  variations on one idea, and be named for that group —
  `simf_cards.dart`, `simf_states.dart`, `simf_tiles.dart`, `about_cards.dart`,
  `meeting_slot_pickers.dart`. What is NOT allowed is the heterogeneous file:
  `live_content.dart` holding a login prompt, a feed toggle, a bullet glyph, a
  banner, a button and a card shares nothing but a feature, and each of those
  belongs in its own file. The test is whether the file's name describes all of
  its contents (2026-08-14).
  **A PRIVATE widget class is itself a violation — including in a screen's own
  file.** `tool/conventions` SIMF-C3 fires on any `class _Foo extends`
  `StatelessWidget` / `StatefulWidget` / `ConsumerWidget` /
  `ConsumerStatefulWidget` / `HookWidget` / `HookConsumerWidget`, whatever its
  length, so an extracted widget is **public**, takes `super.key`, and gets its
  own file under `widgets/`. A framework-required `_FooState extends
  State<Foo>` is not a widget class and is explicitly exempt; only NON-widget
  helpers may be privatized. This replaces the old "a `_Private` helper widget
  may share the screen file if it is <60 lines and used once" allowance, which
  the checker had never honoured — a documented allowance the gate rejects is
  worse than no allowance. `lib/` holds **0** private widget classes on
  2026-08-18, so the rule holds outright.
- File names: `snake_case.dart`. Types: `PascalCase`. Screens end in `_screen`.
- **Names must describe the real thing** (see §13.1). No placeholder/legacy
  prefixes (`Ksa*`, `Page_NNN`, generic `temp`/`demo`).
- No file over ~400 lines. No `build()` over ~50 lines. Don't shred a cohesive
  file to hit a number — but this one is gated, not advisory (the ratchet
  above). **3** files are over on 2026-08-18 and none is a screen:
  `sign_up_visitor_screen` finished at **398** (from 2245 at the start of the
  programme) and `register_visitor_screen` at **397** (from 1268). Both got
  there by moving the NON-widget half out — load, apply-profile, submit
  assembly, validators, pickers, upload — rather than by fighting the parameter
  count of the one `_build*` method left, which is what two earlier attempts
  tried and abandoned. Neither has been verified on a device: D-666 is this
  repo's banked case of a green golden missing a face-capture regression, so
  the sign-up and walk-in flows still owe a real device run.
- **A feature-local pure helper lives at the feature root**, as a small
  purpose-named file: `home_greeting.dart`, `youtube_url.dart`,
  `speaker_initials.dart`, `entity_detail_helpers.dart`. It holds functions and
  constants, never a widget and never a provider. The shape above once named
  only `data/`, `widgets/` and `<name>_screen.dart`, which left these files
  looking like violations when they are the established pattern; recording them
  here beat moving them. There are **11** on 2026-08-18
  (`find lib/features -mindepth 2 -maxdepth 2 -name '*.dart' ! -name
  '*_screen.dart'`), and the distinction is now mechanical rather than a
  reading of this paragraph: `feature_shape_test.dart` lets a pure helper sit at
  the root and fails the build on a *widget* there, which belongs in
  `widgets/`.
- **A provider belongs in `data/`, never in a `*_screen.dart`.** A provider
  declared in a screen forces any other feature that needs it to import a
  screen. Put it beside the repository that feeds it. Private (`_`-prefixed)
  providers used by one screen may stay in that screen.
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

## 3. Responsive & adaptive layout

Must look correct on small phones, large phones, and tablets — **in portrait**
(the app is portrait-locked; landscape/two-pane is out of scope unless the lock
is lifted by owner decision).

- **Breakpoints** in `core/responsive/breakpoints.dart` (built, along with
  `max_width_body.dart` and `grid_columns.dart`):
  `compact < 600 ≤ medium < 905 ≤ expanded < 1240 ≤ large`.
  Use these names; never hardcode `if (width > 600)` inline.
- **Flexible width, not fixed (see §13.7).** Content blocks (cards/banners/tiles/
  buttons/forms) use `width: double.infinity`/`Expanded`/`Flexible` + token
  margin/padding, and drop `maxWidth:` content caps — but **KEEP intrinsic fixed
  sizes for icons/avatars/badges/QR/sweep/spacers**. The owner is on a tablet.
- **Max-width anchor.** Content must NOT stretch edge-to-edge on wide screens.
  Wrap page bodies in `MaxWidthBody` (`core/responsive/max_width_body.dart`),
  which centers and caps content (e.g. `maxWidth: 560` for forms, `840` for reading
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

## 4. Lists & per-screen performance

- **Lazy by default.** Every scrolling list uses `ListView.builder` /
  `SliverList` / `GridView.builder`. Never `ListView(children: [...])` for
  data-driven or long lists. 43 `ListView(` sites exist (2026-08-18); most are
  static content pages and are correct as written, so convert the data-driven
  ones and leave the rest.
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
- **Zero raw `Color(0x…)` in widgets** — reached, and holding at 0 since
  2026-07-28 (the count excludes `tokens.dart`, which legitimately defines them). If a Figma color
  isn't a token yet, add it to `tokens.dart` with the Figma variable name, then
  use the token. Base palette (from node 922-2824): BG `#192B41`, text
  `#FFFFFF`, gold `#C9A84C`, deep `#01132D`, paragraph `#C2B8A2`.
- **Zero raw `TextStyle(fontSize:…)` in widgets** (526 at the outset, **116**
  on 2026-08-18). The raw-numeric form is **gone** — `fontSize: <digit>`
  outside `app/theme/` reads **0** — so all 116 survivors assemble token atoms
  such as `fontSize: SimfTokens.textSm`. That form must still go: use a named
  token style (`SimfTokens.titleM`, `bodyR`, …). The font family is set ONCE
  in the theme — never per-widget.

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
- No `try/catch` unless a failure is genuinely expected and handled (73 `catch`
  sites on 2026-08-18 — don't add reflexive ones).
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
  done, and run each local package's suite from ITS root. Measured 2026-08-18:
  `packages/simf_auth_pkg` **46/46** and `packages/simf_data_pkg` **15/15**.
  **The `simf_auth_pkg` signUp carve-out is dead.** This line used to read "the
  pre-existing signUp failure is a known baseline — do not count it as a
  regression"; that suite is green, there is nothing left to subtract, and a red
  test there is a regression like any other.

---

## 11. Lint gate (see analysis_options.yaml)
- **The gate is repo-wide zero, and CI enforces it.** `very_good_analysis` runs
  over `lib`, `test`, `integration_test` **and both local packages**, and the
  MobileApp stage no longer passes `--no-fatal-infos` — an analyzer info fails
  the build (2026-08-14).

  > This bullet used to read "do NOT flip rules to `error` globally — ~445
  > relative imports + 526 inline styles would flood day one and make the gate
  > unsatisfiable", and the per-module gate below used to be "zero NEW issues +
  > zero in the touched module's files, not repo-wide zero". Both were correct
  > when written and are now simply done: those two counts, and the ~2,137
  > infos and 1,628 over-long lines that came after them, are all 0. The
  > *reasoning* still governs anything added later — **promote a rule only once
  > it already reads zero**, because a gate that fails every build gets switched
  > off, which is exactly what happened to the .NET test stage.

- Two rules are OFF rather than clean, each with its measurement recorded at the
  rule in `analysis_options.yaml` (`specify_nonobvious_property_types`,
  `public_member_api_docs`). Targeted `// ignore:` sites, measured 2026-08-18:
  **9** under `lib/`, **28** across `lib` + `test` + `integration_test` +
  `packages`, plus 2 `// ignore_for_file:`. (`analysis_options.yaml` still says
  "roughly two dozen" at the rule; that was the all-scopes figure and it is
  close, but the `lib/` number is the one that matters when you are editing a
  screen.) **If you add one, write why the analyzer is WRONG about that line,
  or fix it.**
- **Never run `dart format` on its own.** The ban's premise was re-measured on
  2026-08-14 against 250 touched files and it holds exactly: Flutter 3.44's
  "tall" formatter strips the trailing commas `require_trailing_commas` demands,
  taking that rule from **0 to 109 findings** in one run.

  What it does NOT do is explode the diff — 152 files changed by +1120/-687,
  and every golden held. So formatting is usable, in **two steps, never one**:

  ```
  dart format <only the files you touched>
  dart fix --apply --code=require_trailing_commas .    # puts all 109 back
  ```

  Owner-authorised and executed once this way (2026-08-14):
  `lines_longer_than_80_chars` 1690 -> 1428, total infos 1856 -> 1594,
  `require_trailing_commas` back to 0, 1406 tests green, every golden holding
  **without** `--update-goldens`.

  **The end state is deliberately not `dart format`-stable.** Re-running the
  formatter would change 55 of those files straight back, because the formatter
  and `require_trailing_commas` genuinely disagree: the repo can satisfy one or
  the other, not both. This repo picks trailing commas. If you run the
  formatter, you own running step two afterwards — and do not expect a second
  format run to be a no-op.
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

### 13.3 Stable-after-done — bug fixes are always allowed
A page that passes the full per-page DoD is the **reference render**: don't churn
it for taste or re-open settled design without a reason. But it is **not frozen** —
correctness fixes and owner-requested changes are always allowed, and a real bug is
never gated behind a "page is done" status. Whatever you change, re-lock the page's
goldens, tests, and docs in the **same changeset** — that is what keeps a "done"
page trustworthy. (This supersedes the old "freeze-after-done / FINAL" rule per the
owner directive of 2026-07-08; the D-110 schema/enum/wire freeze is unaffected.)

**Blast-radius rule (D-694).** A change to shared foundations — `lib/app/router.dart`,
`packages/simf_auth_pkg` (session / roles / `effectiveAppRole`),
`lib/app/theme/tokens.dart`, `lib/core/widgets/*`, or shared strings in
`lib/app/localization/app_l10n.dart` — can silently break **other** screens' flows.
Before committing one you MUST: (a) run the role×route matrix test
(`test/app/router_role_matrix_test.dart`) and the flow tests
(`integration_test/app_flows_test.dart`); (b) name every screen the change can reach
in the commit message; (c) for any router/auth change, re-verify the sign-up face-
capture path on a device. **Goldens prove pixels, not navigation** — a green golden
did not catch the D-666 face-capture regression; only a flow/matrix test does.

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
Owner rule: every data-loaded page pulls to refresh. **PRESERVE it — never drop it
when restructuring a screen, and VERIFY (don't assume) it still fires after a
split.** Reuse the shared `SimfPullToRefresh` (ex-`KsaRefresh`) + `SimfPullableHost`
(ex-`KsaPullable`, hosts short empty/error states in a viewport-tall
always-scrollable box), or `SimfRefreshableMessage`, which is exactly that pairing
and saves hand-nesting it at every list branch. The refreshable child must use
`AlwaysScrollableScrollPhysics` or the gesture will not fire on short content.

**Hooks.** StatefulWidget `_load()` → `onRefresh: _load`. Riverpod →
**`refreshAsync(ref, provider.future)`** (`lib/core/utils/refresh.dart`).

> This section used to prescribe `ref.invalidate(provider); await
> ref.read(provider.future)`. **Do not use it.** That idiom RETHROWS on a failing
> endpoint, which rejects the RefreshIndicator's future and surfaces as an
> unhandled error on top of the error state the user can already see. 15 files had
> it; `refreshAsync` swallows the failure because the screen's own error branch is
> what should show it.

**Do not trust a grep audit here.** This section previously claimed the rule was
"already implemented repo-wide (a grep audit of all 49 screens confirmed it,
2026-06-28)". That was wrong twice: the 2026-07-28 re-audit found coverage had
regressed to 25 of 69 screens (Home had none at all), and on 2026-08-14 the
**badge** feature was found with no refresh hook on any branch — the state that
mattered most, because a pending account had no way to re-check approval short of
restarting the app. `SimfPageShell` only DEFINES the refresh widgets; applying them
is opt-in per screen, which is why a grep for the widget names reads as coverage
when it is not.

**The baseline is `test/repo/pull_to_refresh_coverage_test.dart`, not this
table.** That test enumerates every screen that fetches and asserts the ones
without a refresh hook are exactly its reviewed `_exempt` map — so a new screen
that loads data and forgets the pull fails the build, and a screen that
legitimately should not have one fails until it is added there *with its
reason*. The table below explains the **categories** and is the place to read
why; it is not a second copy to keep in step, and it deliberately does not
match entry-for-entry (`guest` and `forum_guide` appear here because they have
no pull, but they never fetch, so the test has nothing to excuse).

| Screen(s) | Why exempt |
|---|---|
| `sign_in`, `sign_up_form`, `sign_up_email_verify`, `email_otp_verify`, `forgot_password`, `reset_password`, `badge_activation`, `badge_password`, `badge_sign_in`, `biometric_step_up` | Submit-driven auth forms. Nothing is loaded to re-load. |
| `sign_up_interests`, `my_mobile` | They DO load from the API, but the user then edits/selects on top of it. A pull would discard in-progress input — actively worse than no pull. Both carry an explicit retry on the error branch instead. |
| `registration_status` | A gate screen whose explicit "Re-check" button already polls (Figma 1701:3789). |
| `scan_contact`, `scan_visitor` | Live camera preview; there is no scrollable and no fetch to repeat. |
| `splash`, `onboarding` | Transient boot screens that navigate away. |
| `accessibility` | Local `SharedPreferences` settings via a `Notifier` — no network read. |
| `meeting_confirm` | Takes its meeting from the route and performs one write; it has no load or error branch. |
| `guest`, `forum_guide` | Static content, no repository. |

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
