import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Pins the pull-to-refresh surface, because a grep has now been wrong twice.
///
/// CLAUDE.md section 13.6 is an owner rule: every data-loaded page pulls to
/// refresh. It used to claim the rule was "already implemented repo-wide (a
/// grep audit of all 49 screens confirmed it, 2026-06-28)". That claim failed
/// twice:
///
///   * the 2026-07-28 re-audit found coverage had regressed to 25 of 69
///     screens, with Home carrying none at all;
///   * on 2026-08-14 the whole `features/badge/` tree was found with no
///     refresh hook on any branch — worst in the state that needed it most,
///     since a pending account had no way to re-check approval short of
///     restarting the app.
///
/// Both slipped for the same reason: `SimfPageShell` only DEFINES the refresh
/// widgets, so grepping for their names reads as coverage when applying them
/// is opt-in per screen.
///
/// It then slipped a THIRD time, in this file, on 2026-08-18. The scan read
/// `*_screen.dart` and nothing else, so a screen whose body moved into
/// `widgets/` — which is what CLAUDE.md section 2 asks for — stopped matching
/// `_fetches` and dropped out of the sweep entirely instead of failing it.
/// `my_contacts_screen.dart` and `my_visitors_screen.dart` were both in that
/// position: their `SimfPullToRefresh` had moved into an extracted body and
/// the screen files kept only a `const MyContactsBody()`. Nothing was broken
/// in the app; the check had silently stopped checking them, which is worse
/// than not having it, because the green tick reads as coverage.
///
/// So the unit of measurement is the screen PLUS its extracted body — see
/// [_bundle].
///
/// A FOURTH time, on 2026-08-20, and the same shape again: `_fetches` only
/// matched `ref.watch(` written on ONE line, so a screen whose read had wrapped
/// to `ref\n    .watch(` — ordinary formatting, nothing wrong with it — read as
/// not fetching and dropped out of the sweep instead of being checked.
/// `registration_success_screen.dart` was sitting in that hole. Widening the
/// pattern to tolerate the newline caught it, and it is exempt below on its
/// merits rather than by accident.
///
/// The pattern of all four is worth naming, since a fifth will look like none
/// of them: every slip made a screen INVISIBLE to the sweep, never visibly
/// wrong. A green tick here means "the screens I could see are covered", so the
/// question to ask of any change to this file is what it stops seeing.
///
/// This test is the section 4 pattern applied to section 13.6 — pin the
/// surface with a test, not a comment. It enumerates every screen that fetches
/// and asserts the ones WITHOUT a refresh hook are exactly the reviewed exempt
/// list. A new screen that loads data and forgets the pull fails the build; a
/// screen that legitimately should not have one fails until it is added here
/// WITH its reason, which is the argument this test exists to force.
///
/// The working directory for `flutter test` is the package root
/// (`src/Mobile/simf_app`), so every path below is relative to that.

