import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/sponsor_models.dart';

/// `GET /app/sponsors` → the tier-grouped sponsors (public, D-199).
final sponsorGroupsProvider =
    FutureProvider.autoDispose<List<SponsorTierGroup>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<SponsorTierGroup>>(
    '/app/sponsors',
    decodeData: SponsorTierGroup.listFromData,
  );
});

/// Page 023 — الرعاة · Sponsors (#23, `/sponsors`, Guest+).
///
/// **Public.** One read returns the sponsors grouped by tier; the screen renders
/// a section per tier with the sponsor cards (interim — logo as initials).
class SponsorsScreen extends ConsumerWidget {
  const SponsorsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final groups = ref.watch(sponsorGroupsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.sponsorsTitle)),
      body: SafeArea(
        child: groups.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _Error(
            message: l10n.sponsorsError,
            onRetry: () => ref.invalidate(sponsorGroupsProvider),
          ),
          data: (data) {
            if (data.isEmpty || data.every((g) => g.sponsors.isEmpty)) {
              return _Empty(message: l10n.sponsorsEmpty);
            }
            final isArabic = l10n.isArabic;
            return ListView(
              padding: const EdgeInsets.all(SimfTokens.space4),
              children: <Widget>[
                for (final group in data)
                  if (group.sponsors.isNotEmpty) ...<Widget>[
                    Padding(
                      padding: const EdgeInsets.symmetric(
                        vertical: SimfTokens.space2,
                      ),
                      child: Text(
                        group.tierName,
                        style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: SimfTokens.textLg,
                        ),
                      ),
                    ),
                    for (final sponsor in group.sponsors)
                      Card(
                        margin: const EdgeInsets.only(bottom: SimfTokens.space2),
                        clipBehavior: Clip.antiAlias,
                        child: ListTile(
                          leading: CircleAvatar(
                            backgroundColor: SimfTokens.field,
                            child: const Icon(
                              Icons.workspace_premium_outlined,
                              color: SimfTokens.accent,
                            ),
                          ),
                          title: Text(
                            sponsor.localizedName(isArabic),
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                          subtitle: sponsor.url == null ? null : Text(sponsor.url!),
                        ),
                      ),
                  ],
              ],
            );
          },
        ),
      ),
    );
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.workspace_premium_outlined,
            size: 56,
            color: SimfTokens.inkMuted,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
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
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
