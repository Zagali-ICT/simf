import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/app.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_app/features/splash/data/splash_controller.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../support/simf_test_scope.dart';

/// The App **element-contract sweep** — the mobile analog of the CP/Web live
/// element sweeps (2026-07-26 QA program, WS1). The role x route matrix
/// (`router_role_matrix_test.dart`) already proves every gate, and 199
/// per-screen widget + golden tests already prove each screen mounts and looks
/// right. What no test asserted uniformly is the **element contract**: on a
/// rendered screen nothing throws or overflows, a page actually rendered, and
/// every icon-only control exposes a non-empty *accessible name* (what a
/// screen reader speaks) — the app equivalent of the sweep's "0 unnamed
/// controls" gate.
///
/// It drives **every parameterless route** (see [_sweepPaths]) as a signed-in
/// approved visitor, headlessly — no camera, no live backend. A route this
/// account may not open is redirected by the router; that is correct, and the
/// sweep records it rather than sweeping whatever it landed on instead. Run
/// via
/// `flutter test test/app/screen_element_contract_test.dart`.

// ---------------------------------------------------------------------------
// Fakes + boot (mirrors integration_test/app_flows_test.dart's proven harness).
// ---------------------------------------------------------------------------
class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _s = <String, Object>{
    StorageKeys.onboardingCompleted: true,
    StorageKeys.preferredLanguage: 'en',
  };
  @override
  String? getString(String key) {
    final v = _s[key];
    return v is String ? v : null;
  }

  @override
  Future<bool> setString(String key, String value) async {
    _s[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) {
    final v = _s[key];
    return v is bool ? v : null;
  }
  @override
  Future<bool> setBool(String key, bool value) async {
    _s[key] = value;
    return true;
  }

  @override
  double? getDouble(String key) => null;
  @override
  Future<bool> setDouble(String key, double value) async => true;
  @override
  int? getInt(String key) => null;
  @override
  Future<bool> setInt(String key, int value) async => true;
  @override
  Future<bool> remove(String key) async {
    _s.remove(key);
    return true;
  }
}

class _FakeAuth extends AuthController {
  _FakeAuth(this._initial);
  final AuthState _initial;
  @override
  AuthState build() => _initial;
  @override
  Future<bool> hasEnrolledDeviceKey() async => false;

  // AuthController holds a `late final AuthRepository _repository` that
  // only the
  // real `build()` initialises, and it is private to simf_auth_pkg so a test
  // cannot set it. Any method reaching it therefore throws
  // LateInitializationError — which is what /registration/status (it calls
  // refreshCurrentUser on init) and /auth/biometric-step-up did during the
  // sweep. That is this harness being incomplete, not a screen defect, so the
  // methods those screens call are stubbed rather than excused.
  @override
  Future<CurrentUser> refreshCurrentUser() async {
    final state = _initial;
    return state is AuthStateSignedIn
        ? state.session.user
        : throw StateError('refreshCurrentUser on a signed-out fake');
  }

  @override
  Future<void> signOut() async {}

  /// `/auth/biometric-step-up` calls this from `initState`, so without a stub
  /// the screen cannot mount at all.
  @override
  Future<String> sendBiometricStepUp() async => 'a***@simf.test';
}

class _FakeContactsRepo implements ContactsRepository {
  @override
  Future<String> getMyShareToken() async => 'SHARETOKEN42';
  @override
  Future<String> rotateShareToken() async => 'SHARETOKEN99';
  @override
  Future<VisitorCard> resolve(String token) async => const VisitorCard(
        userId: 'u2',
        name: 'Bob Sailor',
        nameArabic: 'بوب',
        available: true,
        jobTitle: 'Chief Officer',
      );
  @override
  Future<SavedContactRow> save(String token, String? note) async =>
      const SavedContactRow(
        id: 's1',
        subjectUserId: 'u2',
        name: 'Bob Sailor',
        nameArabic: 'بوب',
        subjectAvailable: true,
      );
  @override
  Future<List<SavedContactRow>> listSaved() async => const <SavedContactRow>[];
  @override
  Future<void> remove(String id) async {}
  @override
  Future<String> getVcard(String id) async => 'BEGIN:VCARD\r\nEND:VCARD\r\n';
}

