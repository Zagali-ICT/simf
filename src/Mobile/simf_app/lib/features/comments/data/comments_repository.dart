import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'comment_models.dart';

/// Data layer for the audience-comments feature (Page_028). Four reads/writes
/// against the per-session comment endpoints (`RequireApprovedAccount`):
/// the feed read, the submit, and the like / unlike toggle. Throws
/// [ApiFailure] on a wire error — the screen maps it to error + retry, and a
/// like failure leaves the row untouched.
class CommentsRepository {
  CommentsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/sessions/{id}/comments` → the approved comment feed (L-1).
  Future<List<Comment>> list(String sessionId) {
    return _client.get<List<Comment>>(
      '/app/sessions/$sessionId/comments',
      decodeData: Comment.listFromData,
    );
  }

  /// `POST /app/sessions/{id}/comments` body `{ body: <1..1000> }` → the
  /// submitted comment's moderation status (a fresh comment may be Pending —
  /// L-3).
  Future<CommentSubmitResult> submit(String sessionId, String body) {
    return _client.post<CommentSubmitResult>(
      '/app/sessions/$sessionId/comments',
      body: <String, dynamic>{'body': body},
      decodeData: CommentSubmitResult.fromData,
    );
  }

  /// `POST /app/sessions/{id}/comments/{commentId}/like` → the new like state
  /// for that row (L-4).
  Future<CommentLikeResult> like(String sessionId, String commentId) {
    return _client.post<CommentLikeResult>(
      '/app/sessions/$sessionId/comments/$commentId/like',
      decodeData: CommentLikeResult.fromData,
    );
  }

  /// `DELETE /app/sessions/{id}/comments/{commentId}/like` → the new like state
  /// for that row (L-4).
  Future<CommentLikeResult> unlike(String sessionId, String commentId) {
    return _client.delete<CommentLikeResult>(
      '/app/sessions/$sessionId/comments/$commentId/like',
      decodeData: CommentLikeResult.fromData,
    );
  }
}

final commentsRepositoryProvider = Provider<CommentsRepository>((ref) {
  return CommentsRepository(ref.watch(simfApiClientProvider));
});
