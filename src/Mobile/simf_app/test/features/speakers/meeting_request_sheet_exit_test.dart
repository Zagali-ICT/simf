// Regression guard for the send button on the handler that POPS the sheet —
// the fourth site of the same defect as ContactPreviewSheet, SavedContactSheet
// and CapturedVisitorSheet, and fixed the same way.
//
// `Navigator.pop` only reverses the route's animation controller
// (`TransitionRoute.didPop`); `finishedWhenPopped` is false at that moment, so
// `finalizeRoute` — and with it the State's disposal — does not run until the
// 200ms exit transition completes. A `mounted` guard therefore still passes for
// the whole of that window, and clearing `_submitting` inside it repaints a
// sheet the user can still see: the gold button visibly flicks from "Loading…"
// back to "Send request" as the sheet slides away, inviting a second send of a
// request that already succeeded.
//
// This test asserts MID-EXIT on purpose. After a `pumpAndSettle` the sheet is
// gone in the broken build and the fixed one alike, so nothing tells them
// apart — which is why the existing sheet test file, whose helper ends every
// send with `pumpAndSettle`, cannot express it.
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

/// One free slot on 2026-07-10, built local → UTC so the sheet's `toLocal()`
/// round-trips to the same day whatever the test machine's timezone.
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

/// Opens the sheet the way `speaker_profile_screen.dart` does — as a real
/// modal route, which is what gives the pop an exit transition to assert
/// against.
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

      // The send tap is deliberately NOT settled: the submit future resolves
      // and `pop()` fires inside this frame, and the second pump lands a
      // quarter of the way through the 200ms exit — where the user would see
      // the button flick back.
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

      // The toast is raised AFTER the pop, which is the ordering that keeps it
      // visible instead of hidden behind the sheet.
      expect(find.text('Meeting request sent'), findsOneWidget);

      // Settle the exit, then run out the snackbar's own 4s dismissal so the
      // test does not end on a pending timer.
      await tester.pumpAndSettle();
      expect(find.byType(MeetingSendButton), findsNothing);
      await tester.pump(const Duration(seconds: 4));
      await tester.pumpAndSettle();
    });
  });
}