AuthState _signedInApprovedVisitor() => AuthStateSignedIn(
      Session(
        accessToken: 'A',
        refreshToken: 'R',
        accessTokenExpiresAt: DateTime(2099),
        user: CurrentUser(
          id: 'u1',
          email: 'a@simf.test',
          displayName: 'Alice',
          appRole: AppRole.visitor,
          preferredLanguage: PreferredLanguage.fromJson('en'),
          registrationStatus: RegistrationStatus.approved,
        ),
      ),
    );

/// A throwaway data config so screens that read [simfDataConfigProvider]
/// (About-app, Share-my-contact) build; the host is unreachable, so any live
/// fetch simply fails into the screen's error/empty state — the chrome the
/// contract inspects still renders.
const _dataConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test-key',
  deviceType: SimfDeviceType.android,
);

List<Override> _overrides(AuthState auth) {
  final prefs = _FakePrefs();
  return <Override>[
    simfDataConfigProvider.overrideWithValue(_dataConfig),
    simfPrefsStorageProvider.overrideWithValue(prefs),
    localeControllerProvider.overrideWith(() => LocaleController(prefs: prefs)),
    accessibilityControllerProvider
        .overrideWith(() => AccessibilityController(prefs: prefs)),
    minSplashDurationProvider.overrideWithValue(Duration.zero),
    appUpdateCheckerProvider.overrideWithValue(const NoopAppUpdateChecker()),
    authControllerProvider.overrideWith(() => _FakeAuth(auth)),
    unreadNotificationCountProvider.overrideWith((ref) async => 0),
    contactsRepositoryProvider.overrideWithValue(_FakeContactsRepo()),
  ];
}

/// Pumps until [target] renders (or [maxMs] elapses), then a few settle frames.
/// [WidgetTester.pumpAndSettle] can't be used: several SIMF screens keep a
/// shimmer/loader animating while the un-faked data providers stay pending,
/// so a
/// full settle never quiesces headless. Polling for the target screen is enough
/// for the element contract to inspect it.
Future<void> _pumpUntil(
  WidgetTester tester,
  Finder target, {
  int maxMs = 6000,
}) async {
  await tester.pump();
  var waited = 0;
  while (target.evaluate().isEmpty && waited < maxMs) {
    await tester.pump(const Duration(milliseconds: 100));
    waited += 100;
  }
  // A few more frames so late children (avatars, QR) lay out.
  for (var i = 0; i < 6; i++) {
    await tester.pump(const Duration(milliseconds: 50));
  }
}

Future<ProviderContainer> _boot(WidgetTester tester, AuthState auth) async {
  final container = ProviderContainer(
    overrides: _overrides(auth),
    retry: simfTestNoRetry,
  );
  addTearDown(container.dispose);
  await tester.pumpWidget(
    UncontrolledProviderScope(container: container, child: const SimfApp()),
  );
  await tester.pump();
  return container;
}

/// Known, tracked a11y gaps the sweep still excuses, keyed by owning widget.
///
/// **Empty on purpose — do not add to it without a tracked defect.** The sweep
/// is a ratchet: every entry here is a nameless control the test agrees not to
/// fail on, so a non-empty set is slack, not configuration.
///
/// It held exactly one entry when the sweep was written on 2026-07-26:
/// `SimfCircledBackButton` (DEF-SWEEP-003), the circled back chevron, which set
/// no `tooltip` while its siblings `SimfBackButton` and `SimfMenuButton` did.
/// Commit `e26eb507` gave it
/// `tooltip: MaterialLocalizations.of(context).backButtonTooltip`, so the
/// excuse is retired here rather than left behind — a stale allowlist entry
/// silently re-permits the very defect it was written for.
const _knownUnnamedOwners = <String>{};

