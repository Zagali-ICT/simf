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
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/meetings/meetings_screen.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/data/requests_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the VIP bilateral-meetings page (اللقاءات الثنائية) against
/// Figma frame **1408:9726** (D-745). Run:
///   flutter test --update-goldens test/golden/meetings_golden_test.dart
/// then open test/golden/goldens/meetings_1408-9726.png and compare to the frame.
///
/// Parity: back chevron + centred title, the طلب جديد (outline) / السجل (gold)
/// two-button row, then the approved-upcoming meeting cards — each with the kind
/// headline over the rank, the nationality flag badge, the speaker photo (anchor
/// placeholder in tests) + name (gold), and the slot date + clock. RTL.

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

// Two approved, future meetings: a speaker meeting (with a speaker photo + rank)
// and a delegation meeting (target-country flag, no speaker photo). Fixed future
// dates keep the "upcoming" filter + the card date deterministic.
final List<AppRequestItem> _meetings = <AppRequestItem>[
  AppRequestItem(
    kind: AppRequestKind.speakerMeeting,
    id: 's1',
    title: 'د. محمد العمري',
    titleArabic: 'د. محمد العمري',
    status: AppRequestStatus.accepted,
    eventDateUtc: DateTime.utc(2035, 6, 20, 7, 45),
    createdAt: DateTime.utc(2035, 6, 1),
    canCancel: false,
    subtitle: 'باحث بيئي',
    speakerId: 's1',
    countryId: 682,
  ),
  AppRequestItem(
    kind: AppRequestKind.delegationMeeting,
    id: 'd1',
    title: 'وفد فرنسا',
    titleArabic: 'وفد فرنسا',
    status: AppRequestStatus.accepted,
    eventDateUtc: DateTime.utc(2035, 6, 21, 10),
    createdAt: DateTime.utc(2035, 6, 2),
    canCancel: false,
    countryId: 250,
  ),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Bilateral meetings @375x760 — Figma 1408:9726 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 760);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/meetings',
      routes: <RouteBase>[
        GoRoute(
          path: '/meetings',
          name: RouteNames.meetings,
          builder: (_, __) => const MeetingsScreen(),
        ),
        GoRoute(
          path: '/requests',
          name: RouteNames.requests,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/speakers/:speakerId',
          name: RouteNames.speakerProfile,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_config),
          currentUserMeetingAccessProvider.overrideWith(
            (ref) => const MeetingAccess(speaker: true, delegation: true),
          ),
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
      find.byType(MeetingsScreen),
      matchesGoldenFile('goldens/meetings_1408-9726.png'),
    );
  });
}
