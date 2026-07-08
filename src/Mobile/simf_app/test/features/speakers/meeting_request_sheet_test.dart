import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_sheet.dart';
import 'package:simf_app/features/speakers/widgets/meeting_slot_pickers.dart';

/// Fake repo: two speakers for the bilateral picker; records the meeting-request
/// submit so a test can assert the picked date + time were sent.
class _FakeRepo implements SpeakersRepository {
  _FakeRepo();

  int submitCalls = 0;
  String? lastSubject;
  DateTime? lastSlotStartUtc;

  @override
  Future<List<SpeakerSummary>> getSpeakers() async => const <SpeakerSummary>[
        SpeakerSummary(
          id: 's1',
          name: 'Dr. Sarah Al-Otaibi',
          nameArabic: 'د. سارة العتيبي',
          displayOrder: 0,
        ),
        SpeakerSummary(
          id: 's2',
          name: 'Capt. Omar Nasser',
          nameArabic: 'الرائد عمر ناصر',
          displayOrder: 1,
        ),
      ];

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
  }) async {
    submitCalls++;
    lastSubject = subject;
    lastSlotStartUtc = slotStartUtc;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required String? speakerId,
  SpeakersRepository? repo,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        speakersRepositoryProvider.overrideWithValue(repo ?? _FakeRepo()),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: Scaffold(
          body: Builder(
            builder: (ctx) => MeetingRequestSheet(
              speakerId: speakerId,
              defaultName: 'Raed',
              l10n: AppL10n.of(ctx),
              // Fixed clock so the generated day cards are deterministic.
              now: DateTime(2026, 7, 8),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MeetingRequestSheet', () {
    testWidgets(
        'bilateral entry (speakerId null) shows the speaker picker and defers '
        'the form until one is chosen', (tester) async {
      await _pump(tester, speakerId: null);

      // The picker label is shown; the subject form is deferred.
      expect(find.text('Select speaker'), findsOneWidget);
      expect(find.byType(DropdownButtonFormField<String>), findsOneWidget);
      expect(find.text('Subject'), findsNothing);

      // Pick a speaker → the subject form appears.
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Dr. Sarah Al-Otaibi').last);
      await tester.pumpAndSettle();
      expect(find.text('Subject'), findsOneWidget);
    });

    testWidgets('from a speaker profile (speakerId set) shows no picker and the '
        'form immediately', (tester) async {
      await _pump(tester, speakerId: 's1');

      expect(find.text('Select speaker'), findsNothing);
      expect(find.byType(DropdownButtonFormField<String>), findsNothing);
      expect(find.text('Subject'), findsOneWidget);
    });

    testWidgets('the date cards + time chips always show (Figma 1776:5036) — '
        'not gated on speaker availability slots', (tester) async {
      await _pump(tester, speakerId: 's1');

      expect(find.text('Choose the date'), findsOneWidget);
      expect(find.text('Choose the time'), findsOneWidget);
      // Seven upcoming day cards + nine standard time chips, regardless of slots.
      expect(find.byType(MeetingDayCard), findsNWidgets(7));
      expect(find.byType(MeetingTimeChip), findsNWidgets(9));
    });

    testWidgets('submitting sends the picked subject + date + time', (tester) async {
      final repo = _FakeRepo();
      await _pump(tester, speakerId: 's1', repo: repo);

      await tester.enterText(find.byType(TextField), 'Naval cooperation');
      await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey<String>('meeting-time-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();

      expect(repo.submitCalls, 1);
      expect(repo.lastSubject, 'Naval cooperation');
      // The picked day + time were combined into the requested slot start.
      expect(repo.lastSlotStartUtc, isNotNull);
    });
  });
}
