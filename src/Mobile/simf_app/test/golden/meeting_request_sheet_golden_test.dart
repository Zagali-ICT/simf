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
/// field (hint اكتب الموضوع), اختيار التاريخ row of the speaker's available day
/// cards, اختيار الوقت row of that day's time-slot chips (a day is pre-selected
/// so both rows render), and the full-width gold ارسال الطلب button — sourced
/// from the speaker's REAL availability slots (D-709). RTL.

// Two event days of real slots (20 Nov: 09:00 + 10:00, 21 Nov: 09:00). Local →
// UTC so the sheet's toLocal() round-trips to the same day/time deterministically.
final List<SpeakerSlot> _goldenSlots = <SpeakerSlot>[
  SpeakerSlot(
    start: DateTime(2026, 11, 20, 9).toUtc(),
    end: DateTime(2026, 11, 20, 9, 30).toUtc(),
  ),
  SpeakerSlot(
    start: DateTime(2026, 11, 20, 10).toUtc(),
    end: DateTime(2026, 11, 20, 10, 30).toUtc(),
  ),
  SpeakerSlot(
    start: DateTime(2026, 11, 21, 9).toUtc(),
    end: DateTime(2026, 11, 21, 9, 30).toUtc(),
  ),
];

class _FakeRepo implements SpeakersRepository {
  _FakeRepo();

  @override
  Future<List<SpeakerSummary>> getSpeakers() async => const <SpeakerSummary>[];

  @override
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) async =>
      _goldenSlots;

  @override
  Future<SpeakerDetail> getSpeaker(String id) => throw UnimplementedError();

  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStart,
    DateTime? slotEnd,
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
          speakersRepositoryProvider.overrideWithValue(_FakeRepo()),
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
                  baseUrl: 'http://test.local/api/v1',
                  l10n: AppL10n.of(ctx),
                ),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    // Pre-select the first day so the time-slot chips render alongside the day
    // cards (the frame shows both rows).
    await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(MeetingRequestSheet),
      matchesGoldenFile('goldens/meeting_request_sheet_1776-5036.png'),
    );
  });
}
