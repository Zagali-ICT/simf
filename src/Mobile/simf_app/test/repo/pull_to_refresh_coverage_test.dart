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

  // These DO load from the API, but the user then edits or selects on top of
  // it. A pull would discard in-progress input, which is worse than no pull;
  // both carry an explicit retry on their error branch instead.
  'account/sign_up_interests_screen.dart': 'user edits the loaded data',
  'myarea/my_mobile_screen.dart': 'user edits the loaded data',

  // A gate screen whose explicit "Re-check" button already polls (Figma
  // 1701:3789).
  'registration/registration_status_screen.dart': 'explicit Re-check button',

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
final RegExp _fetches = RegExp(r'ref\.watch\(|ref\.read\(|Future<void> _load');

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

/// Every `*_screen.dart` under `lib/`, keyed the way [_exempt] is.
Map<String, String> _screens() {
  final out = <String, String>{};
  for (final entity in Directory('lib').listSync(recursive: true)) {
    if (entity is! File || !entity.path.endsWith('_screen.dart')) {
      continue;
    }
    final key = entity.path
        .replaceAll(r'\', '/')
        .replaceFirst('lib/features/', '')
        .replaceFirst('lib/', '');
    out[key] = entity.readAsStringSync();
  }
  return out;
}

void main() {
  group('CLAUDE.md 13.6 — pull-to-refresh on every data-loaded screen', () {
    test('every fetching screen refreshes, unless it is a listed exemption',
        () {
      final missing = <String>[];
      _screens().forEach((path, source) {
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
      final screens = _screens();
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
