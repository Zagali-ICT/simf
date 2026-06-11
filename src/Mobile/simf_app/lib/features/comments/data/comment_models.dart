import 'package:flutter/foundation.dart';

/// Moderation status of an audience comment — mirrors the server enum
/// (`Pending = 0`, `Approved = 1`, `Hidden = 2`). A freshly submitted comment
/// may come back **Pending** and is held back from the public feed until a
/// moderator approves it (Page_028 L-3).
enum CommentStatus {
  pending,
  approved,
  hidden;

  static CommentStatus fromInt(int? value) {
    switch (value) {
      case 1:
        return CommentStatus.approved;
      case 2:
        return CommentStatus.hidden;
      default:
        return CommentStatus.pending;
    }
  }
}

/// One row of the audience-comment feed — mirrors
/// `SIMF.Contracts.*.SessionCommentFeedRow`: the comment body, its author's
/// display name (an audit snapshot — D-223), the like count and whether the
/// caller has already liked it.
@immutable
class Comment {
  const Comment({
    required this.id,
    required this.sessionId,
    required this.authorDisplayName,
    required this.body,
    required this.likeCount,
    required this.likedByMe,
    this.userId,
    this.createdAt,
  });

  final String id;
  final String sessionId;
  final String? userId;
  final String authorDisplayName;
  final String body;
  final int likeCount;
  final bool likedByMe;
  final DateTime? createdAt;

  /// A copy with the like toggle applied — used to update one feed row in place
  /// after a like / unlike without re-reading the whole feed.
  Comment withLike(int likeCount, bool likedByMe) => Comment(
        id: id,
        sessionId: sessionId,
        userId: userId,
        authorDisplayName: authorDisplayName,
        body: body,
        likeCount: likeCount,
        likedByMe: likedByMe,
        createdAt: createdAt,
      );

  static Comment fromJson(Map<String, dynamic> json) => Comment(
        id: json['id'] as String? ?? '',
        sessionId: json['sessionId'] as String? ?? '',
        userId: json['userId'] as String?,
        authorDisplayName: json['authorDisplayName'] as String? ?? '',
        body: json['body'] as String? ?? '',
        likeCount: (json['likeCount'] as num?)?.toInt() ?? 0,
        likedByMe: json['likedByMe'] as bool? ?? false,
        createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '')?.toUtc(),
      );

  /// Reads the comment-feed payload (a bare list, or `{ items: [...] }`).
  static List<Comment> listFromData(Object? data) {
    final list = data is Map ? data['items'] : data;
    return (list as List? ?? const <dynamic>[])
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => Comment.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
  }
}

/// Result of submitting a comment — carries the new row's moderation status so
/// the screen can tell the user it is awaiting moderation (Page_028 L-3).
@immutable
class CommentSubmitResult {
  const CommentSubmitResult({required this.status});

  final CommentStatus status;

  bool get isPending => status == CommentStatus.pending;

  static CommentSubmitResult fromData(Object? data) => CommentSubmitResult(
        status: CommentStatus.fromInt(
          data is Map ? (data['status'] as num?)?.toInt() : null,
        ),
      );
}

/// Result of a like / unlike — mirrors `SessionCommentLikeResult`. The screen
/// uses it to refresh just the one row's count + state (Page_028 L-4).
@immutable
class CommentLikeResult {
  const CommentLikeResult({
    required this.commentId,
    required this.likeCount,
    required this.likedByMe,
  });

  final String commentId;
  final int likeCount;
  final bool likedByMe;

  static CommentLikeResult fromData(Object? data) {
    final json = (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
    return CommentLikeResult(
      commentId: json['commentId'] as String? ?? '',
      likeCount: (json['likeCount'] as num?)?.toInt() ?? 0,
      likedByMe: json['likedByMe'] as bool? ?? false,
    );
  }
}
