@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/data/session_calendar.dart';
import 'package:simf_app/features/sessions/data/session_detail_repository.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/session_detail_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Session-detail screen against Figma frame **889:2450**
/// (تفاصيل الجلسة, Approved account with a held reservation). Compare to the frame:
///   flutter test --update-goldens test/golden/session_detail_golden_test.dart
///
/// Richest representative state — signed-in + a held assigned-seat reservation +
/// a live feed — so the frame shows: the header card (title + gold code badge +
/// meta), the two header action buttons (ملخص الجلسة + رابط الجلسة), the
/// description card, the speaker card, the ask-the-host card (enabled because the
/// caller has joined), the booking card (مقعدي · الصف B · مقعد 12 · بانتظار
/// الموافقة · إلغاء الحجز) and the أضف إلى تقويمي / تذكير CTAs. RTL throughout.
///
/// Owner 2026-07-14 — the two header actions are now STATE-GATED (superseding the
/// 2026-06-30 "always both active"). This fixture is an UPCOMING session, so BOTH
/// render greyed/inactive: ملخص الجلسة (no summary before the session ends) and
/// رابط الجلسة (the feed is not live yet). The active styling is covered by the
/// widget test (session_detail_body_test.dart), which drives an ended session
/// (summary active) and a live broadcast (live active).
///
/// The provider wiring + fakes mirror the proven widget test
/// (test/features/sessions/session_detail_screen_test.dart). Rendered values
/// derive from fixed data; the meta clock/day come from `start.toLocal()`, so
/// they are fixed per host timezone — regenerate on the same host as the other
/// goldens (the maintainer's UTC+3 box), as with every golden here.
///
/// D-714 (item 12, GAP-2) — the one time-derived value on the display path: the
/// ask card reads the pre-session label ("اطرح سؤالاً قبل الجلسة") while the
/// session is upcoming (`now < start`, the case here — an event in Nov 2026)
/// and reverts to "اسأل المحاور" once it has started. This golden therefore
/// captures the pre-session state; regenerate it before the event date.
///
/// Known golden-env artifacts (NOT layout/production defects): the speaker photo
/// is Image.network → placeholder in tests (no HTTP); the country flag is an
/// emoji glyph → tofu (no colour-emoji font loaded); and the two bottom CTA
/// labels (أضف إلى تقويمي / تذكير) render as tofu because Material
/// FilledButton/OutlinedButton resolve their label font to a family without
/// Arabic coverage and the headless test env has no system Arabic fallback. The
/// strings themselves are correct — the widget test asserts
/// `find.widgetWithText(FilledButton, 'أضف إلى تقويمي')` — and they render on
/// device. The golden proves layout/structure/colour/RTL, not glyph coverage.

SessionDetail _detail() => SessionDetail(
      id: 's1',
      code: 'OP-01',
      displayOrder: 2, // D-567 — gold badge shows the day-ordinal "02"
      title: 'Opening Session',
      titleArabic: 'الجلسة الافتتاحية',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      start: DateTime.utc(2026, 11, 23, 6),
      end: DateTime.utc(2026, 11, 23, 7, 30),
      speakers: <SessionSpeaker>[
        const SessionSpeaker(
          id: 'sp1',
          name: 'Dr. Ali Al-Harbi',
          nameArabic: 'د. علي الحربي',
          displayOrder: 0,
          role: SessionSpeakerRole.speaker,
          title: 'أستاذ الدراسات البحرية',
          countryNameEn: 'Saudi Arabia',
          countryNameAr: 'المملكة العربية السعودية',
          countryId: 682,
        ),
      ],
      description: 'Opening address of the forum.',
      descriptionArabic:
          'كلمة افتتاح الملتقى البحري السعودي الدولي واستعراض محاور الدورة '
          'الحالية وأبرز المشاركين والرعاة.',
      categoryName: 'Main Session',
      categoryNameArabic: 'جلسة رئيسية',
      liveStreamUrl: 'https://youtu.be/abcdefghijk',
    );

// The caller's own held assigned seat (a pending booking).
const _mySeatCell = SeatCell(
  reservationId: 'r1',
  rowLabel: 'B',
  seatNumber: 12,
  kind: SeatReservationKind.userBooking,
);

SessionSeatMap _seatMap() => SessionSeatMap(
      rowLabels: const <String>['A', 'B'],
      seatsPerRow: 12,
      reservedCells: const <SeatCell>[],
      activeReservedCount: 0,
      hallCapacity: 24,
      myCell: _mySeatCell,
      mode: SeatSelectionMode.assignedSeat,
    );

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
      accessTokenExpiresAt: DateTime.utc(2099, 1, 1),
      user: _visitor(),
    );

class _SignedInController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
}

class _FakeDetailRepo implements SessionDetailRepository {
  @override
  Future<SessionDetail> getDetail(String sessionId) async => _detail();

  @override
  Future<MySeat?> getMySeat(String sessionId) async => null;
}

/// Returns the populated seat map; the mutating methods are never invoked in a
/// static render (no taps).
class _FakeSeatRepo implements SeatMapRepository {
  @override
  Future<SessionSeatMap> getSeatMap(String sessionId) async => _seatMap();

  @override
  Future<MyReservation> joinOpenSeating(String sessionId) =>
      throw UnimplementedError();

  @override
  Future<MyReservation> reserveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) =>
      throw UnimplementedError();

  @override
  Future<MyReservation> reserveRandom(String sessionId) =>
      throw UnimplementedError();

  @override
  Future<void> releaseMine(String sessionId) async {}

  @override
  Future<MyReservation> moveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) =>
      throw UnimplementedError();
}

class _FakeCalendar implements SessionCalendar {
  @override
  Future<bool> addSession(SessionDetail detail, {required bool isArabic}) async =>
      true;
}

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Session-detail @375x1500 — Figma 889:2450 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1500);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/sessions/s1',
      routes: <RouteBase>[
        GoRoute(
          path: '/sessions/:sessionId',
          name: RouteNames.sessionDetail,
          builder: (_, state) => SessionDetailScreen(
            sessionId: state.pathParameters['sessionId'] ?? '',
          ),
        ),
        // Routes the screen can push (never fired in a static render).
        GoRoute(
          path: '/speakers/:speakerId',
          name: RouteNames.speakerProfile,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/sessions/:sessionId/my-seat',
          name: RouteNames.mySeat,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/sessions/:sessionId/pick-seat',
          name: RouteNames.seatPicker,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/live',
          name: RouteNames.liveBroadcast,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/ai-summary',
          name: RouteNames.aiSummary,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/live/question',
          name: RouteNames.sendQuestion,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_config),
          sessionDetailRepositoryProvider
              .overrideWithValue(_FakeDetailRepo()),
          seatMapRepositoryProvider.overrideWithValue(_FakeSeatRepo()),
          sessionCalendarProvider.overrideWithValue(_FakeCalendar()),
          authControllerProvider.overrideWith(_SignedInController.new),
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
      find.byType(SessionDetailScreen),
      matchesGoldenFile('goldens/session_detail_889-2450.png'),
    );
  });
}