/// Icon codepoints of controls rendered by a **third-party package**, which we
/// cannot name without forking it.
///
/// Kept separate from [_knownUnnamedOwners] and keyed on the glyph rather than
/// the owner, because a package control has no `Simf*` ancestor to key on and
/// would otherwise need the owner check loosened — which would also excuse our
/// own widgets. Precision matters here: the first run of this sweep flagged
/// twenty controls, and nine of them reported the same useless owner
/// (`SimfApp`) while being entirely ours and entirely fixable.
///
///   * `U+E292` — the torch toggle inside `flutter_zxing`'s `ReaderWidget`, on
///     `/auth/badge` and `/contacts/scan`. `SimfScannerBody` keeps the built-in
///     torch (it auto-hides where unsupported) and suppresses the package's own
///     overlay and camera-flip. TRACKED AS A REAL GAP: a blind user scanning in
///     poor light gets an unlabelled button. Closing it means either
///     upstreaming
///     a tooltip to flutter_zxing or hiding the package torch and rendering our
///     own labelled one — a behaviour change, not a test change.
const _thirdPartyUnnamedGlyphs = <int>{0xe292};

/// Routes that throw while rendering, with the reason. **Tracked defects, not
/// exemptions** — each is a real problem this sweep found on its first full run
/// and each needs a decision that is bigger than a test change.
///
/// * (none — the list is empty; see below.)
///
/// `/sessions/join` and `/session-summaries` were on this list with
/// "BoxConstraints forces an infinite height". FIXED 2026-07-29, and the
/// diagnosis recorded here was WRONG, which is worth keeping: it blamed
/// `SimfPageShell` for wrapping its body in a scroll host, and concluded the
/// real fix was a design decision across ~40 screens. The shell does no such
/// thing — it lays the body out under `Expanded`, and its own doc says the body
/// is "not scrollable by itself". The actual cause was a two-line double-wrap
/// at two call sites: `SimfRefreshableMessage` ALREADY wraps its child in a
/// `SimfPullableHost`, and both screens' error branches wrapped it in a second
/// one. Two nested `SingleChildScrollView`s hand the inner `LayoutBuilder`
/// maxHeight: infinity, which becomes `BoxConstraints(minHeight: infinity)`.
/// Removing the redundant wrapper fixed both. The lesson is the one this sweep
/// keeps teaching: a plausible diagnosis written from reading the code is not a
/// diagnosis. Reading the widget the call site actually used would have taken a
/// minute and would not have produced a ~40-screen design decision.
///
/// `/auth/biometric-step-up` and `/registration/status` were on this list too,
/// with a LateInitializationError on `AuthController._repository`. Both were
/// harness-side, not screen defects — the fake overrides `build()`, so the
/// package's `late final` repository is never assigned and any method reaching
/// it throws. Stubbing `refreshCurrentUser` and `signOut` on `_FakeAuth`, and
/// draining with `takeException()`, cleared both; they are removed rather than
/// left behind.
///
/// These two are **excluded from [_sweepPaths] entirely**, not merely
/// tolerated. The layout assertion breaks semantics flushing, so it is raised
/// by `pump()` itself rather than deferred, and flutter_test then fails the
/// whole run at teardown no matter what the test consumes. Keeping them in
/// would abort the sweep and hide every route after them — a permanently red
/// test that gets ignored is worse than an honest, green sweep of 56 routes
/// with two written down. They must be re-added the moment the shell issue is
/// fixed.
const _knownRenderIssues = <String>{
  // Empty, and it must stay that way: a route here is a screen nobody is
  // watching. Both former entries were fixed on 2026-07-29 and are back in
  // [_sweepPaths].
};

