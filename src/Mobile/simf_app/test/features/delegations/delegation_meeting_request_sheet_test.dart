// Tests: A35 — the delegation request sheet showed a SPEAKER-specific error.
// A 409 was hard-mapped to "This speaker is not accepting meeting requests" and
// every 400 to "This delegation is not available for meetings", so the server's
// own bilingual reason never reached the delegate. The sheet now surfaces the
// envelope message and keeps the l10n strings only as the offline fallback.
//
// Tests: G3 (owner 2026-07-30, supersedes D-767 R1) — a request can no longer
// be
// sent when the target delegation has NO free slot: the send button is disabled
// and nothing reaches the repository. A FAILED slot fetch is a separate state
// (load error + Retry) so a network blip is never shown as "no availability".
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/delegations/widgets/delegation_meeting_request_sheet.dart';
import 'package:simf_app/features/speakers/widgets/meeting_slot_pickers.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// One local day of real slots (2026-07-10, 09:00 + 10:00) built as local times
// →
// A fixed zone, so the sheet's Saudi-time conversion round-trips on any test
// machine.
final List<DelegationSlot> _oneDaySlots = <DelegationSlot>[
  DelegationSlot(
    start: DateTime(2026, 7, 10, 9).toUtc(),
    end: DateTime(2026, 7, 10, 9, 30).toUtc(),
  ),
  DelegationSlot(
    start: DateTime(2026, 7, 10, 10).toUtc(),
    end: DateTime(2026, 7, 10, 10, 30).toUtc(),
  ),
];

/// Extends the concrete repository (its client field is library-private, so it
/// cannot be `implements`-ed) and never touches the injected client.
class _FakeDelegationsRepository extends DelegationsRepository {
  _FakeDelegationsRepository(
    super._client, {
    this.failure,
    this.slots = const <DelegationSlot>[],
    this.failSlots = false,
  });

  final ApiFailure? failure;

  /// The target delegation's free slots. Empty = it genuinely has none (G3).
  final List<DelegationSlot> slots;

  /// G3 — the availability fetch itself fails, which is NOT "no availability".
  /// NOT final: a test flips it between calls to simulate the network
  /// recovering, which is the only way to prove Retry actually re-fetches.
  bool failSlots;

  int submitCalls = 0;

  /// G3 — attempts at the availability fetch. Without this a no-op Retry would
  /// satisfy a test that only checks the button renders.
  int slotFetchCalls = 0;

