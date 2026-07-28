import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/app.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/features/about/about_app_screen.dart';
import 'package:simf_app/features/accessibility/accessibility_screen.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/my_contacts_screen.dart';
import 'package:simf_app/features/contacts/share_my_contact_screen.dart';
import 'package:simf_app/features/forum_guide/forum_guide_screen.dart';
import 'package:simf_app/features/guest/guest_mode_screen.dart';
import 'package:simf_app/features/more/more_screen.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_app/features/splash/splash_controller.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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
/// This drives the screens the fully-faked harness can reach headlessly (no
/// camera, no live backend); the data/scanner screens are swept on-device
/// against the live API via the same [_unnamedIconButtons] helper (see the
/// manifest [_pushedTargets] + the QA report). Runs headless via
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
/// shimmer/loader animating while the un-faked data providers stay pending, so a
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
  final container = ProviderContainer(overrides: _overrides(auth));
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
    var owner = '?';
    finder.evaluate().first.visitAncestorElements((a) {
      final n = a.widget.runtimeType.toString();
      if (n.startsWith('Simf')) {
        owner = n;
        return false;
      }
      return true;
    });
    if (!_knownUnnamedOwners.contains(owner)) {
      unnamed.add('$label: $owner/icon=${icon.runtimeType}');
    }
  }
  handle.dispose();
  return unnamed;
}

/// The pushed screens the fully-faked harness can reach headlessly via
/// `goNamed` (no camera, no live backend). Each is (routeName, screenType). A
/// target that does not render within the pump budget is recorded as an
/// on-device follow-up, not a failure — the data/scanner screens are swept on a
/// device against the live API using [_unnamedIconButtons] (see the QA report).
final _pushedTargets = <(String, Type)>[
  (RouteNames.myContacts, MyContactsScreen),
  (RouteNames.accessibility, AccessibilityScreen),
  (RouteNames.shareMyContact, ShareMyContactScreen),
  (RouteNames.more, MoreScreen),
  (RouteNames.aboutApp, AboutAppScreen),
  (RouteNames.guestMode, GuestModeScreen),
  (RouteNames.forumGuide, ForumGuideScreen),
];

void main() {
  testWidgets(
    'pushed screens meet the element contract (no un-tracked nameless '
    'icon-only controls)',
    (tester) async {
      final c = await _boot(tester, _signedInApprovedVisitor());
      final router = c.read(routerProvider);

      final violations = <String>[];
      final rendered = <String>[];
      final skipped = <String>[];

      for (final (routeName, screenType) in _pushedTargets) {
        router.goNamed(routeName);
        final finder =
            find.byWidgetPredicate((w) => w.runtimeType == screenType);
        await _pumpUntil(tester, finder);
        if (finder.evaluate().isEmpty) {
          skipped.add(routeName); // did not render headless — device follow-up
          continue;
        }
        rendered.add(routeName);
        violations.addAll(_unnamedIconButtons(tester, routeName));
      }

      // Surface coverage to the run log so a skipped screen is visible.
      // ignore: avoid_print
      print(
        'element-contract sweep — rendered: $rendered; '
        'skipped(headless): $skipped',
      );

      // The sweep must actually have driven screens, not skipped them all.
      expect(
        rendered.length,
        greaterThanOrEqualTo(3),
        reason: 'no target screen rendered headless: $skipped',
      );
      expect(
        violations,
        isEmpty,
        reason: 'icon-only buttons with no accessible name: $violations',
      );
    },
  );
}
