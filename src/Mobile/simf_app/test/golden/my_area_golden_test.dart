@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_app/features/myarea/my_area_screen.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of My Area (منطقتي) against Figma frame **213:963 / 758:1283**
/// (Approved visitor dashboard).
///   flutter test --update-goldens test/golden/my_area_golden_test.dart
///
/// Renders the identity card (initials avatar — the authenticated photo Dio
/// yields no HTTP in tests), the two share pills, the الإحصائيات stat tiles
/// (3 meetings / 2 saved), the جدولي اليوم schedule, and the المزيد rows.

class _SignedIn extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(
        Session(
          accessToken: 'A',
          refreshToken: 'R',
          accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
          user: CurrentUser(
            id: 'u1',
            email: 'v@example.sa',
            displayName: 'رائد السالم',
            appRole: AppRole.visitor,
            preferredLanguage: PreferredLanguage.fromJson('ar'),
            registrationStatus: RegistrationStatus.approved,
          ),
        ),
      );
}

MyAreaDashboard _dashboard() => MyAreaDashboard(
      identity: const MyAreaIdentity(
        fullNameAr: 'رائد السالم',
        fullNameEn: 'Raed Al-Salem',
        qrId: 'ABC123',
        tierNameAr: 'VIP',
        tierNameEn: 'VIP',
      ),
      counters: const MyAreaCounters(bookedSessionsCount: 6, meetingsCount: 3),
      todaySchedule: <MyAreaScheduleItem>[
        MyAreaScheduleItem(
          kind: 'Session',
          start: DateTime(2026, 11, 23, 9),
          titleEn: 'Opening session',
          titleAr: 'الجلسة الافتتاحية',
          status: 'Approved',
          sessionId: 's1',
          hallNameAr: 'القاعة الرئيسية',
        ),
        MyAreaScheduleItem(
          kind: 'Meeting',
          start: DateTime(2026, 11, 23, 11),
          titleEn: 'Bilateral meeting',
          titleAr: 'لقاء ثنائي',
          status: 'Approved',
        ),
      ],
    );

class _FakeRepo implements MyAreaRepository {
  @override
  Future<MyAreaDashboard> getDashboard() async => _dashboard();

  @override
  Future<String> getContactCardVcf() async => 'BEGIN:VCARD\r\nEND:VCARD\r\n';

  @override
  Future<String> getCalendarIcs() async =>
      'BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n';

  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async =>
      true;
}

class _FakeFavourites extends SessionFavouritesController {
  @override
  Future<Set<String>> build() async => <String>{'a', 'b'};
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('My Area @375x1400 — Figma 213:963 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 1400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/my-area',
      routes: <RouteBase>[
        GoRoute(
          path: '/my-area',
          name: RouteNames.myArea,
          builder: (_, __) => const MyAreaScreen(),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          authControllerProvider.overrideWith(_SignedIn.new),
          myAreaRepositoryProvider.overrideWithValue(_FakeRepo()),
          sessionFavouritesProvider.overrideWith(_FakeFavourites.new),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          routerConfig: router,
          locale: const Locale('ar'),
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

    await expectLater(
      find.byType(MyAreaScreen),
      matchesGoldenFile('goldens/my_area_213-963.png'),
    );
  });
}
