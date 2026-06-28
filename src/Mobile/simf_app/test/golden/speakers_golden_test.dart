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
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/speakers_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden-render harness (owner 2026-06-28, verification option 2): renders a
/// screen at its exact Figma frame size with the real brand fonts loaded, so the
/// generated PNG can be compared pixel-for-pixel against the Figma frame. Run:
///   flutter test --update-goldens test/golden/speakers_golden_test.dart
/// then open test/golden/goldens/*.png and compare to the Figma screenshot.

// Realistic Arabic speakers matching Figma 908:1744.
const _speakers = <SpeakerSummary>[
  SpeakerSummary(
    id: 's1',
    name: 'Rashed Al-Subaie',
    nameArabic: 'راشد بن طلال السبيعي',
    displayOrder: 0,
    rank: 'القبطان البحري · RSNF',
    countryId: 682,
  ),
  SpeakerSummary(
    id: 's2',
    name: 'Mohammed Al-Otaibi',
    nameArabic: 'محمد بن فهد العتيبي',
    displayOrder: 1,
    rank: 'القبطان البحري · GAMI · RSNF',
    countryId: 682,
  ),
  SpeakerSummary(
    id: 's3',
    name: 'Abdullah Al-Tuwaijri',
    nameArabic: 'عبدالله إبراهيم التويجري',
    displayOrder: 2,
    rank: 'العميد ركن · المضيف',
    countryId: 682,
  ),
  SpeakerSummary(
    id: 's4',
    name: 'Mohammed Al-Otaibi',
    nameArabic: 'محمد بن فهد العتيبي',
    displayOrder: 3,
    rank: 'العميد البحري',
    countryId: 682,
  ),
];

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

class _FakeSpeakersRepo implements SpeakersRepository {
  @override
  Future<List<SpeakerSummary>> getSpeakers() async => _speakers;
  @override
  Future<SpeakerDetail> getSpeaker(String id) => throw UnimplementedError();
  @override
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) =>
      throw UnimplementedError();
  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStartUtc,
    DateTime? slotEndUtc,
  }) =>
      throw UnimplementedError();
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Speakers @375x939 — Figma 908:1744 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 939);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/speakers',
      routes: <RouteBase>[
        GoRoute(
          path: '/speakers',
          name: RouteNames.speakers,
          builder: (_, __) => const SpeakersScreen(),
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
          speakersRepositoryProvider.overrideWithValue(_FakeSpeakersRepo()),
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
      find.byType(SpeakersScreen),
      matchesGoldenFile('goldens/speakers_908-1744.png'),
    );
  });
}
