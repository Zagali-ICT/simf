import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';

/// Data layer for the rating (Page_040). `POST /app/feedback/rate`
/// (`RequireApprovedAccount`) upserts the caller's star rating + comment.
class FeedbackRepository {
  FeedbackRepository(this._client);

  final SimfApiClient _client;

  Future<void> submitRating({required int stars, String? comment}) {
    return _client.post<bool>(
      '/app/feedback/rate',
      body: <String, dynamic>{'stars': stars, 'comment': comment},
      decodeData: (_) => true,
    );
  }
}

final feedbackRepositoryProvider = Provider<FeedbackRepository>((ref) {
  return FeedbackRepository(ref.watch(simfApiClientProvider));
});

/// Page 040 — تقييم · Rate (#40, `/rate`, Visitor login-only).
///
/// A 1–5 star selector + an optional comment → `POST /app/feedback/rate`. The
/// route is auth-gated; a 401/403 is mapped to a toast.
class RateScreen extends ConsumerStatefulWidget {
  const RateScreen({super.key});

  @override
  ConsumerState<RateScreen> createState() => _RateScreenState();
}

class _RateScreenState extends ConsumerState<RateScreen> {
  final TextEditingController _comment = TextEditingController();
  int _stars = 0;
  bool _submitting = false;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _submit(AppL10n l10n) async {
    if (_stars < 1) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(l10n.rateStarsRequired)));
      return;
    }
    setState(() => _submitting = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(feedbackRepositoryProvider).submitRating(
            stars: _stars,
            comment: _comment.text.trim().isEmpty ? null : _comment.text.trim(),
          );
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      messenger.showSnackBar(SnackBar(content: Text(l10n.rateThanks)));
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      messenger.showSnackBar(SnackBar(content: Text(l10n.rateFailed)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.rateTitle)),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(SimfTokens.space4),
          children: <Widget>[
            Text(l10n.rateLead, style: const TextStyle(fontSize: SimfTokens.textMd)),
            const SizedBox(height: SimfTokens.space4),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                for (var star = 1; star <= 5; star++)
                  IconButton(
                    iconSize: 40,
                    onPressed: () => setState(() => _stars = star),
                    icon: Icon(
                      star <= _stars ? Icons.star : Icons.star_border,
                      color: SimfTokens.accent,
                    ),
                  ),
              ],
            ),
            const SizedBox(height: SimfTokens.space4),
            TextField(
              controller: _comment,
              maxLength: 2000,
              maxLines: 4,
              decoration: InputDecoration(labelText: l10n.rateCommentLabel),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(
              onPressed: _submitting ? null : () => unawaited(_submit(l10n)),
              child: Text(_submitting ? l10n.loadingLabel : l10n.rateSubmit),
            ),
          ],
        ),
      ),
    );
  }
}
