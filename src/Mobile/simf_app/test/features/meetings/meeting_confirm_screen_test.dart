import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/meetings/meeting_confirm_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The other party's one-tap confirm surface, reached from a MeetingRequested
/// notification deep link. Eligibility and state are enforced server-side, so
/// the screen's whole job is to map 403 / 409 / other onto distinct copy and to
/// swap to the summary on success — that mapping is what these tests pin.
class _FakeDelegationsRepo implements DelegationsRepository {
  _FakeDelegationsRepo({this.summary, this.status});

  final DelegationMeetingSummary? summary;
  final int? status;
  int confirmCalls = 0;

  @override
  Future<DelegationMeetingSummary> confirmMeeting(String requestId) async {
    confirmCalls++;
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return summary!;
  }

  @override
  Future<Delegations> getDelegations() => throw UnimplementedError();

  @override
  Future<List<DelegationSlot>> getAvailableSlots(int countryId) =>
      throw UnimplementedError();

  @override
  Future<void> submitMeetingRequest({
    required String targetCountryCode,
    required int attendeeCount,
    required String subject,
    DateTime? slotStart,
    DateTime? slotEnd,
  }) =>
      throw UnimplementedError();
}

Future<void> _pump(
  WidgetTester tester, {
  required DelegationsRepository repo,
  String requestId = 'req-1',
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        delegationsRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: MeetingConfirmScreen(requestId: requestId),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MeetingConfirmScreen (bi-meeting confirm)', () {
    testWidgets('renders the intro + confirm CTA for a valid request id',
        (tester) async {
      await _pump(tester, repo: _FakeDelegationsRepo());

      expect(
        find.text('Tap to confirm this meeting with the other party.'),
        findsOneWidget,
      );
      expect(find.text('Confirm meeting'), findsWidgets);
    });

    testWidgets('an empty request id shows the not-found state and never calls'
        ' the API', (tester) async {
      final repo = _FakeDelegationsRepo();
      await _pump(tester, repo: repo, requestId: '');

      expect(find.text('Meeting not found'), findsOneWidget);
      expect(repo.confirmCalls, 0);
    });

    testWidgets('confirming swaps to the summary (both parties + subject)',
        (tester) async {
      final repo = _FakeDelegationsRepo(
        summary: DelegationMeetingSummary(
          requestingCountry: 'Saudi Arabia',
          targetCountry: 'Egypt',
          subject: 'Naval logistics',
          slotStart: DateTime.utc(2026, 11, 3, 9),
        ),
      );
      await _pump(tester, repo: repo);

      await tester.tap(find.text('Confirm meeting').last);
      await tester.pumpAndSettle();

      expect(repo.confirmCalls, 1);
      expect(find.text('Meeting confirmed'), findsOneWidget);
      expect(find.text('Saudi Arabia — Egypt'), findsOneWidget);
      expect(find.text('Naval logistics'), findsOneWidget);
    });

    testWidgets('409 says the meeting is not awaiting confirmation',
        (tester) async {
      await _pump(tester, repo: _FakeDelegationsRepo(status: 409));

      await tester.tap(find.text('Confirm meeting').last);
      await tester.pumpAndSettle();

      expect(
        find.text('This meeting is not awaiting confirmation'),
        findsOneWidget,
      );
    });

    testWidgets('403 says the caller is not the other party', (tester) async {
      await _pump(tester, repo: _FakeDelegationsRepo(status: 403));

      await tester.tap(find.text('Confirm meeting').last);
      await tester.pumpAndSettle();

      // 403 maps to the shared delegation "not allowed" copy, NOT the generic
      // failure — the distinction is the whole point of the switch.
      expect(
        find.text('Could not confirm the meeting. Try again.'),
        findsNothing,
      );
    });

    testWidgets('any other failure shows the generic retry copy',
        (tester) async {
      await _pump(tester, repo: _FakeDelegationsRepo(status: 500));

      await tester.tap(find.text('Confirm meeting').last);
      await tester.pumpAndSettle();

      expect(
        find.text('Could not confirm the meeting. Try again.'),
        findsOneWidget,
      );
    });

    testWidgets('renders in Arabic', (tester) async {
      await _pump(
        tester,
        repo: _FakeDelegationsRepo(),
        locale: const Locale('ar'),
      );

      expect(find.text('تأكيد الاجتماع'), findsWidgets);
    });
  });
}
