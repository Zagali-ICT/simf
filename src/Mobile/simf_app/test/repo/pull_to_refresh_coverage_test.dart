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

/// Screens that fetch but must NOT pull to refresh. The reason is the point:
/// keep it accurate, and mirror any change into CLAUDE.md section 13.6.
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

/// Any of the shared refresh affordances, or a raw RefreshIndicator.
final RegExp _refreshes = RegExp(
  'SimfPullToRefresh|SimfRefreshableMessage|RefreshIndicator|onRefresh',
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

    test('the badge screen refreshes — the 2026-08-14 regression', () {
      // Named rather than left to the sweep above: this is the one that got
      // through a grep audit, and the pending branch is the one that mattered.
      final source = File('lib/features/badge/badge_screen.dart')
          .readAsStringSync();
      expect(_refreshes.hasMatch(source), isTrue);
      expect(
        'SimfRefreshableMessage'.allMatches(source).length,
        greaterThanOrEqualTo(3),
        reason: 'The pending, not-approved and load-error branches each need '
            'their own refresh host — a pending account discovers approval by '
            'pulling on one of them.',
      );
    });
  });
}
