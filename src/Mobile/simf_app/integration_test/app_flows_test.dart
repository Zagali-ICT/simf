import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/app.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/features/accessibility/accessibility_screen.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_app/features/accessibility/widgets/accessibility_toggle_row.dart';
import 'package:simf_app/features/accessibility/widgets/size_chip.dart';
import 'package:simf_app/features/account/sign_in_screen.dart';
import 'package:simf_app/features/account/widgets/sign_in_alt_actions.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/my_contacts_screen.dart';
import 'package:simf_app/features/contacts/share_my_contact_screen.dart';
import 'package:simf_app/features/guest/guest_mode_screen.dart';
import 'package:simf_app/features/home/home_screen.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_app/features/myarea/identity_verification_screen.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_app/features/splash/data/splash_controller.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../test/support/simf_test_scope.dart';

/// End-to-end flow tests over the **assembled app** (real `SimfApp` → real
/// `routerProvider` → real screens). Only the leaf I/O is faked (auth state,
/// the contacts repository, the unread-count fetch), so these exercise the
/// cross-screen navigation glue — splash routing, the auth gate, and the
/// FDS-014 / guest-entry wiring — that the per-screen widget tests cannot.
///
/// **Needs a device**: run it as
/// `flutter test integration_test/app_flows_test.dart -d DEVICE_ID`. It cannot run headless — `IntegrationTestWidgetsFlutterBinding`
/// requires an attached target — and the header claimed otherwise until
/// 2026-08-20, which is a large part of why nobody noticed the file had never
/// passed. Nothing in CI or in a bare `flutter test` reaches it, so it is worth
/// running by hand whenever D-694's shared foundations move.

/// In-memory prefs: onboarding marked complete (so the splash routes a
/// signed-out user straight to sign-in, not onboarding) and the UI language
/// pinned to English for stable assertions.
class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _s = <String, Object>{
    StorageKeys.onboardingCompleted: true,
    StorageKeys.preferredLanguage: 'en',
  };

  @override
  String? getString(String key) =>
      _s[key] is String ? _s[key]! as String : null;
  @override
  Future<bool> setString(String key, String value) async {
    _s[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) => _s[key] is bool ? _s[key]! as bool : null;
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

/// A fake auth controller fixed to a given state. `hasEnrolledDeviceKey` is
/// overridden to false so the sign-in screen never touches the `local_auth`
/// plugin channel in the headless environment.
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

/// Share-my-contact needs BOTH repositories, which is easy to miss: the screen
/// only names `contactsRepositoryProvider`, but `ShareCardNotifier.build()`
/// awaits `myAreaRepositoryProvider` for the vCard as well
/// (contacts_providers.dart:37-39). Without this fake that second await hit the
/// unreachable test host, the notifier settled into AsyncError, and the screen
/// rendered its error state — so `find.byType(QrImageView)` found nothing and
/// the failure pointed at the QR widget rather than at the missing fake.
class _FakeMyAreaRepo implements MyAreaRepository {
  @override
  Future<String> getContactCardVcf() async => 'BEGIN:VCARD\r\nEND:VCARD\r\n';

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
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
          // Explicit, because the DEFAULT IS FALSE (current_user.dart:21) and
          // `routeAfterAuth` sends an incomplete profile to the sign-up screen
          // rather than Home. This file asserted Home while relying on a
          // default that never existed, so the assertion could not pass.
          profileComplete: true,
        ),
      ),
    );

/// A signed-in but **not-yet-approved** account. Its token carries a Visitor
/// role, but `effectiveAppRole` resolves to [AppRole.guest] until approval
/// (D-666). `profileComplete` is set explicitly so the splash boots it to Home
/// (an incomplete profile routes to the sign-up screen instead).
AuthState _signedInPendingVisitor() => AuthStateSignedIn(
      Session(
        accessToken: 'A',
        refreshToken: 'R',
        accessTokenExpiresAt: DateTime(2099),
        user: CurrentUser(
          id: 'u3',
          email: 'pending@simf.test',
          displayName: 'Pat Pending',
          appRole: AppRole.visitor,
          preferredLanguage: PreferredLanguage.fromJson('en'),
          registrationStatus: RegistrationStatus.pending,
          profileComplete: true,
        ),
      ),
    );

