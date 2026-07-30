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
    start: DateTime(2026, 7, 10, 9).toUtc(),
    end: DateTime(2026, 7, 10, 9, 30).toUtc(),
  ),
  SpeakerSlot(
    start: DateTime(2026, 7, 10, 10).toUtc(),
    end: DateTime(2026, 7, 10, 10, 30).toUtc(),
  ),
  SpeakerSlot(
    start: DateTime(2026, 7, 11, 9).toUtc(),
    end: DateTime(2026, 7, 11, 9, 30).toUtc(),
  ),
];

/// Fake repo: two speakers for the bilateral picker; a configurable slot list;
/// records the meeting-request submit so a test can assert the sent slot.
class _FakeRepo implements SpeakersRepository {
  _FakeRepo({
    this.slots = const <SpeakerSlot>[],
    this.failSlots = false,
    this.failSubmitStatus,
    this.failSubmitCode = 'x',
    this.failSubmitMessage = 'fail',
  });

  final List<SpeakerSlot> slots;
  // G3 — the availability fetch itself fails (network / server). Distinct from an
  // empty [slots] list, which means the speaker genuinely has no free slot.
  // NOT final: a test flips it between calls to simulate the network recovering,
  // which is the only way to prove Retry actually re-fetches.
  bool failSlots;

  // G3 — how many times the availability fetch was attempted. Without this a
  // no-op Retry button would satisfy a test that only checks the button renders.
  int slotFetchCalls = 0;
  // When set, submitMeetingRequest throws an ApiFailure with this HTTP status
  // (e.g. 403 for the eligibility gate) so the failure mapping can be tested.
  final int? failSubmitStatus;

  // QA A26 — the envelope code + the server's already-localized message, so a
  // test can prove the sheet renders the SERVER's reason for a 409 instead of
  // collapsing every conflict onto one hardcoded (and usually wrong) string.
  final String failSubmitCode;
  final String failSubmitMessage;

  int submitCalls = 0;
  String? lastSpeakerId;
  String? lastSubject;
  DateTime? lastSlotStart;
  DateTime? lastSlotEnd;

  @override
  Future<List<SpeakerSummary>> getSpeakers() async => const <SpeakerSummary>[
        SpeakerSummary(
          id: 's1',
          name: 'Dr. Sarah Al-Otaibi',
          nameArabic: 'د. سارة العتيبي',
          displayOrder: 0,
          // A rank whose token ('admiral') is in NO speaker name, so a search
          // for it can only match via the rank branch of the picker predicate.
          rank: 'Rear Admiral',
        ),
        SpeakerSummary(
          id: 's2',
          name: 'Capt. Omar Nasser',
          nameArabic: 'الرائد عمر ناصر',
          displayOrder: 1,
        ),
      ];

