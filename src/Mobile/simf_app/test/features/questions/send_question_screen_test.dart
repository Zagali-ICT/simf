import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/questions/data/questions_repository.dart';
import 'package:simf_app/features/questions/send_question_screen.dart';
import 'package:simf_app/features/sessions/data/session_detail_repository.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
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

/// Feeds the optional "بيانات الجلسة" block. [detail] null → the block stays
/// hidden (the composer-only path); [fail] → an [ApiFailure] (also hidden).
class _FakeSessionDetailRepo implements SessionDetailRepository {
  _FakeSessionDetailRepo({this.detail, this.fail = false});

  final SessionDetail? detail;
  final bool fail;

  @override
  Future<SessionDetail> getDetail(String sessionId) async {
    if (fail) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: 500,
      );
    }
    return detail ?? _detail();
  }

  @override
  Future<MySeat?> getMySeat(String sessionId) async => null;
}

/// A minimal session detail; [description] fills the data block (one numbered
/// line per non-blank line).
SessionDetail _detail({String? description}) => SessionDetail(
      id: 's1',
      code: 'S1',
      title: 'Opening session',
      titleArabic: 'الجلسة الافتتاحية',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      start: DateTime.utc(2026, 1, 1, 9),
      end: DateTime.utc(2026, 1, 1, 10),
      speakers: const <SessionSpeaker>[],
      description: description,
      descriptionArabic: description,
    );

Future<void> _pump(
  WidgetTester tester, {
  required QuestionsRepository repo,
  SessionDetailRepository? detailRepo,
  String? sessionId,
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        questionsRepositoryProvider.overrideWithValue(repo),
        // Default: a detail with no description → the data block stays hidden,
        // so the composer-focused tests are unaffected.
        sessionDetailRepositoryProvider.overrideWithValue(
          detailRepo ?? _FakeSessionDetailRepo(detail: _detail()),
        ),
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

    testWidgets('renders the بيانات الجلسة block as a numbered list',
        (tester) async {
      final repo = _FakeQuestionsRepo();
      await _pump(
        tester,
        repo: repo,
        sessionId: 's1',
        detailRepo: _FakeSessionDetailRepo(
          detail: _detail(description: 'First point\nSecond point'),
        ),
      );

      // The section header + a numbered entry per description line.
      expect(find.text('Session details'), findsOneWidget);
      expect(find.text('1.'), findsOneWidget);
      expect(find.text('First point'), findsOneWidget);
      expect(find.text('2.'), findsOneWidget);
      expect(find.text('Second point'), findsOneWidget);
    });

    testWidgets('hides the بيانات الجلسة block when the detail read fails',
        (tester) async {
      final repo = _FakeQuestionsRepo();
      await _pump(
        tester,
        repo: repo,
        sessionId: 's1',
        detailRepo: _FakeSessionDetailRepo(fail: true),
      );

      // The block is optional context — a failed read leaves the composer alone.
      expect(find.text('Session details'), findsNothing);
      expect(find.byType(TextField), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Send question'), findsOneWidget);
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

      // DEF-MOD-006 — the copy no longer promises a 5-minute pre-start window
      // the server never enforced (SubmitAsync has NO lower bound; it only
      // closes questions at the session end).
      expect(
        find.text('Questions are closed for this session.'),
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
