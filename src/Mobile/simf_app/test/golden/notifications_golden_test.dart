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
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_app/features/notifications/notifications_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Notifications screen against Figma frame **758:2491**
/// (الإشعارات). Compare `goldens/notifications_758-2491.png` to the frame:
/// flutter test --update-goldens test/golden/notifications_golden_test.dart
///
/// Frame parity expected: the search box + الكل / جلسات / VIP filter chips and
/// مسح الكل action, a day header (here "date"), then borderless navyDeep cards
/// — each a 40px solid circular **per-kind** category mark (D-531: gold ticket
/// for an approved account, green check for a session reminder, green card for
/// a scheduled meeting, coral star for a VIP invitation, coral busy-mark for a
/// rejection), the bold title + body + "{time} · {day}" line, and the red
/// unread dot at the top inline-end corner. RTL throughout.
///
/// The screen auto-marks the inbox read on open (`_openInbox`), which would
/// strip the unread dots before the shot — so the fake repo throws from
/// `markAllRead` to preserve the populated unread frame for the parity
/// comparison.

NotificationItem _n({
  required String id,
  required String kind,
  required String titleAr,
  required String bodyAr,
  required NotificationSeverity severity,
}) =>
    NotificationItem(
      id: id,
      kind: kind,
      title: '',
      titleArabic: titleAr,
      body: '',
      bodyArabic: bodyAr,
      severity: severity,
      isRead: false,
      // Fixed, well in the past so the day label is the stable "d MMM" form
      // (not the relative اليوم / أمس that would drift with the run date).
      createdAt: DateTime.utc(2026, 6, 10, 6, 30),
    );

// One card per D-531 palette branch so the golden proves the per-kind styling.
final _items = <NotificationItem>[
  _n(
    id: '1',
    kind: 'AccountApproved',
    titleAr: 'تم اعتماد حسابك',
    bodyAr: 'تمت الموافقة على طلبك، شارتك الرقمية جاهزة الآن.',
    severity: NotificationSeverity.success,
  ),
  _n(
    id: '2',
    kind: 'SessionReminder',
    titleAr: 'تذكير بجلسة',
    bodyAr: 'تبدأ جلسة "مستقبل الأمن البحري" بعد ١٥ دقيقة في القاعة الرئيسية.',
    severity: NotificationSeverity.info,
  ),
  _n(
    id: '3',
    kind: 'MeetingScheduled',
    titleAr: 'تم تحديد موعد لقاء',
    bodyAr: 'تم تأكيد لقائك مع وفد الهيئة العامة للصناعات العسكرية.',
    severity: NotificationSeverity.success,
  ),
  _n(
    id: '4',
    kind: 'VipBroadcast',
    titleAr: 'دعوة VIP',
    bodyAr: 'أنت مدعو لحضور الجلسة الافتتاحية في المنصة الرئيسية.',
    severity: NotificationSeverity.info,
  ),
  _n(
    id: '5',
    kind: 'BookingRejected',
    titleAr: 'تعذّر تأكيد الحجز',
    bodyAr: 'لم يتوفر مقعد في الجلسة المطلوبة، يمكنك اختيار جلسة أخرى.',
    severity: NotificationSeverity.warning,
  ),
];

/// Keeps the populated unread frame for the parity shot — see the file note.
class _FakeNotificationsRepo implements NotificationsRepository {
  @override
  Future<List<NotificationItem>> getNotifications(
          {int skip = 0, int top = 50,}) async =>
      _items;

  @override
  Future<int> getUnreadCount() async => _items.where((n) => !n.isRead).length;

  @override
  Future<bool> markRead(String id) async => true;

  @override
  Future<bool> markAllRead() async =>
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
}

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Notifications @375x1000 — Figma 758:2491 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1000);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/notifications',
      routes: <RouteBase>[
        GoRoute(
          path: '/notifications',
          name: RouteNames.notifications,
          builder: (_, __) => const NotificationsScreen(),
        ),
        GoRoute(
          path: '/rate',
          name: RouteNames.rate,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_config),
          notificationsRepositoryProvider
              .overrideWithValue(_FakeNotificationsRepo()),
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
      find.byType(NotificationsScreen),
      matchesGoldenFile('goldens/notifications_758-2491.png'),
    );
  });
}
