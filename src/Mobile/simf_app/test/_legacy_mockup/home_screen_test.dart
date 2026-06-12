import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/_legacy_mockup/home_screen.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

CurrentUser _visitor() => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Visitor',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: RegistrationStatus.approved,
    );

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _visitor(),
    );

/// A signed-in (Visitor) controller — its `build()` returns a fixed state so
/// the screen's privilege gate is testable without the real session machinery.
class _SignedInController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
}

/// A signed-out (Guest) controller.
class _GuestController extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

/// A fake repository (via `implements`, so it needs no real [SimfApiClient])
/// returning a fixed unread count — drives the bell badge deterministically.
class _FakeNotificationsRepository implements NotificationsRepository {
  _FakeNotificationsRepository(this.count);

  final int count;

  @override
  Future<int> getUnreadCount() async => count;

  @override
  Future<List<NotificationItem>> getNotifications({int skip = 0, int top = 50}) async =>
      const <NotificationItem>[];

  @override
  Future<bool> markRead(String id) async => true;

  @override
  Future<bool> markAllRead() async => true;
}

Future<void> _pump(
  WidgetTester tester, {
  required AuthController controller,
  int unread = 0,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const HomeScreen(),
      ),
      GoRoute(
        name: RouteNames.sessions,
        path: '/sessions',
        builder: (c, s) => const Scaffold(body: Text('SESSIONS')),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN')),
      ),
      GoRoute(
        name: RouteNames.notifications,
        path: '/notifications',
        builder: (c, s) => const Scaffold(body: Text('NOTIFICATIONS')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(() => controller),
        // Drive the real unread-count provider through a fake repository (the
        // signed-in path calls it; the guest path short-circuits to 0).
        notificationsRepositoryProvider
            .overrideWithValue(_FakeNotificationsRepository(unread)),
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
  await tester.pumpAndSettle();
}

void main() {
  group('HomeScreen (Page 013)', () {
    testWidgets('guest sees the sign-in prompt + public tiles, no bell/visitor '
        'tiles', (tester) async {
      await _pump(tester, controller: _GuestController());

      expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
      expect(find.text('Speakers'), findsOneWidget);
      // Visitor-only tiles are hidden for a guest (Logic L-2).
      expect(find.text('My area'), findsNothing);
      expect(find.text('My smart badge'), findsNothing);
      // No notification bell for a guest.
      expect(find.byTooltip('Notifications'), findsNothing);
    });

    testWidgets('signed-in visitor sees the bell + visitor tiles, no prompt',
        (tester) async {
      await _pump(tester, controller: _SignedInController());

      expect(find.byTooltip('Notifications'), findsOneWidget);
      expect(find.text('My area'), findsOneWidget);
      expect(find.text('My smart badge'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Sign in'), findsNothing);
    });

    testWidgets('unread badge is hidden when the count is 0', (tester) async {
      await _pump(tester, controller: _SignedInController());
      expect(tester.widget<Badge>(find.byType(Badge)).isLabelVisible, isFalse);
    });

    testWidgets('unread badge shows the count when greater than 0',
        (tester) async {
      await _pump(tester, controller: _SignedInController(), unread: 3);
      final badge = tester.widget<Badge>(find.byType(Badge));
      expect(badge.isLabelVisible, isTrue);
      expect((badge.label! as Text).data, '3');
    });

    testWidgets('tapping a nav destination navigates to its route',
        (tester) async {
      await _pump(tester, controller: _GuestController());

      // The KSA nav shows labels on the active tab only — tap by icon.
      await tester.tap(find.byIcon(Icons.calendar_today_outlined));
      await tester.pumpAndSettle();
      expect(find.text('SESSIONS'), findsOneWidget);
    });

    testWidgets('renders right-to-left in Arabic', (tester) async {
      await _pump(
        tester,
        controller: _GuestController(),
        locale: const Locale('ar'),
      );

      expect(find.text('المتحدثون'), findsOneWidget);
      expect(
        Directionality.of(tester.element(find.text('المتحدثون'))),
        TextDirection.rtl,
      );
    });
  });
}
