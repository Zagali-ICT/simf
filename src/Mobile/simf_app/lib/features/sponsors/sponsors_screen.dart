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
            final visibleGroups = <SponsorTierGroup>[
              for (final group in data)
                if (group.sponsors.isNotEmpty) group,
            ];
            return ListView(
              padding: const EdgeInsets.all(SimfTokens.space4),
              children: <Widget>[
                for (var i = 0; i < visibleGroups.length; i++) ...<Widget>[
                  if (i > 0) const SizedBox(height: SimfTokens.space4),
                  _TierHeader(
                    label: visibleGroups[i].tierName,
                    // Mockup highlights the first (strategic) tier in gold;
                    // the remaining bands use the secondary-text colour.
                    accented: i == 0,
                  ),
                  const SizedBox(height: SimfTokens.space2),
                  for (final sponsor in visibleGroups[i].sponsors)
                    _SponsorCard(
                      name: sponsor.localizedName(isArabic),
                      url: sponsor.url,
                      hero: i == 0,
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

/// A tier band heading — mockup's dot + bold letter-spaced label. The first
/// (strategic) band is accented in gold; the rest use the secondary text tint.
class _TierHeader extends StatelessWidget {
  const _TierHeader({required this.label, required this.accented});

  final String label;
  final bool accented;

  @override
  Widget build(BuildContext context) {
    final color = accented ? SimfTokens.accent : SimfTokens.txtSecondary;
    return Row(
      children: <Widget>[
        Container(
          width: 6,
          height: 6,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(
            label,
            style: TextStyle(
              color: color,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textXs,
              letterSpacing: 0.8,
            ),
          ),
        ),
      ],
    );
  }
}

/// One sponsor row — mockup `.spon-card` (logo box · name + link · chevron).
/// The hero variant (first tier) gets the accent border + accent fills.
class _SponsorCard extends StatelessWidget {
  const _SponsorCard({required this.name, required this.url, required this.hero});

  final String name;
  final String? url;
  final bool hero;

  String get _initials {
    final words = name.trim().split(RegExp(r'\s+'));
    final letters = words
        .where((w) => w.isNotEmpty)
        .take(2)
        .map((w) => w.characters.first)
        .join();
    return letters.isEmpty ? '—' : letters.toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: SimfTokens.space2),
      padding: const EdgeInsets.all(SimfTokens.space3),
      decoration: BoxDecoration(
        color: hero
            ? SimfTokens.accent.withValues(alpha: 0.06)
            : SimfTokens.surfaceTint,
        border: Border.all(
          color: hero ? SimfTokens.accent : SimfTokens.line2,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: hero ? SimfTokens.accent : SimfTokens.surface,
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
            alignment: Alignment.center,
            child: Text(
              _initials,
              style: const TextStyle(
                color: SimfTokens.navy,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textSm,
                letterSpacing: 0.4,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space3),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(
                  name,
                  style: const TextStyle(
                    color: SimfTokens.surface,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textMd,
                    height: 1.4,
                  ),
                ),
                if (url != null) ...<Widget>[
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    url!,
                    style: const TextStyle(
                      color: SimfTokens.txtSecondary,
                      fontSize: SimfTokens.textXs,
                      height: 1.5,
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Container(
            width: 28,
            height: 28,
            decoration: BoxDecoration(
              color: hero ? SimfTokens.accent : Colors.transparent,
              shape: BoxShape.circle,
              border: Border.all(
                color: hero ? SimfTokens.accent : SimfTokens.line,
              ),
            ),
            alignment: Alignment.center,
            child: Icon(
              Icons.chevron_left,
              size: 16,
              color: hero ? SimfTokens.navy : SimfTokens.txtTertiary,
            ),
          ),
        ],
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
            color: SimfTokens.txtTertiary,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.txtSecondary)),
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
