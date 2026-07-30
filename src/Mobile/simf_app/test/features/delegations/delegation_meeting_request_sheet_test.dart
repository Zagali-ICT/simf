// Tests: A35 — the delegation request sheet showed a SPEAKER-specific error.
// A 409 was hard-mapped to "This speaker is not accepting meeting requests" and
// every 400 to "This delegation is not available for meetings", so the server's
// own bilingual reason never reached the delegate. The sheet now surfaces the
// envelope message and keeps the l10n strings only as the offline fallback.
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/delegations/widgets/delegation_meeting_request_sheet.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Extends the concrete repository (its client field is library-private, so it
/// cannot be `implements`-ed) and never touches the injected client.
class _FakeDelegationsRepository extends DelegationsRepository {
  _FakeDelegationsRepository(super.client, {this.failure});

  final ApiFailure? failure;

  @override
  Future<List<DelegationSlot>> getAvailableSlots(int countryId) async =>
      const <DelegationSlot>[];

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

Future<void> _pumpAndSubmit(WidgetTester tester, ApiFailure failure) async {
  final repository = _FakeDelegationsRepository(_dummyClient(), failure: failure);
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

  testWidgets('A35 a 400 shows the server reason, not the blanket delegation copy',
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
}
