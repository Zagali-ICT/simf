import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/meet_models.dart';

/// `GET /app/account/recommendations/meet-like-you` → the visitor's "meet
/// someone like you" matches (RequireApprovedAccount).
final meetRecommendationsProvider =
    FutureProvider.autoDispose<List<Recommendation>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<Recommendation>>(
    '/app/account/recommendations/meet-like-you',
    decodeData: Recommendation.listFromData,
  );
});

/// Page 035 — قابل أشخاص مثلك · Meet people (#35, `/meet`, approved account).
///
/// **Auth-gated** (route 35). Pixel-parity to KSA Figma frame `1072:13409`: the
/// navy [KsaPage] shell, a smart-suggestions header card (title + subtitle + the
/// three topic chips, frame `1082:15269`) and a per-match card (frame
/// `1082:15273`) — the gold **% match** (from the scorer's `score`) over the
/// `تطابق` label, the name, the profile-type line, the match reason and a gold
/// initials avatar. The reason prefers the backend-generated bilingual
/// `matchReason` (D-451 — sessions + shared interests) and falls back to the
/// shared-interest count for older payloads ([_reason]).
class MeetPeopleScreen extends ConsumerWidget {
  const MeetPeopleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final matches = ref.watch(meetRecommendationsProvider);
    // Pull-to-refresh — re-fetch the recommendations (invalidate + await next).
    Future<void> onRefresh() async {
      ref.invalidate(meetRecommendationsProvider);
      await ref.read(meetRecommendationsProvider.future);
    }

    return KsaPage(
      title: l10n.meetPeopleTitle,
      onBack: () => ksaBackOrHome(context),
      body: matches.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => KsaRefresh(
          onRefresh: onRefresh,
          child: KsaPullable(
            child: _Error(
              message: l10n.meetPeopleError,
              onRetry: () => ref.invalidate(meetRecommendationsProvider),
            ),
          ),
        ),
        data: (data) {
          final isArabic = l10n.isArabic;
          return KsaRefresh(
            onRefresh: onRefresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(SimfTokens.space4),
              children: <Widget>[
              _HeaderCard(l10n: l10n),
              const SizedBox(height: SimfTokens.space4),
              if (data.isEmpty)
                _EmptyInline(message: l10n.meetPeopleEmpty)
              else
                for (final match in data) ...<Widget>[
                  _MatchCard(match: match, isArabic: isArabic, l10n: l10n),
                  const SizedBox(height: SimfTokens.space4),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

/// The percent shown on a card — the scorer's `score` is a 0–1 ratio; older
/// payloads that already send 0–100 are passed through. Always 0–100.
int _percent(double score) =>
    (score <= 1 ? score * 100 : score).round().clamp(0, 100);

/// The match-reason line. Prefers the backend-generated bilingual `matchReason`
/// (D-451 — sessions + shared interests); falls back to the shared-interest
/// count for older payloads that don't carry it.
String _reason(Recommendation m, AppL10n l10n) {
  final fromApi = m.localizedMatchReason(l10n.isArabic);
  if (fromApi != null && fromApi.isNotEmpty) {
    return fromApi;
  }
  return m.sharedInterestCount > 0
      ? l10n.meetPeopleSharedInterests(m.sharedInterestCount)
      : '';
}

/// First letters of the first two name words, dot-joined (frame `1082:15157`,
/// "س.أ") — a single letter for a one-word name.
String _avatarInitials(String name) {
  final parts =
      name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) {
    return '?';
  }
  if (parts.length == 1) {
    return parts.first.substring(0, 1);
  }
  return '${parts.first.substring(0, 1)}.${parts[1].substring(0, 1)}';
}

/// The smart-suggestions header card (frame `1082:15269`): a navy-deep card,
/// radius 8, with the title, the subtitle and the three topic chips.
class _HeaderCard extends StatelessWidget {
  const _HeaderCard({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.meetPeopleSmartTitle,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.meetPeopleSmartSubtitle,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              height: 1.4,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Row(
            children: <Widget>[
              for (final (index, label) in <String>[
                l10n.meetPeopleFilterAi,
                l10n.meetPeopleFilterSupply,
                l10n.meetPeopleFilterSeabed,
              ].indexed) ...<Widget>[
                if (index > 0) const SizedBox(width: SimfTokens.space2),
                Expanded(child: _TopicChip(label: label)),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

/// One topic chip in the header (frame `1082:15267`): beige hairline, radius 4,
/// beige 12px label.
class _TopicChip extends StatelessWidget {
  const _TopicChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textSm,
        ),
      ),
    );
  }
}

/// One match card (frame `1082:15273`): navy fill, radius 8, the gold % block
/// at the inline end, the name / profile-type / reason column and the gold
/// initials avatar at the inline start.
class _MatchCard extends StatelessWidget {
  const _MatchCard({
    required this.match,
    required this.isArabic,
    required this.l10n,
  });

  final Recommendation match;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final name = match.localizedName(isArabic);
    final subtitle = match.localizedProfileType(isArabic) ?? match.jobTitle;
    final reason = _reason(match, l10n);
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          _Avatar(initials: _avatarInitials(name)),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(
                  name,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textMd,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                if (subtitle != null && subtitle.trim().isNotEmpty) ...<Widget>[
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    subtitle,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
                if (reason.isNotEmpty) ...<Widget>[
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    reason,
                    style: TextStyle(
                      color: SimfTokens.beigeBorder.withValues(alpha: 0.6),
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          _PercentBlock(percent: _percent(match.score), label: l10n.meetPeopleMatchLabel),
        ],
      ),
    );
  }
}

/// The gold % over the `تطابق` label (frame `1082:15270`).
class _PercentBlock extends StatelessWidget {
  const _PercentBlock({required this.percent, required this.label});

  final int percent;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Text(
          '$percent%',
          textDirection: TextDirection.ltr,
          style: const TextStyle(
            color: SimfTokens.accent,
            fontSize: SimfTokens.textXl,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          label,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
          ),
        ),
      ],
    );
  }
}

/// The gold rounded-square initials avatar (frame `1082:15156`).
class _Avatar extends StatelessWidget {
  const _Avatar({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 48,
      height: 48,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        initials,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textMd,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _EmptyInline extends StatelessWidget {
  const _EmptyInline({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: SimfTokens.space8),
      child: Column(
        children: <Widget>[
          const Icon(
            Icons.people_outline,
            size: 56,
            color: SimfTokens.txtTertiary,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(color: SimfTokens.txtSecondary),
          ),
        ],
      ),
    );
  }
}

class _Error extends StatelessWidget {
  const _Error({required this.message, required this.onRetry});

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
