import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/comment_models.dart';
import 'data/comments_repository.dart';

/// Page 028 — تعليقات الجمهور · Audience comments (#28, `/live/comments`).
///
/// **Auth-gated** (`RequireApprovedAccount`). Opened from a live session with a
/// `sessionId` query param. With no session it shows an "open from a live
/// session" empty state (L-2). Otherwise it reads the approved comment feed
/// (`GET /app/sessions/{id}/comments`), renders each comment with a like toggle
/// (`POST` / `DELETE .../like`, updating that one row — L-4), and a bottom
/// submit box (`POST .../comments`) that, on success, tells the user the
/// comment is awaiting moderation (a fresh comment may be Pending — L-3) and
/// refreshes the feed. Loading / empty / error+retry states. UI is interim —
/// final visuals from SIMF-VID-001.
class AudienceCommentsScreen extends ConsumerStatefulWidget {
  const AudienceCommentsScreen({required this.sessionId, super.key});

  final String? sessionId;

  @override
  ConsumerState<AudienceCommentsScreen> createState() =>
      _AudienceCommentsScreenState();
}

class _AudienceCommentsScreenState
    extends ConsumerState<AudienceCommentsScreen> {
  final TextEditingController _bodyController = TextEditingController();
  bool _loading = true;
  bool _error = false;
  bool _submitting = false;
  List<Comment> _comments = const <Comment>[];

  bool get _hasSession =>
      widget.sessionId != null && widget.sessionId!.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    if (_hasSession) {
      unawaited(_load());
    } else {
      _loading = false;
    }
  }

  @override
  void dispose() {
    _bodyController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final comments =
          await ref.read(commentsRepositoryProvider).list(widget.sessionId!);
      if (!mounted) {
        return;
      }
      setState(() {
        _comments = comments;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _error = true;
      });
    }
  }

  Future<void> _toggleLike(Comment comment) async {
    final repo = ref.read(commentsRepositoryProvider);
    try {
      final result = comment.likedByMe
          ? await repo.unlike(widget.sessionId!, comment.id)
          : await repo.like(widget.sessionId!, comment.id);
      if (!mounted) {
        return;
      }
      setState(() {
        _comments = <Comment>[
          for (final row in _comments)
            if (row.id == comment.id)
              row.withLike(result.likeCount, result.likedByMe)
            else
              row,
        ];
      });
    } on ApiFailure {
      // A like failure leaves the row untouched (L-4) — no blocking error.
    }
  }

  Future<void> _submit(AppL10n l10n) async {
    final body = _bodyController.text.trim();
    if (body.isEmpty || _submitting) {
      return;
    }
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _submitting = true);
    try {
      final result =
          await ref.read(commentsRepositoryProvider).submit(widget.sessionId!, body);
      if (!mounted) {
        return;
      }
      _bodyController.clear();
      setState(() => _submitting = false);
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            result.isPending
                ? l10n.commentSubmittedPending
                : l10n.commentSubmitted,
          ),
        ),
      );
      await _load();
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.commentSubmitFailed)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.commentsTitle)),
      body: SafeArea(
        child: Column(
          children: <Widget>[
            Expanded(child: _buildBody(l10n)),
            if (_hasSession) _SubmitBox(
              controller: _bodyController,
              submitting: _submitting,
              hint: l10n.commentBodyHint,
              sendTooltip: l10n.commentSend,
              onSubmit: () => unawaited(_submit(l10n)),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (!_hasSession) {
      return _EmptyState(
        icon: Icons.live_tv_outlined,
        message: l10n.commentsNoSession,
      );
    }
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _ErrorState(
        message: l10n.commentsError,
        onRetry: () => unawaited(_load()),
      );
    }
    if (_comments.isEmpty) {
      return _EmptyState(
        icon: Icons.forum_outlined,
        message: l10n.commentsEmpty,
      );
    }
    return ListView.builder(
      padding: const EdgeInsets.all(SimfTokens.space4),
      itemCount: _comments.length,
      itemBuilder: (context, index) {
        final comment = _comments[index];
        return _CommentCard(
          comment: comment,
          onLike: () => unawaited(_toggleLike(comment)),
        );
      },
    );
  }
}

/// One comment row: author + body + a like button (filled when [likedByMe])
/// showing the count; tapping toggles like / unlike.
class _CommentCard extends StatelessWidget {
  const _CommentCard({required this.comment, required this.onLike});

  final Comment comment;
  final VoidCallback onLike;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: SimfTokens.space2),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              comment.authorDisplayName,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textMd,
              ),
            ),
            const SizedBox(height: SimfTokens.space1),
            Text(comment.body),
            const SizedBox(height: SimfTokens.space2),
            Align(
              alignment: AlignmentDirectional.centerEnd,
              child: TextButton.icon(
                onPressed: onLike,
                icon: Icon(
                  comment.likedByMe
                      ? Icons.favorite
                      : Icons.favorite_border,
                  size: 18,
                  color: comment.likedByMe
                      ? SimfTokens.accent
                      : SimfTokens.inkMuted,
                ),
                label: Text(comment.likeCount.toString()),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The bottom submit box: a multi-line text field + a send button.
class _SubmitBox extends StatelessWidget {
  const _SubmitBox({
    required this.controller,
    required this.submitting,
    required this.hint,
    required this.sendTooltip,
    required this.onSubmit,
  });

  final TextEditingController controller;
  final bool submitting;
  final String hint;
  final String sendTooltip;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.surface,
      elevation: 8,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: <Widget>[
            Expanded(
              child: TextField(
                controller: controller,
                minLines: 1,
                maxLines: 4,
                maxLength: 1000,
                textInputAction: TextInputAction.newline,
                decoration: InputDecoration(
                  hintText: hint,
                  counterText: '',
                  filled: true,
                  fillColor: SimfTokens.field,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                    borderSide: BorderSide.none,
                  ),
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            IconButton.filled(
              tooltip: sendTooltip,
              onPressed: submitting ? null : onSubmit,
              icon: submitting
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.send),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: 56, color: SimfTokens.inkMuted),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.inkMuted),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
