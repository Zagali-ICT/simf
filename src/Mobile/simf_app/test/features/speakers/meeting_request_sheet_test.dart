import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_sheet.dart';
import 'package:simf_app/features/speakers/widgets/meeting_slot_pickers.dart';

// Two local days of real slots: 2026-07-10 (09:00 + 10:00) and 2026-07-11
// (09:00). Built as local times → UTC so the sheet's toLocal() round-trips to
// the same day/time regardless of the test machine's timezone.
final List<SpeakerSlot> _twoDaySlots = <SpeakerSlot>[
  SpeakerSlot(
    startUtc: DateTime(2026, 7, 10, 9).toUtc(),
    endUtc: DateTime(2026, 7, 10, 9, 30).toUtc(),
  ),
  SpeakerSlot(
    startUtc: DateTime(2026, 7, 10, 10).toUtc(),
    endUtc: DateTime(2026, 7, 10, 10, 30).toUtc(),
  ),
  SpeakerSlot(
    startUtc: DateTime(2026, 7, 11, 9).toUtc(),
    endUtc: DateTime(2026, 7, 11, 9, 30).toUtc(),
  ),
];

/// Fake repo: two speakers for the bilateral picker; a configurable slot list;
/// records the meeting-request submit so a test can assert the sent slot.
class _FakeRepo implements SpeakersRepository {
  _FakeRepo({this.slots = const <SpeakerSlot>[], this.failSubmitStatus});

  final List<SpeakerSlot> slots;
  // When set, submitMeetingRequest throws an ApiFailure with this HTTP status
  // (e.g. 403 for the VIP-only gate) so the failure mapping can be tested.
  final int? failSubmitStatus;

  int submitCalls = 0;
  String? lastSpeakerId;
  String? lastSubject;
  DateTime? lastSlotStartUtc;
  DateTime? lastSlotEndUtc;

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
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) async => slots;

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
    if (failSubmitStatus != null) {
      throw ApiFailure(
        code: 'x',
        message: 'fail',
        httpStatus: failSubmitStatus,
      );
    }
    submitCalls++;
    lastSpeakerId = speakerId;
    lastSubject = subject;
    lastSlotStartUtc = slotStartUtc;
    lastSlotEndUtc = slotEndUtc;
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

    testWidgets("presents the speaker's REAL available days + that day's slots "
        '(D-709 — not a free grid)', (tester) async {
      await _pump(tester, speakerId: 's1', repo: _FakeRepo(slots: _twoDaySlots));

      // Two distinct days carry slots → two day cards (10th + 11th).
      expect(find.byType(MeetingDayCard), findsNWidgets(2));
      // Time chips only appear once a day is picked.
      expect(find.byType(MeetingTimeChip), findsNothing);
      await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
      await tester.pumpAndSettle();
      // The 10th offers two slots → two chips.
      expect(find.byType(MeetingTimeChip), findsNWidgets(2));
    });

    testWidgets('no availability → shows the no-slots notice and can still send '
        'the request subject-only', (tester) async {
      final repo = _FakeRepo();
      await _pump(tester, speakerId: 's1', repo: repo);

      expect(find.text('No meeting slots available right now'), findsOneWidget);
      expect(find.byType(MeetingDayCard), findsNothing);

      await tester.enterText(find.byType(TextField), 'Naval cooperation');
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();

      expect(repo.submitCalls, 1);
      expect(repo.lastSubject, 'Naval cooperation');
      // No slot picked (none offered) → the request carries no slot.
      expect(repo.lastSlotStartUtc, isNull);
      expect(repo.lastSlotEndUtc, isNull);
    });

    testWidgets("submitting a picked real slot sends that slot's start + end",
        (tester) async {
      final repo = _FakeRepo(slots: _twoDaySlots);
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
      // The second slot on the 10th (10:00 local) was sent verbatim.
      expect(repo.lastSlotStartUtc, DateTime(2026, 7, 10, 10).toUtc());
      expect(repo.lastSlotEndUtc, DateTime(2026, 7, 10, 10, 30).toUtc());
    });

    testWidgets('bilateral flow — picking a speaker loads ITS slots and the '
        'picked slot is sent for that speaker', (tester) async {
      final repo = _FakeRepo(slots: _twoDaySlots);
      await _pump(tester, speakerId: null, repo: repo);

      // Pick a speaker → its real slots load into day cards.
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Dr. Sarah Al-Otaibi').last);
      await tester.pumpAndSettle();
      expect(find.byType(MeetingDayCard), findsNWidgets(2));

      await tester.enterText(find.byType(TextField), 'Naval cooperation');
      await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey<String>('meeting-time-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();

      expect(repo.submitCalls, 1);
      expect(repo.lastSpeakerId, 's1');
      expect(repo.lastSlotStartUtc, DateTime(2026, 7, 10, 9).toUtc());
    });

    testWidgets('a 403 on submit surfaces the VIP-only message', (tester) async {
      final repo = _FakeRepo(slots: _twoDaySlots, failSubmitStatus: 403);
      await _pump(tester, speakerId: 's1', repo: repo);

      await tester.enterText(find.byType(TextField), 'Naval cooperation');
      await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey<String>('meeting-time-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();

      expect(
        find.text('Booking a meeting slot is for VIP guests only'),
        findsOneWidget,
      );
    });
  });
}