// ---------------------------------------------------------------------------
// The reusable element-contract assertion (also used by the on-device sweep).
// ---------------------------------------------------------------------------
/// Collects element-contract violations on the screen currently pumped into
/// [tester] — the sweep's portable signal: every icon-only [IconButton] must
/// expose a non-empty accessible name (rendered-semantics label, `tooltip`, or
/// the icon's `semanticLabel`), the app analog of the CP/Web "0 unnamed
/// controls" gate. Known/tracked gaps in [_knownUnnamedOwners] are excused
/// (the ratchet); a *new* nameless control is returned as a violation.
List<String> _unnamedIconButtons(WidgetTester tester, String label) {
  final handle = tester.ensureSemantics();
  final unnamed = <String>[];
  final iconButtons = find.byType(IconButton);
  for (var i = 0; i < iconButtons.evaluate().length; i++) {
    final finder = iconButtons.at(i);
    // A hidden/offstage control isn't user-reachable — don't demand a name.
    if (finder.evaluate().isEmpty) continue;
    final node = tester.getSemantics(finder);
    final ib = tester.widget<IconButton>(finder);
    final icon = ib.icon;
    final iconLabel = icon is Icon ? (icon.semanticLabel ?? '') : '';
    final named = node.label.trim().isNotEmpty ||
        (ib.tooltip?.trim().isNotEmpty ?? false) ||
        iconLabel.trim().isNotEmpty;
    if (named) continue;
    final glyphCode = icon is Icon ? icon.icon?.codePoint : null;
    if (glyphCode != null && _thirdPartyUnnamedGlyphs.contains(glyphCode)) {
      continue; // package-owned control — see _thirdPartyUnnamedGlyphs
    }

    // Report the nearest Simf* owner AND the raw ancestor chain. The owner
    // alone is not enough to find the control: a button declared in a
    // feature-local header (OnboardingTopBar, AccountSubHeader, …) has no Simf*
    // ancestor nearer than the app root, so nine real violations all reported
    // as "SimfApp" and had to be tracked down by static scan instead.
    var owner = 'SimfApp';
    final chain = <String>[];
    finder.evaluate().first.visitAncestorElements((a) {
      final n = a.widget.runtimeType.toString();
      if (chain.length < 8 && !n.startsWith('_')) chain.add(n);
      if (owner == 'SimfApp' && n.startsWith('Simf') && n != 'SimfApp') {
        owner = n;
      }
      return chain.length < 8;
    });
    if (!_knownUnnamedOwners.contains(owner)) {
      final glyph = icon is Icon ? '${icon.icon}' : icon.runtimeType.toString();
      unnamed.add(
        '$label: $owner/icon=$glyph via ${chain.join(" < ")}',
      );
    }
  }
  handle.dispose();
  return unnamed;
}

/// Every route in the app that takes **no path parameter**, driven by PATH.
///
/// The first version of this sweep named seven screens as `(routeName, Type)`
/// pairs, which meant importing each screen class and so, in practice, a list
/// that would only ever grow by hand — seven of the app's screens were covered
/// and the other fifty-six were not, silently. Driving by path removes the
/// per-screen import entirely, so the cost of covering a route is one line.
///
/// The nine parameterised routes (session detail, my-seat, seat-picker, speaker
/// / sponsor / exhibitor detail, booth map, moderate, staff seating) are NOT
/// here: they need a real id, and a fabricated one sends the screen straight to
/// its not-found state, which would sweep the error page and report a pass.
/// They are covered by their own widget tests, which build them with seeded
/// data.
const _sweepPaths = <String>[
  '/auth/badge-activation', '/auth/badge-password', '/auth/badge',
  '/auth/biometric-step-up', '/auth/forgot-password',
  '/auth/reset-password', '/auth/verify-otp',
  '/splash', '/onboarding', '/sign-in', '/sign-up', '/sign-up/otp',
  '/sign-up/visitor', '/sign-up/interests', '/terms',
  '/registration/success', '/registration/status', '/guest',
  '/', '/my-area', '/map', '/sessions', '/speakers', '/delegations',
  '/booths', '/sponsors', '/archive', '/live', '/live/question',
  '/news', '/media', '/media-partners', '/badge', '/notifications',
  '/ai-summary', '/meet', '/chatbot', '/about', '/settings/accessibility',
  '/rate', '/more', '/contacts', '/contacts/share', '/contacts/scan',
  '/my-area/verify-identity', '/my-area/interests', '/my-area/mobile',
  '/gates/scan', '/exhibitor/scan', '/exhibitor/visitors', '/requests',
  '/my-sessions',
  '/staff/register-visitor', '/meetings', '/meeting-confirm',
  '/forum-guide', '/faq', '/session-presentations', '/contact-us',
  '/about-app',
  // Back in the sweep as of 2026-07-29. Both were pulled out entirely for
  // "BoxConstraints forces an infinite height"; the cause turned out to be a
  // redundant SimfPullableHost at each screen's error branch, not the shell.
  '/sessions/join', '/session-summaries',
];

