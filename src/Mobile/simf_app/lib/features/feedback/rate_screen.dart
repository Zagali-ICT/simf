import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';

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
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.rateTitle)),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space5,
            SimfTokens.space4,
            SimfTokens.space6,
          ),
          children: <Widget>[
            // Centered lead + star selector (mockup: question over a gold
            // star row), on the bare navy surface.
            Column(
              children: <Widget>[
                Text(
                  l10n.rateLead,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: SimfTokens.surface,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textLg,
                    height: 1.5,
                  ),
                ),
                const SizedBox(height: SimfTokens.space4),
                // Stars fill left-to-right (LTR) like the mockup row.
                Directionality(
                  textDirection: TextDirection.ltr,
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      for (var star = 1; star <= 5; star++)
                        IconButton(
                          iconSize: 36,
                          onPressed: () => setState(() => _stars = star),
                          icon: Icon(
                            star <= _stars ? Icons.star : Icons.star_border,
                            color: star <= _stars
                                ? SimfTokens.accent
                                : SimfTokens.line,
                          ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space6),
            // Comments section (mockup: "ملاحظاتك" heading over a bordered box).
            Text(
              l10n.rateCommentLabel,
              style: const TextStyle(
                color: SimfTokens.txtTertiary,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXs,
                letterSpacing: 0.8,
              ),
            ),
            const SizedBox(height: SimfTokens.space2),
            TextField(
              controller: _comment,
              maxLength: 2000,
              maxLines: 4,
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