/// Screens that fetch but must NOT pull to refresh. **This list is the
/// baseline** — CLAUDE.md section 13.6 explains the categories and points
/// here; it does not carry a second copy to keep in step. The two could not be
/// kept identical anyway: the doc names `guest` and `forum_guide` as having no
/// pull, but neither fetches, so listing them here would fail the
/// stale-exemption test below. The doc answers "which screens have no pull, and
/// why"; this answers "which FETCHING screens are excused", which is a smaller
/// question.
///
/// The reason string is the point of each entry — keep it accurate.
const Map<String, String> _exempt = <String, String>{
  // Submit-driven auth forms — nothing is loaded, so there is nothing to
  // re-load.
  'account/badge_activation_screen.dart': 'auth form',
  'account/badge_password_screen.dart': 'auth form',
  'account/badge_sign_in_screen.dart': 'auth form',
  'account/biometric_step_up_screen.dart': 'auth form',
  'account/email_otp_verify_screen.dart': 'auth form',
  'account/forgot_password_screen.dart': 'auth form',
  'account/reset_password_screen.dart': 'auth form',
  'account/sign_in_screen.dart': 'auth form',
  'account/sign_up_email_verify_screen.dart': 'auth form',
  'account/sign_up_form_screen.dart': 'auth form',

  // This DOES load from the API, but the user then edits and selects on top
  // of it. A pull would discard in-progress input, which is worse than no
  // pull; an explicit retry on the error branch stands in for it.
  //
  // `account/sign_up_interests_screen.dart` sat here beside it and no longer
  // does, which is the bundle rule (see [_bundle]) doing its job rather than a
  // change of mind: its extracted body carries a SimfRefreshableMessage on its
  // own error branch, so it does offer a refresh affordance and needs no
  // excuse. CLAUDE.md section 13.6 still lists both under "the user edits the
  // loaded data" and is still right — the data branch has no pull. That doc
  // answers a different question, as the note above says.
  'myarea/my_mobile_screen.dart': 'user edits the loaded data',

  // A gate screen whose explicit "Re-check" button already polls (Figma
  // 1701:3789).
  'registration/registration_status_screen.dart': 'explicit Re-check button',

  // Terminal confirmation of sign-up, reached as a replacement and offline-safe
  // by contract (D-366). Its one read is the CP-editable welcome line (D-461),
  // which already falls back to bundled copy; the reference number comes from
  // the route. There is nothing a pull could fetch that the screen does not
  // already have.
  'registration/registration_success_screen.dart': 'terminal, offline-safe',

  // Live camera preview: no scrollable, and no fetch to repeat.
  'contacts/scan_contact_screen.dart': 'camera preview',
  'exhibitor/scan_visitor_screen.dart': 'camera preview',

  // Transient boot screens that navigate away.
  'onboarding/onboarding_screen.dart': 'transient boot screen',
  'splash/splash_screen.dart': 'transient boot screen',

  // Local SharedPreferences settings via a Notifier — no network read.
  'accessibility/accessibility_screen.dart': 'local prefs, no network',

  // Takes its meeting from the route and performs one write; no load branch.
  'meetings/meeting_confirm_screen.dart': 'route-supplied, write-only',
};

/// A screen "fetches" when it reads a provider or declares the repo's standard
/// `_load()` hook.
///
/// The whitespace between `ref`, `.` and the method is what closes the fourth
/// slip in the header: a wrapped `ref\n    .watch(` is ordinary formatting and
/// must count. No `\b` before `ref` — `_ref.read(` has to keep matching, and a
/// word boundary would not fire between `_` and `r`.
final RegExp _fetches =
    RegExp(r'ref\s*\.\s*(watch|read)\(|Future<void> _load');

/// Any of the shared refresh affordances, a raw RefreshIndicator, or an
/// `onRefresh:` handed to a child that owns one.
///
/// That last alternative is load-bearing, not slack. Dropping it as
/// "redundant" — every refresh widget takes `onRefresh:` anyway — turned five
/// screens red that genuinely do refresh: `home`, `my_seat`, `seat_picker`,
/// `session_detail` and `staff_seating` all delegate to a child widget
/// (`VisitorHome`, `SeatMapAsyncView`) that owns the `SimfPullToRefresh`, so
/// the screen file itself contains only the forwarded callback.
///
/// It is matched WITH its colon, so a `Future<void> onRefresh()` declaration
/// alone does not count as coverage — the callback has to be passed to
/// something.
final RegExp _refreshes = RegExp(
  'SimfPullToRefresh|SimfRefreshableMessage|RefreshIndicator|onRefresh:',
);

/// An import of a file in a feature's own `widgets/` folder, capturing the
/// feature and the file name.
final RegExp _featureWidgetImport =
    RegExp(r"import\s+'package:simf_app/features/([^/]+)/widgets/([^']+)'");

/// The feature a `lib/` path belongs to. Null for `lib/app/` and `lib/core/`.
final RegExp _featurePath = RegExp('^lib/features/([^/]+)/');