  @override
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) async {
    slotFetchCalls++;
    if (failSlots) {
      throw const ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'Network is unreachable.',
      );
    }
    return slots;
  }

  @override
  Future<SpeakerDetail> getSpeaker(String id) => throw UnimplementedError();

  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStart,
    DateTime? slotEnd,
  }) async {
    if (failSubmitStatus != null) {
      throw ApiFailure(
        code: failSubmitCode,
        message: failSubmitMessage,
        httpStatus: failSubmitStatus,
      );
    }
    submitCalls++;
    lastSpeakerId = speakerId;
    lastSubject = subject;
    lastSlotStart = slotStart;
    lastSlotEnd = slotEnd;
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
              baseUrl: 'http://test.local/api/v1',
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

      // The picker label is shown; every speaker is a selectable row (D-745 —
      // photo + name + country, no longer a bare dropdown); the form is deferred.
      expect(find.text('Select speaker'), findsOneWidget);
      expect(find.text('Dr. Sarah Al-Otaibi'), findsOneWidget);
      expect(find.text('Capt. Omar Nasser'), findsOneWidget);
      expect(find.text('Subject'), findsNothing);

      // Tap a speaker row → the subject form appears.
      await tester.tap(find.text('Dr. Sarah Al-Otaibi'));
      await tester.pumpAndSettle();
      expect(find.text('Subject'), findsOneWidget);
    });

    testWidgets('the picker search filters the speaker rows by name and shows '
        'the no-matches hint when nothing matches (owner 2026-07-11)',
        (tester) async {
      await _pump(tester, speakerId: null);

      // The search field is present and both speakers show before any query.
      final search =
          find.byKey(const ValueKey<String>('meeting-speaker-search'));
      expect(search, findsOneWidget);
      expect(find.text('Dr. Sarah Al-Otaibi'), findsOneWidget);
      expect(find.text('Capt. Omar Nasser'), findsOneWidget);

      // Typing part of one name leaves only the matching row.
      await tester.enterText(search, 'omar');
      await tester.pumpAndSettle();
      expect(find.text('Capt. Omar Nasser'), findsOneWidget);
      expect(find.text('Dr. Sarah Al-Otaibi'), findsNothing);

      // Typing part of a RANK ("Rear Admiral") matches via the rank branch of
      // the predicate — no speaker NAME contains "admiral".
      await tester.enterText(search, 'admiral');
      await tester.pumpAndSettle();
      expect(find.text('Dr. Sarah Al-Otaibi'), findsOneWidget);
      expect(find.text('Capt. Omar Nasser'), findsNothing);

      // A query that matches nobody shows the shared no-matches hint.
      await tester.enterText(search, 'zzz-nobody');
      await tester.pumpAndSettle();
      expect(find.text('Capt. Omar Nasser'), findsNothing);
      expect(find.text('No matching speakers'), findsOneWidget);
    });

    testWidgets('the picker keeps the SELECTED speaker visible even when the '
        'search would filter it out, so the submit target is never hidden',
        (tester) async {
      await _pump(tester, speakerId: null);

      // Select Sarah (the form + her selection now target s1)…
      await tester.tap(find.text('Dr. Sarah Al-Otaibi'));
      await tester.pumpAndSettle();
      expect(find.text('Subject'), findsOneWidget);

      // …then search for Omar. Omar matches the query, but Sarah stays pinned
      // because she is the chosen target — the picker never contradicts the
      // speaker the form submits to.
      await tester.enterText(
        find.byKey(const ValueKey<String>('meeting-speaker-search')),
        'omar',
      );
      await tester.pumpAndSettle();
      expect(find.text('Capt. Omar Nasser'), findsOneWidget);
      expect(find.text('Dr. Sarah Al-Otaibi'), findsOneWidget);
    });

    testWidgets('from a speaker profile (speakerId set) shows no picker and the '
        'form immediately', (tester) async {
      await _pump(tester, speakerId: 's1');

      expect(find.text('Select speaker'), findsNothing);
      // No picker rows when the speaker is fixed by the profile flow.
      expect(find.text('Dr. Sarah Al-Otaibi'), findsNothing);
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

    testWidgets(
        'G3 — no availability shows the no-slots notice AND disables send, so no '
        'subject-only request is sent (supersedes D-767 R1)', (tester) async {
      final repo = _FakeRepo();
      await _pump(tester, speakerId: 's1', repo: repo);

      expect(find.text('No meeting slots available right now'), findsOneWidget);
      expect(find.byType(MeetingDayCard), findsNothing);
      // The load succeeded and returned nothing — that is NOT a load error, so
      // there is no retry offered here.
      expect(find.text('Retry'), findsNothing);

      await tester.enterText(
        find.byKey(const ValueKey<String>('meeting-subject')),
        'Naval cooperation',
      );
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();

      // The send button is disabled: the server would 409
      // SPEAKER_MEETING_NO_AVAILABILITY, so the tap must not reach the repo.
      expect(repo.submitCalls, 0);
      expect(repo.lastSubject, isNull);
    });

    testWidgets(
        'G3 — a FAILED slot fetch shows a load error + Retry, not the '
        '"no availability" notice', (tester) async {
      final repo = _FakeRepo(failSlots: true);
      await _pump(tester, speakerId: 's1', repo: repo);

      // A transient network failure must never be presented as the speaker
      // having no availability — that would be untrue and unactionable.
      expect(find.text('No meeting slots available right now'), findsNothing);
      expect(find.text('Could not load the list.'), findsOneWidget);
      expect(
        find.byKey(const ValueKey<String>('meeting-slots-retry')),
        findsOneWidget,
      );

      // Sending is still blocked (there is no slot to send).
      await tester.enterText(
        find.byKey(const ValueKey<String>('meeting-subject')),
        'Naval cooperation',
      );
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();
      expect(repo.submitCalls, 0);
    });

    testWidgets(
        'G3 — tapping Retry after a failed fetch re-loads the slots and '
        'un-blocks Send', (tester) async {
      // The catalogue (bi-meeting-lifecycle.md E2E-BML-013c) scripts this
      // recovery, so it needs a test that TAPS the button: asserting the button
      // merely renders would pass against a no-op or mis-wired onPressed.
      final repo = _FakeRepo(failSlots: true, slots: _twoDaySlots);
      await _pump(tester, speakerId: 's1', repo: repo);

      expect(repo.slotFetchCalls, 1);
      expect(find.text('Could not load the list.'), findsOneWidget);
      expect(find.byType(MeetingDayCard), findsNothing);

      // The network comes back, then the user taps Retry.
      repo.failSlots = false;
      await tester.tap(find.byKey(const ValueKey<String>('meeting-slots-retry')));
      await tester.pumpAndSettle();

      // It genuinely re-fetched, and the error state cleared.
      expect(repo.slotFetchCalls, 2);
      expect(find.text('Could not load the list.'), findsNothing);
      expect(find.byType(MeetingDayCard), findsWidgets);

      // And Send now works, which is the point of recovering.
      await _submitWithFirstSlot(tester);

      expect(repo.submitCalls, 1);
      expect(repo.lastSubject, 'Naval cooperation');
    });

    testWidgets("submitting a picked real slot sends that slot's start + end",
        (tester) async {
      final repo = _FakeRepo(slots: _twoDaySlots);
      await _pump(tester, speakerId: 's1', repo: repo);

      await tester.enterText(
        find.byKey(const ValueKey<String>('meeting-subject')),
        'Naval cooperation',
      );
      await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey<String>('meeting-time-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();

      expect(repo.submitCalls, 1);
      expect(repo.lastSubject, 'Naval cooperation');
      // The second slot on the 10th (10:00 local) was sent verbatim.
      expect(repo.lastSlotStart, DateTime(2026, 7, 10, 10).toUtc());
      expect(repo.lastSlotEnd, DateTime(2026, 7, 10, 10, 30).toUtc());
    });

    testWidgets('bilateral flow — picking a speaker loads ITS slots and the '
        'picked slot is sent for that speaker', (tester) async {
      final repo = _FakeRepo(slots: _twoDaySlots);
      await _pump(tester, speakerId: null, repo: repo);

      // Pick a speaker row → its real slots load into day cards.
      await tester.tap(find.text('Dr. Sarah Al-Otaibi'));
      await tester.pumpAndSettle();
      expect(find.byType(MeetingDayCard), findsNWidgets(2));

      await tester.enterText(
        find.byKey(const ValueKey<String>('meeting-subject')),
        'Naval cooperation',
      );
      await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey<String>('meeting-time-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();

      expect(repo.submitCalls, 1);
      expect(repo.lastSpeakerId, 's1');
      expect(repo.lastSlotStart, DateTime(2026, 7, 10, 9).toUtc());
    });

    testWidgets(
        'QA A28 — a 403 on submit describes the real eligibility rule, not the '
        'stale VIP-only copy', (tester) async {
      final repo = _FakeRepo(slots: _twoDaySlots, failSubmitStatus: 403);
      await _pump(tester, speakerId: 's1', repo: repo);

      await _submitWithFirstSlot(tester);

      expect(
        find.textContaining('not enabled for your account'),
        findsOneWidget,
      );
      expect(find.textContaining('VIP'), findsNothing);
    });

    testWidgets(
        'QA A26 — a 409 surfaces the SERVER reason, not a hardcoded '
        '"speaker does not accept meeting requests"', (tester) async {
      // A duplicate-pending / slot-already-taken conflict: before the fix EVERY
      // 409 was collapsed onto the "does not accept meeting requests" string, so
      // the user was told something flatly untrue and the API's own bilingual
      // text was discarded.
      final repo = _FakeRepo(
        slots: _twoDaySlots,
        failSubmitStatus: 409,
        failSubmitCode: 'SPEAKER_MEETING_REQUEST_INVALID',
        failSubmitMessage: 'That slot is no longer available.',
      );
      await _pump(tester, speakerId: 's1', repo: repo);

      await _submitWithFirstSlot(tester);

      expect(find.text('That slot is no longer available.'), findsOneWidget);
      expect(
        find.text('This speaker is not accepting meeting requests'),
        findsNothing,
      );
    });

    testWidgets(
        'QA A26 — a failure that never reached the server still shows localized '
        'copy, not the raw dio string', (tester) async {
      final repo = _FakeRepo(
        slots: _twoDaySlots,
        failSubmitStatus: null,
        failSubmitCode: ApiErrorCodes.clientNetwork,
        failSubmitMessage: 'Network is unreachable.',
      );
      await _pump(tester, speakerId: 's1', repo: repo);

      await _submitWithFirstSlot(tester);

      expect(find.text('Network is unreachable.'), findsNothing);
    });
  });
}

/// Fills the subject, picks the first day + first slot, and taps send.
Future<void> _submitWithFirstSlot(WidgetTester tester) async {
  await tester.enterText(
    find.byKey(const ValueKey<String>('meeting-subject')),
    'Naval cooperation',
  );
  await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
  await tester.pumpAndSettle();
  await tester.tap(find.byKey(const ValueKey<String>('meeting-time-0')));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Send request'));
  await tester.pumpAndSettle();
}
