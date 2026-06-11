import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/comments/audience_comments_screen.dart';
import 'package:simf_app/features/comments/data/comment_models.dart';
import 'package:simf_app/features/comments/data/comments_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

Comment _comment({
  String id = 'c1',
  String body = 'Great talk!',
  int likeCount = 2,
  bool likedByMe = false,
}) =>
    Comment(
      id: id,
      sessionId: 's1',
      authorDisplayName: 'Sara',
      body: body,
      likeCount: likeCount,
      likedByMe: likedByMe,
    );

class _FakeCommentsRepo implements CommentsRepository {
  _FakeCommentsRepo({
    List<Comment>? comments,
    this.listStatus,
    this.submitStatus = CommentStatus.pending,
  }) : comments = comments ?? <Comment>[];

  List<Comment> comments;
  final int? listStatus;
  final CommentStatus submitStatus;
  int listCalls = 0;
  int submitCalls = 0;
  int likeCalls = 0;
  int unlikeCalls = 0;
  String? lastSubmittedBody;

  @override
  Future<List<Comment>> list(String sessionId) async {
    listCalls++;
    if (listStatus != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: listStatus,
      );
    }
    return comments;
  }

  @override
  Future<CommentSubmitResult> submit(String sessionId, String body) async {
    submitCalls++;
    lastSubmittedBody = body;
    return CommentSubmitResult(status: submitStatus);
  }

  @override
  Future<CommentLikeResult> like(String sessionId, String commentId) async {
    likeCalls++;
    return const CommentLikeResult(
      commentId: 'c1',
      likeCount: 3,
      likedByMe: true,
    );
  }

  @override
  Future<CommentLikeResult> unlike(String sessionId, String commentId) async {
    unlikeCalls++;
    return const CommentLikeResult(
      commentId: 'c1',
      likeCount: 2,
      likedByMe: false,
    );
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required CommentsRepository repo,
  String? sessionId,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        commentsRepositoryProvider.overrideWithValue(repo),
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
        home: AudienceCommentsScreen(sessionId: sessionId),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('AudienceCommentsScreen (Page 028)', () {
    testWidgets('no sessionId shows the open-from-live empty state',
        (tester) async {
      final repo = _FakeCommentsRepo();
      await _pump(tester, repo: repo, sessionId: null);

      expect(find.text('Open this from a live session.'), findsOneWidget);
      // It never reads the feed without a session.
      expect(repo.listCalls, 0);
      // No submit box without a session.
      expect(find.byType(TextField), findsNothing);
    });

    testWidgets('renders the comment feed', (tester) async {
      await _pump(
        tester,
        repo: _FakeCommentsRepo(comments: <Comment>[_comment()]),
        sessionId: 's1',
      );

      expect(find.text('Sara'), findsOneWidget);
      expect(find.text('Great talk!'), findsOneWidget);
      expect(find.text('2'), findsOneWidget);
    });

    testWidgets('empty feed shows the empty state', (tester) async {
      await _pump(
        tester,
        repo: _FakeCommentsRepo(comments: <Comment>[]),
        sessionId: 's1',
      );
      expect(find.text('No comments yet'), findsOneWidget);
    });

    testWidgets('a read failure shows error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeCommentsRepo(listStatus: 500);
      await _pump(tester, repo: repo, sessionId: 's1');

      expect(find.text('Could not load the comments.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.listCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('submitting calls the repo + shows the moderation toast',
        (tester) async {
      final repo = _FakeCommentsRepo(comments: <Comment>[_comment()]);
      await _pump(tester, repo: repo, sessionId: 's1');

      await tester.enterText(find.byType(TextField), 'Nice session');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(repo.submitCalls, 1);
      expect(repo.lastSubmittedBody, 'Nice session');
      expect(
        find.text('Your comment was submitted and is awaiting moderation.'),
        findsOneWidget,
      );
    });

    testWidgets('an approved comment shows the plain submitted toast + refresh',
        (tester) async {
      final repo = _FakeCommentsRepo(
        comments: <Comment>[_comment()],
        submitStatus: CommentStatus.approved,
      );
      await _pump(tester, repo: repo, sessionId: 's1');

      await tester.enterText(find.byType(TextField), 'Nice session');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(repo.submitCalls, 1);
      expect(find.text('Your comment was submitted'), findsOneWidget);
      // The success path refreshes the feed (initial load + post-submit reload).
      expect(repo.listCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('tapping like calls like then unlike updates the row',
        (tester) async {
      final repo = _FakeCommentsRepo(comments: <Comment>[_comment()]);
      await _pump(tester, repo: repo, sessionId: 's1');

      // Not yet liked → outline icon.
      expect(find.byIcon(Icons.favorite_border), findsOneWidget);
      await tester.tap(find.byIcon(Icons.favorite_border));
      await tester.pumpAndSettle();

      expect(repo.likeCalls, 1);
      // Liked → filled icon + the new count.
      expect(find.byIcon(Icons.favorite), findsOneWidget);
      expect(find.text('3'), findsOneWidget);

      // Tap again → unlike.
      await tester.tap(find.byIcon(Icons.favorite));
      await tester.pumpAndSettle();
      expect(repo.unlikeCalls, 1);
      expect(find.byIcon(Icons.favorite_border), findsOneWidget);
      expect(find.text('2'), findsOneWidget);
    });
  });
}
