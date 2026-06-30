import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
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
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.commentsTitle)),
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
      return SimfPullToRefresh(
        onRefresh: _load,
        child: SimfPullableHost(
          child: _ErrorState(
            message: l10n.commentsError,
            onRetry: () => unawaited(_load()),
          ),
        ),
      );
    }
    if (_comments.isEmpty) {
      return SimfPullToRefresh(
        onRefresh: _load,
        child: SimfPullableHost(
          child: _EmptyState(
            icon: Icons.forum_outlined,
            message: l10n.commentsEmpty,
          ),
        ),
      );
    }
    return SimfPullToRefresh(
      onRefresh: _load,
      child: ListView.separated(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space3,
          SimfTokens.space4,
          SimfTokens.space4,
        ),
        itemCount: _comments.length,
        separatorBuilder: (context, index) =>
            const SizedBox(height: SimfTokens.space2),
        itemBuilder: (context, index) {
          final comment = _comments[index];
          return _CommentCard(
            comment: comment,
            onLike: () => unawaited(_toggleLike(comment)),
          );
        },
      ),
    );
  }
}

/// One comment row (mockup `.cm`): a head with a gold initials avatar + author
/// name, the comment body, then a foot with a like pill (red when [likedByMe],
/// mockup `.like.on`) showing the count and a thin gold accent bar. Tapping the
/// pill toggles like / unlike.
class _CommentCard extends StatelessWidget {
  const _CommentCard({required this.comment, required this.onLike});

  final Comment comment;
  final VoidCallback onLike;

  String get _initials {
    final parts = comment.authorDisplayName
        .trim()
        .split(RegExp(r'\s+'))
        .where((p) => p.isNotEmpty)
        .toList();
    if (parts.isEmpty) {
      return '؟';
    }
    if (parts.length == 1) {
      return parts.first.characters.first;
    }
    return parts.first.characters.first + parts.last.characters.first;
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space3,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                CircleAvatar(
                  radius: 12,
                  backgroundColor: SimfTokens.accent,
                  foregroundColor: SimfTokens.navy,
                  child: Text(
                    _initials,
                    style: const TextStyle(
                      fontSize: 9,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: Text(
                    comment.authorDisplayName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SimfTokens.surface,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              comment.body,
              style: const TextStyle(
                color: SimfTokens.surface,
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w500,
                height: 1.65,
              ),
            ),
            const SizedBox(height: SimfTokens.space2),
            Container(
              margin: const EdgeInsets.only(bottom: SimfTokens.space2),
              decoration: const BoxDecoration(
                border: Border(top: BorderSide(color: SimfTokens.line2)),
              ),
            ),
            Row(
              children: <Widget>[
                _LikePill(
                  liked: comment.likedByMe,
                  count: comment.likeCount,
                  onTap: onLike,
                ),
                const SizedBox(width: SimfTokens.space2),
                const Expanded(child: _AccentBar()),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// The like control rendered as the mockup `.like` pill — a rounded outline
/// chip that turns red ([SimfTokens.danger]) when [liked].
class _LikePill extends StatelessWidget {
  const _LikePill({
    required this.liked,
    required this.count,
    required this.onTap,
  });

  final bool liked;
  final int count;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final Color color = liked ? SimfTokens.danger : SimfTokens.txtSecondary;
    return Material(
      color: liked
          ? SimfTokens.danger.withValues(alpha: 0.06)
          : Colors.transparent,
      borderRadius: BorderRadius.circular(14),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(14),
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: 3,
          ),
          decoration: BoxDecoration(
            border: Border.all(
              color: liked
                  ? SimfTokens.danger.withValues(alpha: 0.35)
                  : SimfTokens.line,
            ),
            borderRadius: BorderRadius.circular(14),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Icon(
                liked ? Icons.favorite : Icons.favorite_border,
                size: 14,
                color: color,
              ),
              const SizedBox(width: SimfTokens.space1),
              Text(
                count.toString(),
                textDirection: TextDirection.ltr,
                style: TextStyle(
                  color: color,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textXs,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The thin gold gradient bar that fills the comment foot (mockup `.cm-bar`).
class _AccentBar extends StatelessWidget {
  const _AccentBar();

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 2,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(1),
        gradient: LinearGradient(
          colors: <Color>[
            SimfTokens.accent.withValues(alpha: 0.25),
            Colors.transparent,
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
    return Container(
      decoration: const BoxDecoration(
        color: SimfTokens.navy,
        border: Border(top: BorderSide(color: SimfTokens.line2)),
      ),
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
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontSize: SimfTokens.textSm,
                ),
                decoration: InputDecoration(
                  hintText: hint,
                  hintStyle: const TextStyle(color: SimfTokens.txtTertiary),
                  counterText: '',
                  filled: true,
                  fillColor: SimfTokens.surfaceTint,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                    borderSide: const BorderSide(color: SimfTokens.line2),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                    borderSide: const BorderSide(color: SimfTokens.line2),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                    borderSide: const BorderSide(color: SimfTokens.accent),
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
            Icon(icon, size: 56, color: SimfTokens.txtTertiary),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.txtSecondary),
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
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.txtSecondary),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
