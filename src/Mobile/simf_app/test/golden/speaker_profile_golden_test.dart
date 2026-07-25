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
import 'package:simf_app/features/speakers/speaker_profile_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Speaker-profile screen against Figma frame **908:2110**
/// (ملف المتحدث / "About Speaker"). Compare to the frame:
///   flutter test --update-goldens test/golden/speaker_profile_golden_test.dart
///
/// Proves the two-line header (the nationality flag leading the white name over
/// the beige rank, circled back chevron — Figma 1327:3461), the 125px white
/// avatar ringed gold (anchor placeholder), the four CV pills in one row — the
/// active نبذة عنه pill gold on the **right**, the other three border-only (no
/// fill) running to the left — the navy #192B41 CV card with right-aligned white
/// body text, and the **text-only** gold طلب مقابلة CTA (Figma 1049:2302 has no
/// leading icon). RTL throughout.
///
/// Fixed data only, so the PNG is stable. Known golden-env artifacts (NOT layout
/// defects): the avatar photo is Image.network → the anchor SVG placeholder (no
/// HTTP in tests); the nationality flag (🇸🇦) is a colour-emoji glyph → renders
/// as tofu (no colour-emoji font loaded), but its position (leading the name) is
/// verifiable; the FilledButton label may render with reduced Arabic glyph
/// coverage in the headless env — the string is asserted correct by the widget
/// test, and it renders on device.

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

SpeakerDetail _speaker() => const SpeakerDetail(
      id: 's1',
      name: 'Rashed Al-Subaie',
      nameArabic: 'راشد بن طلال السبيعي',
      rank: 'القبطان البحري',
      countryId: 682,
      countryNameEn: 'Saudi Arabia',
      countryNameAr: 'المملكة العربية السعودية',
      bio: 'A naval commander with over 15 years of experience.',
      bioArabic:
          'قائد بحري بخبرة تتجاوز 15 عامًا في مجال الملاحة والأمن البحري، شارك '
          'في عدة مهام إقليمية ودولية وله إسهامات في تطوير أنظمة السلامة البحرية '
          'والتدريب الميداني.',
      qualifications: 'Naval Academy graduate.',
      qualificationsArabic: 'خريج الكلية البحرية الملكية مع مرتبة الشرف.',
      trainingExperience: 'Joint maritime exercises.',
      trainingExperienceArabic: 'قاد عدة تمارين بحرية مشتركة إقليمية ودولية.',
      awards: 'Order of merit.',
      awardsArabic: 'وسام الاستحقاق البحري من الدرجة الأولى.',
      allowsMeetingRequests: true,
      allowsDataSharing: false,
      displayOrder: 0,
      sessions: <SpeakerSession>[],
    );

class _FakeSpeakersRepo implements SpeakersRepository {
  @override
  Future<List<SpeakerSummary>> getSpeakers() => throw UnimplementedError();
  @override
  Future<SpeakerDetail> getSpeaker(String id) async => _speaker();
  @override
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) async =>
      const <SpeakerSlot>[];
  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStart,
    DateTime? slotEnd,
  }) =>
      throw UnimplementedError();
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Speaker-profile @375x812 — Figma 908:2110 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/speakers/s1',
      routes: <RouteBase>[
        GoRoute(
          path: '/speakers/:speakerId',
          name: RouteNames.speakerProfile,
          builder: (_, state) => SpeakerProfileScreen(
            speakerId: state.pathParameters['speakerId'] ?? '',
          ),
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
      find.byType(SpeakerProfileScreen),
      matchesGoldenFile('goldens/speaker_profile_908-2110.png'),
    );
  });
}
