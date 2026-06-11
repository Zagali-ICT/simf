import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/comments/data/comment_models.dart';

void main() {
  group('Comment.fromJson', () {
    test('decodes the feed row + parses createdAt as UTC', () {
      final comment = Comment.fromJson(<String, dynamic>{
        'id': 'c1',
        'sessionId': 's1',
        'userId': 'u1',
        'authorDisplayName': 'Sara',
        'body': 'Great talk!',
        'createdAt': '2026-06-05T09:00:00Z',
        'likeCount': 3,
        'likedByMe': true,
      });

      expect(comment.id, 'c1');
      expect(comment.sessionId, 's1');
      expect(comment.userId, 'u1');
      expect(comment.authorDisplayName, 'Sara');
      expect(comment.body, 'Great talk!');
      expect(comment.likeCount, 3);
      expect(comment.likedByMe, isTrue);
      expect(comment.createdAt, isNotNull);
      expect(comment.createdAt!.isUtc, isTrue);
    });

    test('a missing payload yields safe defaults', () {
      final comment = Comment.fromJson(<String, dynamic>{});
      expect(comment.id, '');
      expect(comment.likeCount, 0);
      expect(comment.likedByMe, isFalse);
      expect(comment.userId, isNull);
      expect(comment.createdAt, isNull);
    });

    test('withLike updates only the count + state', () {
      final comment = Comment.fromJson(<String, dynamic>{
        'id': 'c1',
        'body': 'Hi',
        'likeCount': 1,
        'likedByMe': false,
      });
      final liked = comment.withLike(2, true);
      expect(liked.id, 'c1');
      expect(liked.body, 'Hi');
      expect(liked.likeCount, 2);
      expect(liked.likedByMe, isTrue);
    });
  });

  group('Comment.listFromData', () {
    test('reads a bare list', () {
      final list = Comment.listFromData(<dynamic>[
        <String, dynamic>{'id': 'c1', 'body': 'one'},
        <String, dynamic>{'id': 'c2', 'body': 'two'},
      ]);
      expect(list, hasLength(2));
      expect(list.first.id, 'c1');
    });

    test('reads an items-wrapped payload', () {
      final list = Comment.listFromData(<String, dynamic>{
        'items': <dynamic>[
          <String, dynamic>{'id': 'c1', 'body': 'one'},
        ],
      });
      expect(list, hasLength(1));
      expect(list.first.id, 'c1');
    });

    test('null / empty yields an empty list', () {
      expect(Comment.listFromData(null), isEmpty);
      expect(Comment.listFromData(<dynamic>[]), isEmpty);
    });
  });

  group('CommentStatus.fromInt', () {
    test('maps the server enum values', () {
      expect(CommentStatus.fromInt(0), CommentStatus.pending);
      expect(CommentStatus.fromInt(1), CommentStatus.approved);
      expect(CommentStatus.fromInt(2), CommentStatus.hidden);
    });

    test('an unknown / null value falls back to pending', () {
      expect(CommentStatus.fromInt(9), CommentStatus.pending);
      expect(CommentStatus.fromInt(null), CommentStatus.pending);
    });
  });

  group('CommentSubmitResult.fromData', () {
    test('a pending status flags isPending', () {
      final result = CommentSubmitResult.fromData(<String, dynamic>{'status': 0});
      expect(result.status, CommentStatus.pending);
      expect(result.isPending, isTrue);
    });

    test('an approved status is not pending', () {
      final result = CommentSubmitResult.fromData(<String, dynamic>{'status': 1});
      expect(result.status, CommentStatus.approved);
      expect(result.isPending, isFalse);
    });
  });

  group('CommentLikeResult.fromData', () {
    test('decodes the like result', () {
      final result = CommentLikeResult.fromData(<String, dynamic>{
        'commentId': 'c1',
        'likeCount': 5,
        'likedByMe': true,
      });
      expect(result.commentId, 'c1');
      expect(result.likeCount, 5);
      expect(result.likedByMe, isTrue);
    });

    test('a missing payload yields safe defaults', () {
      final result = CommentLikeResult.fromData(null);
      expect(result.commentId, '');
      expect(result.likeCount, 0);
      expect(result.likedByMe, isFalse);
    });
  });
}