/// Where the router actually ended up, which is not always where we asked: a
/// role gate may redirect, and that is correct behaviour, not a failure.
String _location(GoRouter router) =>
    router.routerDelegate.currentConfiguration.uri.toString();

void main() {
  testWidgets(
    'every parameterless route meets the element contract '
    '(no nameless icon-only controls)',
    (tester) async {
      final container = await _boot(tester, _signedInApprovedVisitor());
      final router = container.read(routerProvider);

      final violations = <String>[];
      final swept = <String>[];
      final redirected = <String>[];
      // Render exceptions, attributed to the route that caused them, drained
      // after each navigation with the supported `tester.takeException()`.
      //
      // Attribution matters: without it the runner reports "Multiple exceptions
      // (76) were detected" and nothing more — one root cause cascading into 64
      // follow-on "RenderBox was not laid out" reads as 65 separate problems
      // and none of them names a screen. Draining matters just as much: an
      // unconsumed exception is rethrown by the NEXT pump(), so a single bad
      // screen would abort the sweep and hide every route after it.
      final renderErrors = <String, String>{};

      for (final path in _sweepPaths) {
        router.go(path);
        // Pump for a screen to appear rather than pumpAndSettle: several SIMF
        // screens shimmer forever headless because their data providers never
        // resolve, so a full settle never quiesces.
        await _pumpUntil(tester, find.byType(Scaffold), maxMs: 2000);

        for (var thrown = tester.takeException();
            thrown != null;
            thrown = tester.takeException()) {
          renderErrors.putIfAbsent(
            path,
            () => thrown.toString().split('\n').first,
          );
        }

        final landed = _location(router);
        if (landed != path) {
          // A gate sent us elsewhere (this account is an approved visitor, so
          // the staff / exhibitor / moderator routes correctly bounce). The
          // redirect matrix is asserted exhaustively by
          // router_role_matrix_test.dart; here it only means "not this
          // account's screen", and sweeping whatever we landed on instead
          // would report a pass for a page we never asked for.
          redirected.add('$path -> $landed');
          continue;
        }
        swept.add(path);
        violations.addAll(_unnamedIconButtons(tester, path));
      }

      // Coverage is part of the result: a run that swept three screens and
      // called itself green is the failure mode this replaced.
      // ignore: avoid_print
      print(
        'element-contract sweep — swept ${swept.length}/${_sweepPaths.length}; '
        'redirected ${redirected.length}; '
        'routes that threw ${renderErrors.length}\n'
        '  swept: $swept\n'
        '  redirected: $redirected\n'
        '  threw: $renderErrors',
      );

      expect(
        swept.length,
        greaterThanOrEqualTo(40),
        reason: 'the sweep reached far fewer routes than expected — the '
            'harness or the router changed. '
            'swept=$swept redirected=$redirected',
      );
      expect(
        violations,
        isEmpty,
        reason: 'icon-only controls with no accessible name: $violations',
      );
      expect(
        renderErrors,
        isEmpty,
        reason: 'routes that threw while rendering: $renderErrors',
      );
      // The exclusion list must not rot: a route named there but still absent
      // from _sweepPaths after its defect is fixed silently loses coverage.
      expect(
        _knownRenderIssues.where(_sweepPaths.contains),
        isEmpty,
        reason: 'a route is both excluded and swept — pick one',
      );
    },
  );
}
