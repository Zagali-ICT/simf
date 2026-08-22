// Pins that a successful send leaves the button disabled through the sheet's
// 200ms exit, so it cannot flick back to "Send request" and invite a second
// send. `mounted` stays true for that whole window — `finalizeRoute` disposes
// the State only once the exit transition ends.
import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_sheet.dart';
import 'package:simf_app/features/speakers/widgets/meeting_send_button.dart';

import '../../support/simf_test_scope.dart';

/// Built local → UTC so the sheet's `toLocal()` round-trips to the same day
/// whatever the test machine's timezone.
final List<SpeakerSlot> _oneSlot = <SpeakerSlot>[
  SpeakerSlot(
    start: DateTime(2026, 7, 10, 9).toUtc(),
    end: DateTime(2026, 7, 10, 9, 30).toUtc(),
  ),
];

/// A repository whose submit SUCCEEDS — the only path that pops.
class _SendsFineRepo implements SpeakersRepository {
  int submitCalls = 0;

  @override
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) async =>
      _oneSlot;

  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStart,
    DateTime? slotEnd,
  }) async {
    submitCalls++;
  }

  @override
  Future<List<SpeakerSummary>> getSpeakers() async =>
      const <SpeakerSummary>[];

  @override
  Future<SpeakerDetail> getSpeaker(String id) => throw UnimplementedError();
}

/// Opens the sheet as a real modal route, which is what gives the pop an exit
/// transition to assert against.
Future<void> _openSheet(WidgetTester tester, _SendsFineRepo repo) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        speakersRepositoryProvider.overrideWithValue(repo),
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
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: FilledButton(
                onPressed: () => unawaited(
                  showModalBottomSheet<void>(
                    context: context,
                    isScrollControlled: true,
                    backgroundColor: SimfTokens.cardBeige,
                    showDragHandle: false,
                    builder: (_) => MeetingRequestSheet(
                      speakerId: 's1',
                      defaultName: 'Raed',
                      baseUrl: 'http://test.local/api/v1',
                      l10n: AppL10n.of(context),
                    ),
                  ),
                ),
                child: const Text('open'),
              ),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

void main() {
  group('MeetingRequestSheet send button on exit', () {
    testWidgets('a successful send stays disabled while the sheet exits',
        (tester) async {
      final repo = _SendsFineRepo();
      await _openSheet(tester, repo);

      await tester.enterText(
        find.byKey(const ValueKey<String>('meeting-subject')),
        'Naval cooperation',
      );
      await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey<String>('meeting-time-0')));
      await tester.pumpAndSettle();

      // Deliberately NOT settled — the assertions below need a frame partway
      // through the exit; after a settle the broken and fixed builds look the
      // same.
      await tester.tap(find.text('Send request'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));

      expect(repo.submitCalls, 1);
      expect(find.byType(MeetingSendButton), findsOneWidget);
      final button =
          tester.widget<MeetingSendButton>(find.byType(MeetingSendButton));
      expect(button.enabled, isFalse);
      expect(find.text('Loading…'), findsOneWidget);
      expect(find.text('Send request'), findsNothing);

      // Raised AFTER the pop, or it sits hidden behind the sheet.
      expect(find.text('Meeting request sent'), findsOneWidget);

      // Run out the snackbar's 4s dismissal so the test does not end on a
      // pending timer.
      await tester.pumpAndSettle();
      expect(find.byType(MeetingSendButton), findsNothing);
      await tester.pump(const Duration(seconds: 4));
      await tester.pumpAndSettle();
    });
  });
}
