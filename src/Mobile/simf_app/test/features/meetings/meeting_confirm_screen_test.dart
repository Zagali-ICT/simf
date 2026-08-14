// Tests: B8 — the delegation TARGET can DECLINE an approved meeting from the
// app (the screen only ever offered Confirm, so their single exit was an admin
// cancel); A30 — the screen's copy names the DELEGATION explicitly, so it is no
// longer confusable with the website's `/meeting/confirm?token=` speaker page.
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/meetings/meeting_confirm_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Records which endpoint the screen called and returns a canned summary, or
/// throws the configured [ApiFailure]. Extends the concrete repository (its
/// client field is library-private, so it cannot be `implements`-ed) and never
/// touches the injected client.
class _FakeDelegationsRepository extends DelegationsRepository {
  _FakeDelegationsRepository(super._client, {this.failure});

  final ApiFailure? failure;

  int confirmCalls = 0;
  int declineCalls = 0;
  String? lastRequestId;

  @override
  Future<DelegationMeetingSummary> confirmMeeting(String requestId) async {
    confirmCalls++;
    lastRequestId = requestId;
    if (failure != null) {
      throw failure!;
    }
    return _summary;
  }

  @override
  Future<DelegationMeetingSummary> declineMeeting(String requestId) async {
    declineCalls++;
    lastRequestId = requestId;
    if (failure != null) {
      throw failure!;
    }
    return _summary;
  }

  static const DelegationMeetingSummary _summary = DelegationMeetingSummary(
    requestingCountry: 'Egypt',
    targetCountry: 'Saudi Arabia',
    subject: 'Naval cooperation',
  );
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

Future<_FakeDelegationsRepository> _pump(
  WidgetTester tester, {
  String requestId = 'req-1',
  ApiFailure? failure,
}) async {
  final repository =
      _FakeDelegationsRepository(_dummyClient(), failure: failure);
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
        home: MeetingConfirmScreen(requestId: requestId),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return repository;
}

void main() {
  const confirmKey = ValueKey<String>('delegation-meeting-confirm');
  const declineKey = ValueKey<String>('delegation-meeting-decline');

  testWidgets('B8 offers both a confirm and a decline action', (tester) async {
    await _pump(tester);

    expect(find.byKey(confirmKey), findsOneWidget);
    expect(find.byKey(declineKey), findsOneWidget);
  });

  testWidgets('A30 the title names the DELEGATION meeting', (tester) async {
    await _pump(tester);

    // The website's anonymous speaker page keeps the generic "Confirm meeting";
    // this screen must say which meeting family it answers.
    expect(find.text('Confirm delegation meeting'), findsOneWidget);
  });

  testWidgets('B8 tapping decline calls the decline endpoint, not confirm',
      (tester) async {
    final repository = await _pump(tester, requestId: 'req-42');

    await tester.tap(find.byKey(declineKey));
    await tester.pumpAndSettle();

    expect(repository.declineCalls, 1);
    expect(repository.confirmCalls, 0);
    expect(repository.lastRequestId, 'req-42');
    // The success view reads "declined", not "confirmed".
    expect(find.text('Meeting declined'), findsOneWidget);
  });

  testWidgets('B8 confirm still books the meeting', (tester) async {
    final repository = await _pump(tester);

    await tester.tap(find.byKey(confirmKey));
    await tester.pumpAndSettle();

    expect(repository.confirmCalls, 1);
    expect(repository.declineCalls, 0);
    expect(find.text('Meeting confirmed'), findsOneWidget);
  });

  testWidgets('B8 a 409 decline surfaces the not-awaiting message',
      (tester) async {
    await _pump(
      tester,
      failure: const ApiFailure(
        code: 'APP_REQUEST_ALREADY_RESPONDED',
        message: 'This meeting is not awaiting confirmation.',
        httpStatus: 409,
      ),
    );

    await tester.tap(find.byKey(declineKey));
    await tester.pumpAndSettle();

    expect(
        find.text('This meeting is not awaiting confirmation'), findsOneWidget,);
    expect(find.text('Meeting declined'), findsNothing);
  });
}
