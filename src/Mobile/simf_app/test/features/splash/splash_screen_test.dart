import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_logo.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/features/splash/data/splash_controller.dart';
import 'package:simf_app/features/splash/splash_screen.dart';

import '../../support/simf_test_scope.dart';

/// Replaces the real boot sequence with a fixed state so the screen's render
/// + one-shot route-out glue is tested in isolation (the sequence itself is
/// covered by splash_controller_test.dart).
class _StubSplashController extends SplashController {
  _StubSplashController(this._fixed);

  final SplashState _fixed;

  @override
  SplashState build() => _fixed;
}

/// Serves a fixed organization profile (or none) without touching prefs / HTTP.
class _StubOrgProfile extends OrgProfileController {
  _StubOrgProfile(this._fixed);

  final OrgProfile? _fixed;

  @override
  OrgProfile? build() => _fixed;
}

OrgProfile _edition({
  DateTime? start,
  DateTime? end,
  String location = 'Jeddah',
  String locationArabic = 'جدة',
}) =>
    OrgProfile(
      name: 'Saudi International Maritime Forum',
      nameArabic: 'الملتقى البحري السعودي الدولي',
      title: '',
      titleArabic: '',
      currentYear: 2027,
      status: 'Open',
      locationText: location,
      locationTextArabic: locationArabic,
      eventStartDate: start,
      eventEndDate: end,
      social: const OrgSocial(),
      aboutItems: const <OrgAboutItem>[],
      details: const <OrgDetail>[],
    );

/// Records the D-736 seam calls the soft-update dialog glue must make.
class _RecordingUpdateChecker implements AppUpdateChecker {
  int snoozeCalls = 0;
  int openStoreCalls = 0;

  @override
  Future<AppUpdateStatus> check() async => AppUpdateStatus.upToDate;

  @override
  Future<void> snoozeOptionalUpdate() async => snoozeCalls++;

  @override
  Future<void> openStoreListing() async => openStoreCalls++;
}

Future<void> _pump(
  WidgetTester tester,
  SplashState state, {
  AppUpdateChecker? checker,
  OrgProfile? profile,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/splash',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.splash,
        path: '/splash',
        builder: (c, s) => const SplashScreen(),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN')),
      ),
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
      ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        splashControllerProvider.overrideWith(
          () => _StubSplashController(state),
        ),
        orgProfileProvider.overrideWith(() => _StubOrgProfile(profile)),
        if (checker != null)
          appUpdateCheckerProvider.overrideWithValue(checker),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
      ),
    ),
  );
  await tester.pump();
}

void main() {
  group('SplashScreen (Page 001 — KSA design, D-361)', () {
    testWidgets('renders the brand lock-up while booting', (tester) async {
      await _pump(tester, const SplashLoading());

      expect(find.byType(SimfLogo), findsOneWidget);
      expect(find.text('SAUDI · MOD · RSNF'), findsOneWidget);
      expect(find.text('Saudi International Maritime Forum'), findsOneWidget);
      expect(find.text('4th Edition\n23–25 Nov 2026 · Riyadh'), findsOneWidget);
    });

    testWidgets(
        'the event line comes from the configured edition dates, not the '
        'bundled literal (#40-residual)', (tester) async {
      await _pump(
        tester,
        const SplashLoading(),
        profile: _edition(
          start: DateTime(2027, 3, 8),
          end: DateTime(2027, 3, 10),
        ),
      );

      expect(
          find.text('4th Edition\n8-10 March 2027 · Jeddah'), findsOneWidget,);
      // The hardcoded 2026 date must NOT survive a re-configured edition.
      expect(
        find.textContaining('23–25 Nov 2026'),
        findsNothing,
      );
    });

    testWidgets('the configured event line renders in Arabic too',
        (tester) async {
      await _pump(
        tester,
        const SplashLoading(),
        locale: const Locale('ar'),
        profile: _edition(
          start: DateTime(2027, 3, 8),
          end: DateTime(2027, 3, 10),
        ),
      );

      expect(
        find.text('النسخة الرابعة\n8-10 مارس 2027 · جدة'),
        findsOneWidget,
      );
    });

    testWidgets('an edition with dates but no location omits the separator',
        (tester) async {
      await _pump(
        tester,
        const SplashLoading(),
        profile: _edition(
          start: DateTime(2027, 3, 8),
          end: DateTime(2027, 3, 10),
          location: '',
          locationArabic: '',
        ),
      );

      expect(find.text('4th Edition\n8-10 March 2027'), findsOneWidget);
    });

    testWidgets(
        'an edition with no dates falls back to the bundled literal '
        '(first run / offline)', (tester) async {
      await _pump(tester, const SplashLoading(), profile: _edition());

      expect(find.text('4th Edition\n23–25 Nov 2026 · Riyadh'), findsOneWidget);
    });

    testWidgets('routes out once the boot resolves to a route name',
        (tester) async {
      await _pump(
        tester,
        const SplashReady(routeName: RouteNames.signIn),
      );
      await tester.pumpAndSettle();

      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('routes out to a resumed location', (tester) async {
      await _pump(tester, const SplashReady(location: '/'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
    });

    testWidgets(
        'soft update — "Later" snoozes the version and routes out (D-736)',
        (tester) async {
      final checker = _RecordingUpdateChecker();
      await _pump(
        tester,
        const SplashReady(routeName: RouteNames.signIn, softUpdate: true),
        checker: checker,
      );
      await tester.pumpAndSettle();

      expect(find.text('Update available'), findsOneWidget);
      await tester.tap(find.text('Later'));
      await tester.pumpAndSettle();

      expect(checker.snoozeCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets(
        'soft update — a scrim dismiss also snoozes and routes out (D-736)',
        (tester) async {
      final checker = _RecordingUpdateChecker();
      await _pump(
        tester,
        const SplashReady(routeName: RouteNames.signIn, softUpdate: true),
        checker: checker,
      );
      await tester.pumpAndSettle();

      // Tap outside the dialog (the barrier is dismissible for a soft update).
      await tester.tapAt(const Offset(8, 8));
      await tester.pumpAndSettle();

      expect(checker.snoozeCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets(
        'soft update — "Update now" opens the store then routes out (D-736)',
        (tester) async {
      final checker = _RecordingUpdateChecker();
      await _pump(
        tester,
        const SplashReady(routeName: RouteNames.signIn, softUpdate: true),
        checker: checker,
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Update now'));
      await tester.pumpAndSettle();

      // The store opened, and the soft dialog popped so boot resumes + snoozes.
      expect(checker.openStoreCalls, 1);
      expect(checker.snoozeCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets(
        'forced update — a non-dismissible gate blocks boot, no route-out '
        'and no Later (D-736)', (tester) async {
      final checker = _RecordingUpdateChecker();
      await _pump(tester, const SplashUpdateRequired(), checker: checker);
      await tester.pumpAndSettle();

      // The hard gate: title + Update now, but NO Later, and it never routes.
      expect(find.text('Update required'), findsOneWidget);
      expect(find.text('Update now'), findsOneWidget);
      expect(find.text('Later'), findsNothing);
      expect(find.text('SIGN-IN'), findsNothing);
      expect(find.text('HOME'), findsNothing);

      // The Android back button cannot dismiss it (PopScope canPop:false) —
      // the framework consumes the pop, so the dialog (and the gate) stay up.
      final popped = await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(popped, isTrue);
      expect(find.text('Update required'), findsOneWidget);
      expect(checker.snoozeCalls, 0); // a forced gate never snoozes
    });
  });
}