/// Windows `Directory.listSync` returns backslash paths.
String _posix(String path) => path.replaceAll(r'\', '/');

/// Every `*_screen.dart` under `lib/`, keyed the way [_exempt] is, each mapped
/// to its BUNDLE rather than to its own source — see [_bundle].
Map<String, String> _screenBundles() {
  final out = <String, String>{};
  for (final entity in Directory('lib').listSync(recursive: true)) {
    if (entity is! File || !entity.path.endsWith('_screen.dart')) {
      continue;
    }
    final key = _posix(entity.path)
        .replaceFirst('lib/features/', '')
        .replaceFirst('lib/', '');
    out[key] = _bundle(entity);
  }
  return out;
}

/// A screen's source plus the source of every widget it pulls in from its OWN
/// feature's `widgets/` folder, transitively — the screen as the user meets
/// it, rather than as it happens to be filed.
///
/// This is what closes the third slip in the header. A screen that hands its
/// whole body to `MyContactsBody` is not a screen without a pull-to-refresh;
/// it is a screen whose pull-to-refresh is one file over, exactly where
/// CLAUDE.md section 2 says to put it. Reading the screen file alone made
/// obeying section 2 look, to this test, like deleting the screen.
///
/// **Same-feature `widgets/` only, and that limit is the entire design.**
/// Following `app/widgets/` would walk into `SimfPageShell`, which DEFINES
/// `SimfPullToRefresh` — every screen in the app would read as covered, which
/// is the original 2026-07-28 bug rebuilt one level deeper. A feature's own
/// `widgets/` folder holds that feature's extracted parts and nothing shared,
/// so following it adds the body and no false coverage.
String _bundle(File screen) {
  final source = screen.readAsStringSync();
  final feature = _featurePath.firstMatch(_posix(screen.path))?.group(1);
  if (feature == null) {
    return source;
  }

  final parts = <String>[source];
  final seen = <String>{};
  final queue = _widgetImports(source, feature);
  while (queue.isNotEmpty) {
    final path = queue.removeLast();
    if (!seen.add(path)) {
      continue;
    }
    final widgetSource = File(path).readAsStringSync();
    parts.add(widgetSource);
    queue.addAll(_widgetImports(widgetSource, feature));
  }
  return parts.join('\n');
}

/// The paths [source] imports from `lib/features/[feature]/widgets/`.
List<String> _widgetImports(String source, String feature) =>
    _featureWidgetImport
        .allMatches(source)
        .where((match) => match.group(1) == feature)
        .map((match) => 'lib/features/$feature/widgets/${match.group(2)}')
        .toList();

void main() {
  group('CLAUDE.md 13.6 — pull-to-refresh on every data-loaded screen', () {
    test('every fetching screen refreshes, unless it is a listed exemption',
        () {
      final missing = <String>[];
      _screenBundles().forEach((path, source) {
        if (!_fetches.hasMatch(source) || _refreshes.hasMatch(source)) {
          return;
        }
        if (!_exempt.containsKey(path)) {
          missing.add(path);
        }
      });

      expect(
        missing,
        isEmpty,
        reason: 'These screens load data but offer no pull-to-refresh, which '
            'the owner rule in CLAUDE.md section 13.6 requires. Either wrap '
            'the body in SimfPullToRefresh (and its short states in '
            'SimfRefreshableMessage), or add the screen to _exempt WITH the '
            'reason and mirror it into section 13.6.',
      );
    });

    test('no exemption outlives the screen it was written for', () {
      // A stale entry is how the list quietly stops describing the app: the
      // screen is deleted or gains a refresh, and the exemption keeps standing
      // guard over nothing.
      final screens = _screenBundles();
      final stale = <String>[];
      for (final path in _exempt.keys) {
        final source = screens[path];
        if (source == null) {
          stale.add('$path (no such screen)');
        } else if (_refreshes.hasMatch(source)) {
          stale.add('$path (now refreshes — drop the exemption)');
        } else if (!_fetches.hasMatch(source)) {
          stale.add('$path (no longer fetches — the exemption is moot)');
        }
      }

      expect(
        stale,
        isEmpty,
        reason: 'Remove these from _exempt and from CLAUDE.md section 13.6.',
      );
    });

    // The badge regression itself is pinned BEHAVIOURALLY, in
    // `test/features/badge/badge_screen_test.dart`: four tests that pump the
    // screen and assert the dashboard is actually re-fetched on a pull, one
    // per branch, including the pending account discovering approval.
    //
    // A third test lived here that counted `SimfRefreshableMessage` in the
    // source text and required at least three. It was deleted rather than
    // kept, for two reasons worth recording so it is not re-added:
    //
    //   * it asserted nothing the branch tests do not assert better - counting
    //     a type name proves the token is typed three times, not that the
    //     pending branch is one of the three, which is what its `reason:`
    //     claimed;
    //   * it pinned an implementation shape, so the Decision 7 `AsyncValue`
    //     conversion would have reddened it legitimately but confusingly, and
    //     the cheapest response then is to delete the assertion rather than
    //     re-argue it. Better to not have written it.
    //
    // What this file is for is the SWEEP - catching the next screen that
    // forgets - not re-proving one screen the widget tests already cover.
  });
}
