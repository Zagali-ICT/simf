@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_sheet.dart';

import 'golden_fonts.dart';

/// Golden render of the "طلب مقابلة" meeting-request sheet against Figma frame
/// **1776:5036**. Compare to the frame:
///   flutter test --update-goldens test/golden/meeting_request_sheet_golden_test.dart
///
/// Parity: gold drag handle, right-aligned طلب مقابلة title, الموضوع subject
/// field (hint اكتب الموضوع), اختيار التاريخ row of upcoming day cards, اختيار
/// الوقت row of time chips, and the full-width gold ارسال الطلب button — the beige
/// sheet with a free date/time pick (owner 2026-07-08). RTL.

class _FakeRepo implements SpeakersRepository {
  const _FakeRepo();

  @override
  Future<List<SpeakerSummary>> getSpeakers() async => const <SpeakerSummary>[];

  @override
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) async =>
      const <SpeakerSlot>[];

  @override
  Future<SpeakerDetail> getSpeaker(String id) => throw UnimplementedError();

  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStartUtc,
    DateTime? slotEndUtc,
  }) async {}
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Meeting-request sheet @375x760 — Figma 1776:5036 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 760);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          speakersRepositoryProvider.overrideWithValue(const _FakeRepo()),
        ],
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          locale: const Locale('ar'),
          supportedLocales: AppL10n.supportedLocales,
          localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
            ...AppL10n.localizationsDelegates,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          // The sheet is presented over a beige-filled surface with a Material
          // ancestor (its caller is showModalBottomSheet, backgroundColor:
          // cardBeige) — replicate that here so the fields have their Material.
          home: Material(
            color: SimfTokens.cardBeige,
            child: SafeArea(
              child: Builder(
                builder: (ctx) => MeetingRequestSheet(
                  speakerId: 's1', // fixed speaker → no picker, form immediately
                  defaultName: 'Raed',
                  l10n: AppL10n.of(ctx),
                  now: DateTime(2026, 7, 8),
                ),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(MeetingRequestSheet),
      matchesGoldenFile('goldens/meeting_request_sheet_1776-5036.png'),
    );
  });
}
