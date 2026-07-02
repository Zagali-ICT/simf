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
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/data/requests_repository.dart';
import 'package:simf_app/features/requests/my_meetings_screen.dart';

import 'golden_fonts.dart';

/// Golden render of the My-Meetings screen against Figma frame **1701:9406**
/// (المقابلات, Approved attendee). Regenerate:
///   flutter test --update-goldens test/golden/my_meetings_golden_test.dart
///
/// Frame parity expected: the three equal-width status chips the sample data
/// yields (الكل gold-selected, مكتملة green, قيد الانتظار amber — each
/// "{label} ({count})"; مرفوضة is hidden while its bucket is empty), the
/// "جميع المقابلات (N)" total header, then navyDeep person cards — a 38px gold
/// initial avatar at the inline end, the counterpart name over the meeting type,
/// and a clock + "12 يناير – 10:30 PM" date line opposite a neutral "مؤكدة"
/// badge. RTL throughout.

AppRequestItem _meeting({
  required String id,
  required String nameAr,
  required AppRequestStatus status,
  AppRequestKind kind = AppRequestKind.speakerMeeting,
  String? subtitle,
}) =>
    AppRequestItem(
      kind: kind,
      id: id,
      title: nameAr,
      titleArabic: nameAr,
      status: status,
      subtitle: subtitle,
      // 19:30 UTC → 22:30 local (UTC+3) = "10:30 PM"; a fixed date so the card
      // date line reads "12 يناير – 10:30 PM" like the frame.
      eventDateUtc: DateTime.utc(2026, 1, 12, 19, 30),
      createdAt: DateTime.utc(2026, 1, 1),
      canCancel: false,
    );

// Mirrors the Figma 1701:9406 sample exactly — 4 accepted (مكتملة) + 2 pending
// (قيد الانتظار), 0 rejected — so the chip row renders the frame's three chips
// (الكل (6) / مكتملة (4) / قيد الانتظار (2)); مرفوضة is hidden while empty.
final _meetings = <AppRequestItem>[
  // Speaker meetings carry the speaker's rank (D-590) as the secondary line —
  // "باحث بيئي" like the frame; the delegation (m5) has no rank so it falls back
  // to its meeting-type headline.
  _meeting(
    id: 'm1',
    nameAr: 'د. ابراهيم الحامد',
    status: AppRequestStatus.accepted,
    subtitle: 'باحث بيئي',
  ),
  _meeting(
    id: 'm2',
    nameAr: 'أ. سارة المطيري',
    status: AppRequestStatus.accepted,
    subtitle: 'باحث بيئي',
  ),
  _meeting(
    id: 'm3',
    nameAr: 'م. خالد العتيبي',
    status: AppRequestStatus.accepted,
    subtitle: 'باحث بيئي',
  ),
  _meeting(
    id: 'm4',
    nameAr: 'د. نورة السبيعي',
    status: AppRequestStatus.accepted,
    subtitle: 'باحث بيئي',
  ),
  _meeting(
    id: 'm5',
    nameAr: 'مملكة الشرق',
    status: AppRequestStatus.pending,
    kind: AppRequestKind.delegationMeeting,
  ),
  _meeting(
    id: 'm6',
    nameAr: 'د. فهد القحطاني',
    status: AppRequestStatus.pending,
    subtitle: 'باحث بيئي',
  ),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('My-Meetings @375x812 — Figma 1701:9406 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/my-meetings',
      routes: <RouteBase>[
        GoRoute(
          path: '/my-meetings',
          name: RouteNames.myMeetings,
          builder: (_, __) => const MyMeetingsScreen(),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          myRequestsProvider.overrideWith((ref) async => _meetings),
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
      find.byType(MyMeetingsScreen),
      matchesGoldenFile('goldens/my_meetings_1701-9406.png'),
    );
  });
}
