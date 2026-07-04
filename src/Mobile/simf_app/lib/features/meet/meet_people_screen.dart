import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/meet_repository.dart';
import 'widgets/meet_header_card.dart';
import 'widgets/meet_match_card.dart';

// `meetRecommendationsProvider` lives in `data/meet_repository.dart`;
// re-exported so the existing imports (the meet screen test) keep resolving off
// this screen.
export 'data/meet_repository.dart';

/// Page 035 — قابل أشخاص مثلك · Meet people (#35, `/meet`, approved account).
///
/// **Auth-gated** (route 35). Pixel-parity to KSA Figma frame `1072:13409`: the
/// navy [SimfPageShell] shell, a smart-suggestions header card (title + subtitle + the
/// three topic chips, frame `1082:15269`) and a per-match card (frame
/// `1082:15273`) — the gold **% match** (from the scorer's `score`) over the
/// `تطابق` label, the name, the profile-type line, the match reason and a gold
/// initials avatar. The reason prefers the backend-generated bilingual
/// `matchReason` (D-451 — sessions + shared interests) and falls back to the
/// shared-interest count for older payloads.
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

    return SimfPageShell(
      title: l10n.meetPeopleTitle,
      onBack: () => backOrHome(context),
      body: matches.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => SimfPullToRefresh(
          onRefresh: onRefresh,
          child: SimfPullableHost(
            child: SimfErrorState(
              message: l10n.meetPeopleError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(meetRecommendationsProvider),
            ),
          ),
        ),
        data: (data) {
          final isArabic = l10n.isArabic;
          return SimfPullToRefresh(
            onRefresh: onRefresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(SimfTokens.space4),
              children: <Widget>[
                MeetHeaderCard(l10n: l10n),
                const SizedBox(height: SimfTokens.space4),
                if (data.isEmpty)
                  _EmptyInline(message: l10n.meetPeopleEmpty)
                else
                  for (final match in data) ...<Widget>[
                    MeetMatchCard(match: match, isArabic: isArabic, l10n: l10n),
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

/// The no-matches state — a people glyph + message shown **inline** beneath the
/// always-visible smart-suggestions header (so it is not the shared full-screen
/// [SimfEmptyState], which would replace the header too).
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