  @override
  Future<List<DelegationSlot>> getAvailableSlots(int countryId) async {
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
  Future<void> submitMeetingRequest({
    required String targetCountryCode,
    required int attendeeCount,
    required String subject,
    DateTime? slotStart,
    DateTime? slotEnd,
  }) async {
    if (failure != null) {
      throw failure!;
    }
    submitCalls++;
  }
}

SimfApiClient _dummyClient() => SimfApiClient.build(
      config: const SimfDataConfig(
        baseUrl: 'https://example.invalid/api/v1',
        appKey: 'test',
        deviceType: SimfDeviceType.android,
      ),
      tokenSource: const NoAuthTokenSource(),
      currentLanguageCode: () => 'en',
    );

const _target = DelegationItem(
  countryId: 818,
  countryCode: 'EG',
  countryName: 'Egypt',
  countryNameArabic: 'مصر',
  memberCount: 12,
);

Future<void> _pump(
  WidgetTester tester,
  _FakeDelegationsRepository repository,
) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        delegationsRepositoryProvider.overrideWithValue(repository),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        supportedLocales: AppL10n.supportedLocales,
        home: Builder(
          builder: (context) => Scaffold(
            body: DelegationMeetingRequestSheet(
              country: _target,
              l10n: AppL10n.of(context),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

/// Fills the form, picks the first day + first slot, and taps send. G3 — a slot
/// is now mandatory, so every submit path goes through a real pick.
Future<void> _fillAndSend(WidgetTester tester) async {
  await tester.enterText(
    find.byKey(const ValueKey<String>('delegation-subject')),
    'Naval cooperation',
  );
  await tester.enterText(
    find.byKey(const ValueKey<String>('delegation-attendees')),
    '4',
  );
  await tester.tap(find.byKey(const ValueKey<String>('delegation-day-0')));
  await tester.pumpAndSettle();
  await tester.tap(find.byKey(const ValueKey<String>('delegation-time-0')));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Send request'));
  await tester.pumpAndSettle();
}

Future<void> _pumpAndSubmit(WidgetTester tester, ApiFailure failure) async {
  await _pump(
    tester,
    _FakeDelegationsRepository(
      _dummyClient(),
      failure: failure,
      slots: _oneDaySlots,
    ),
  );
  await _fillAndSend(tester);
}

void main() {
  testWidgets('A35 a 409 shows the server reason, not the speaker copy',
      (tester) async {
    await _pumpAndSubmit(
      tester,
      const ApiFailure(
        code: 'DELEGATION_MEETING_REQUEST_INVALID',
        message: 'A delegation cannot request a meeting with itself.',
        httpStatus: 409,
      ),
    );

    expect(
      find.text('A delegation cannot request a meeting with itself.'),
      findsOneWidget,
    );
    expect(
      find.text('This speaker is not accepting meeting requests'),
      findsNothing,
    );
  });

  testWidgets(
      'A35 a 400 shows the server reason, not the blanket delegation copy',
      (tester) async {
    await _pumpAndSubmit(
      tester,
      const ApiFailure(
        code: 'DELEGATION_MEETING_REQUEST_INVALID',
        message: 'Subject must be between 1 and 1000 characters.',
        httpStatus: 400,
      ),
    );

    expect(
      find.text('Subject must be between 1 and 1000 characters.'),
      findsOneWidget,
    );
    expect(
      find.text('This delegation is not available for meetings'),
      findsNothing,
    );
  });

  testWidgets('A35 an offline failure still falls back to the local message',
      (tester) async {
    // No httpStatus — the call never reached the server, so there is no
    // server-authored message to show.
    await _pumpAndSubmit(
      tester,
      const ApiFailure(code: 'NETWORK', message: 'SocketException'),
    );

    expect(find.text('Could not send the request. Try again.'), findsOneWidget);
  });

  testWidgets(
      'G3 — no availability shows the no-slots notice AND disables send, so no '
      'subject-only request is sent', (tester) async {
    final repository = _FakeDelegationsRepository(_dummyClient());
    await _pump(tester, repository);

    expect(find.text('No meeting slots available right now'), findsOneWidget);
    expect(find.byType(MeetingDayCard), findsNothing);
    // The load succeeded and returned nothing — not a load error, so no retry.
    expect(find.text('Retry'), findsNothing);

    await tester.enterText(
      find.byKey(const ValueKey<String>('delegation-subject')),
      'Naval cooperation',
    );
    await tester.enterText(
      find.byKey(const ValueKey<String>('delegation-attendees')),
      '4',
    );
    await tester.tap(find.text('Send request'));
    await tester.pumpAndSettle();

    // The server would 409 DELEGATION_MEETING_NO_AVAILABILITY, so the tap must
    // not reach the repository at all.
    expect(repository.submitCalls, 0);
  });

  testWidgets(
      'G3 — a FAILED slot fetch shows a load error + Retry, not the '
      '"no availability" notice', (tester) async {
    final repository =
        _FakeDelegationsRepository(_dummyClient(), failSlots: true);
    await _pump(tester, repository);

    expect(find.text('No meeting slots available right now'), findsNothing);
    expect(find.text('Could not load the list.'), findsOneWidget);
    expect(
      find.byKey(const ValueKey<String>('delegation-slots-retry')),
      findsOneWidget,
    );

    await tester.enterText(
      find.byKey(const ValueKey<String>('delegation-subject')),
      'Naval cooperation',
    );
    await tester.tap(find.text('Send request'));
    await tester.pumpAndSettle();
    expect(repository.submitCalls, 0);
  });

  testWidgets('G3 — with real slots the picked slot is submitted',
      (tester) async {
    final repository =
        _FakeDelegationsRepository(_dummyClient(), slots: _oneDaySlots);
    await _pump(tester, repository);

    expect(find.byType(MeetingDayCard), findsOneWidget);
    await _fillAndSend(tester);

    expect(repository.submitCalls, 1);
  });

  testWidgets(
      'G3 — tapping Retry after a failed fetch re-loads the slots and '
      'un-blocks Send', (tester) async {
    // mobile-delegation-request.md E2E-DELREQ-005 scripts this recovery, so it
    // needs a test that TAPS the button — asserting the button merely renders
    // would pass against a no-op or mis-wired onPressed.
    final repository = _FakeDelegationsRepository(
      _dummyClient(),
      failSlots: true,
      slots: _oneDaySlots,
    );
    await _pump(tester, repository);

    expect(repository.slotFetchCalls, 1);
    expect(find.text('Could not load the list.'), findsOneWidget);
    expect(find.byType(MeetingDayCard), findsNothing);

    // The network comes back, then the user taps Retry.
    repository.failSlots = false;
    await tester
        .tap(find.byKey(const ValueKey<String>('delegation-slots-retry')));
    await tester.pumpAndSettle();

    expect(repository.slotFetchCalls, 2);
    expect(find.text('Could not load the list.'), findsNothing);
    expect(find.byType(MeetingDayCard), findsOneWidget);

    // And Send now works, which is the point of recovering.
    await _fillAndSend(tester);
    expect(repository.submitCalls, 1);
  });
}