/// A throwaway data config, matching the one in
/// `test/app/screen_element_contract_test.dart`. The host is unreachable, so a
/// live fetch simply fails into the screen's error state — fine here, because
/// these tests assert routing and chrome, not data.
///
/// It is NOT optional. `simfDataConfigProvider` throws
/// `UnimplementedError("must be overridden at app startup")` by default, and
/// `simfApiClientProvider` watches it — so without this override every screen
/// that reaches the client dies at `SplashScreen` with "Tried to use a provider
/// that is in error state". That is exactly what this file did, on all six
/// tests, until 2026-08-20.
///
/// Worth more than the usual missing-line note. D-694 names this file as a
/// REQUIRED gate before any change to the shared foundations, on the grounds
/// that "goldens prove pixels, not navigation". The gate could not run: it
/// needs `-d <device>`, so it never went near CI or a headless suite, and the
/// throwing default it depends on landed separately (19a7e8b0). A mandated gate
/// that cannot execute is worse than none, because its name in the rule reads
/// as coverage.
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
    // Collapse the splash min-display so boot routes immediately.
    minSplashDurationProvider.overrideWithValue(Duration.zero),
    // D-736 — pin the launch update check inert (the server checker would
    // fail open anyway; this keeps the harness deterministic).
    appUpdateCheckerProvider.overrideWithValue(const NoopAppUpdateChecker()),
    authControllerProvider.overrideWith(() => _FakeAuth(auth)),
    // Home's only live call — pin it so the home never touches HTTP.
    unreadNotificationCountProvider.overrideWith((ref) async => 0),
    contactsRepositoryProvider.overrideWithValue(_FakeContactsRepo()),
    myAreaRepositoryProvider.overrideWithValue(_FakeMyAreaRepo()),
  ];
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
  await tester.pumpAndSettle();
  return container;
}

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets(
      'navy-always: the app pins themeMode to dark so a light-mode '
      'device still renders the navy theme (D-331)', (tester) async {
    await _boot(tester, const AuthStateSignedOut());

    // The root MaterialApp pins themeMode to dark — the OS light/dark setting
    // is ignored, so the on-navy screens are never rendered on a light
    // scaffold.
    final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
    expect(app.themeMode, ThemeMode.dark);

    // And the theme actually resolved on a screen is the dark (navy) brand,
    // even though a widget test's default platformBrightness is light — which
    // is exactly the case that themeMode.system would have rendered light.
    final ctx = tester.element(find.byType(SignInScreen));
    expect(Theme.of(ctx).brightness, Brightness.dark);
  });

  testWidgets(
      'guest entry: splash -> sign-in -> browse-as-guest -> guest '
      'landing -> Home (no auth)', (tester) async {
    await _boot(tester, const AuthStateSignedOut());

    // Signed-out + onboarding-complete -> the splash lands on sign-in.
    expect(find.byType(SignInScreen), findsOneWidget);

    // The "Browse without signing in" entry (D-325). Located by structure, not
    // by icon or label: D-363 redesigned it into the design-native underlined
    // link of Figma 627:2390, which carries NO icon, and its text is Arabic by
    // default. `SignInAltActions` holds exactly one TextButton — the badge and
    // biometric entries are not TextButtons — so this is unambiguous and
    // survives both a copy change and a locale change.
    await tester.tap(
      find.descendant(
        of: find.byType(SignInAltActions),
        matching: find.byType(TextButton),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(GuestModeScreen), findsOneWidget);

    // "Continue as guest" -> the public Home, as a Guest, with no token.
    await tester.tap(
      find.descendant(
        of: find.byType(GuestModeScreen),
        matching: find.byType(FilledButton),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(HomeScreen), findsOneWidget);
  });

  testWidgets(
      'signed-in visitor: boots to Home and reaches the FDS-014 '
      'contact screens via the real router', (tester) async {
    final container = await _boot(tester, _signedInApprovedVisitor());

    // An approved visitor with no saved last-route boots to Home.
    expect(find.byType(HomeScreen), findsOneWidget);

    final router = container.read(routerProvider);

    // My Contacts renders + shows the empty state (fake repo returns []).
    // `router` is used again below, so folding this into its declaration
    // would hide the later uses.
    // ignore: cascade_invocations
    router.goNamed(RouteNames.myContacts);
    await tester.pumpAndSettle();
    expect(find.byType(MyContactsScreen), findsOneWidget);
    expect(find.text('No saved contacts yet'), findsOneWidget);

    // Share my contact mints the token and renders it as a QR.
    router.goNamed(RouteNames.shareMyContact);
    await tester.pumpAndSettle();
    expect(find.byType(ShareMyContactScreen), findsOneWidget);
    expect(find.byType(QrImageView), findsOneWidget);
  });

  testWidgets(
      'accessibility: changing text size + high-contrast rebuilds the '
      'app without crashing (D-327)', (tester) async {
    final container = await _boot(tester, _signedInApprovedVisitor());

    final router = container.read(routerProvider);
    // `router` is used again below, so folding this into its declaration
    // would hide the later uses.
    // ignore: cascade_invocations
    router.goNamed(RouteNames.accessibility);
    await tester.pumpAndSettle();
    expect(find.byType(AccessibilityScreen), findsOneWidget);

    // Large text + high contrast force the root MediaQuery + theme to swap —
    // the whole tree must rebuild cleanly.
    // By structure again, for the same reason: these were `ChoiceChip` and
    // `SwitchListTile` matched on ENGLISH labels, and the screen renders in
    // Arabic and now builds `SizeChip` + `AccessibilityToggleRow` instead. The
    // last size chip is the largest; the first toggle row is high-contrast.
    await tester.tap(find.byType(SizeChip).last);
    await tester.pumpAndSettle();
    await tester.tap(
      find.descendant(
        of: find.byType(AccessibilityToggleRow).first,
        matching: find.byType(Switch),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(AccessibilityScreen), findsOneWidget);
  });

  testWidgets(
      'D-694 — a pending sign-up account reaches the face-capture '
      '(identity verification) screen, NOT Home', (tester) async {
    // The regression: the sign-up "capture face photo" button pushes route 103
    // (identityVerification). Since D-666 a pending account presents as an
    // effective guest, and while 103 was attendee-gated the redirect bounced
    // EVERY sign-up user to Home — sign-up was functionally broken. This drives
    // the real router with a real pending session through the assembled app.
    final container = await _boot(tester, _signedInPendingVisitor());
    expect(find.byType(HomeScreen), findsOneWidget); // pending boots to Home

    container.read(routerProvider).goNamed(RouteNames.identityVerification);
    // The liveness screen shows a camera-loading spinner that never settles, so
    // pump a couple of frames rather than pumpAndSettle.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.byType(IdentityVerificationScreen), findsOneWidget);
    expect(find.byType(HomeScreen), findsNothing);
  });

  testWidgets(
      'D-694 is targeted: a pending account is STILL sent home from an '
      'attendee-only route (/meet)', (tester) async {
    // Proves the fix opened exactly route 103, not the whole attendee tier —
    // the D-666 pending gate still holds everywhere else.
    final container = await _boot(tester, _signedInPendingVisitor());

    container.read(routerProvider).goNamed(RouteNames.meetPeople);
    await tester.pumpAndSettle();

    expect(find.byType(HomeScreen), findsOneWidget);
  });
}
