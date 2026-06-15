import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/questions/data/questions_repository.dart';
import 'package:simf_app/features/questions/send_question_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _FakeQuestionsRepo implements QuestionsRepository {
  _FakeQuestionsRepo({this.failStatus, this.failCode});

  final int? failStatus;
  final String? failCode;
  String? lastQuestionText;
  int? lastRecipientIndex;
  int calls = 0;

  @override
  Future<void> submitQuestion(
    String sessionId, {
    required String questionText,
    required int recipientIndex,
  }) async {
    calls++;
    lastQuestionText = questionText;
    lastRecipientIndex = recipientIndex;
    if (failStatus != null || failCode != null) {
      throw ApiFailure(
        code: failCode ?? ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: failStatus,
      );
    }
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required QuestionsRepository repo,
  String? sessionId,
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        questionsRepositoryProvider.overrideWithValue(repo),
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
        home: SendQuestionScreen(sessionId: sessionId),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SendQuestionScreen (Page 026, Figma 934:3636)', () {
    testWidgets('golden render — label, question box, submit, note',
        (tester) async {
      final repo = _FakeQuestionsRepo();
      await _pump(tester, repo: repo, sessionId: 's1');

      // The "الاسئلة" section label, the question box, the gold submit, and
      // the "reviewed before air" note all render.
      expect(find.byType(TextField), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Send question'), findsOneWidget);
      expect(
        find.textContaining('Questions are reviewed before going on air.'),
        findsOneWidget,
      );
      // The frame carries no recipient selector.
      expect(find.text('Speaker'), findsNothing);
      expect(find.text('Host'), findsNothing);
    });

    testWidgets('no session id shows the open-from-a-session empty state',
        (tester) async {
      final repo = _FakeQuestionsRepo();
      await _pump(tester, repo: repo);

      expect(
        find.text('Open this from a live session to send a question.'),
        findsOneWidget,
      );
      expect(find.byType(TextField), findsNothing);
      expect(repo.calls, 0);
    });

    testWidgets('empty question shows the inline prompt, no submit',
        (tester) async {
      final repo = _FakeQuestionsRepo();
      await _pump(tester, repo: repo, sessionId: 's1');

      await tester.tap(find.widgetWithText(FilledButton, 'Send question'));
      await tester.pumpAndSettle();

      expect(find.text('Type your question first'), findsOneWidget);
      expect(repo.calls, 0);
    });

    testWidgets('typing + submit sends to the default recipient + sent toast',
        (tester) async {
      final repo = _FakeQuestionsRepo();
      await _pump(tester, repo: repo, sessionId: 's1');

      await tester.enterText(find.byType(TextField), 'How deep is the reef?');
      await tester.tap(find.widgetWithText(FilledButton, 'Send question'));
      await tester.pumpAndSettle();

      expect(repo.lastQuestionText, 'How deep is the reef?');
      // The frame has no selector; the wire `recipient` stays the default (0).
      expect(repo.lastRecipientIndex, 0);
      expect(find.text('Your question was sent'), findsOneWidget);
      // The field is cleared after a successful submit.
      expect(find.text('How deep is the reef?'), findsNothing);
    });

    testWidgets('a 400 SESSION_NOT_LIVE_FOR_QUESTIONS shows the not-open toast',
        (tester) async {
      final repo = _FakeQuestionsRepo(
        failStatus: 400,
        failCode: 'SESSION_NOT_LIVE_FOR_QUESTIONS',
      );
      await _pump(tester, repo: repo, sessionId: 's1');

      await tester.enterText(find.byType(TextField), 'Is the venue open?');
      await tester.tap(find.widgetWithText(FilledButton, 'Send question'));
      await tester.pumpAndSettle();

      expect(
        find.text(
          'Questions are only open from 5 minutes before the session until it ends.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('a generic failure shows the generic error toast',
        (tester) async {
      final repo = _FakeQuestionsRepo(failStatus: 500);
      await _pump(tester, repo: repo, sessionId: 's1');

      await tester.enterText(find.byType(TextField), 'Anything?');
      await tester.tap(find.widgetWithText(FilledButton, 'Send question'));
      await tester.pumpAndSettle();

      final failToast = find.text('Could not send your question. Try again.');
      expect(failToast, findsOneWidget);
    });
  });
}
