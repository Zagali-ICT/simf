import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_sheet.dart';

/// Fake repo: two speakers for the bilateral picker, no free slots (the
/// topic-only path). submit is a no-op success.
class _FakeRepo implements SpeakersRepository {
  const _FakeRepo();

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
  }) async {}
}

Future<void> _pump(WidgetTester tester, {required String? speakerId}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        speakersRepositoryProvider.overrideWithValue(const _FakeRepo()),
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
  });
}
