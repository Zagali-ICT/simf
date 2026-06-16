import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_svg_icon.dart';
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

/// Page 023 — الرعاة · Sponsors (#23, `/sponsors`, Guest+), rebuilt to the
/// KSA-Project Figma frame **922:2824 "Shepherds"** on the shared navy shell.
///
/// **Public.** Behaviour/data contract unchanged: one read returns the sponsors
/// grouped by tier (`SponsorTierGroup`), and the screen renders one section per
/// non-empty tier in the order the API returns them. Frame mapping: the navy
/// [KsaPage] shell (forced-LTR header, centred "الرعاة", bottom nav), then per
/// tier a right-aligned section label followed by the sponsor cards. The **first
/// (strategic)** tier renders the gold hero card (gold fill, dark text, gold
/// initials badge, navy chevron); every later tier renders the navy premium
/// card (navyDeep fill, beige hairline, white text, navy initials badge with a
/// gold edge, gold chevron). The logo is shown as initials (interim — the API
/// returns `logoRelativePath` but the frame's logo art is not yet wired). The
/// loading / error / empty / RTL states are preserved.
class SponsorsScreen extends ConsumerWidget {
  const SponsorsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final groups = ref.watch(sponsorGroupsProvider);
    return KsaPage(
      title: l10n.sponsorsTitle,
      onBack: () => ksaBackOrHome(context),
      showSweep: true,
      body: groups.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => KsaErrorState(
          message: l10n.sponsorsError,
          retryLabel: l10n.retryLabel,
          onRetry: () => ref.invalidate(sponsorGroupsProvider),
        ),
        data: (data) {
          final visibleGroups = <SponsorTierGroup>[
            for (final group in data)
              if (group.sponsors.isNotEmpty) group,
          ];
          if (visibleGroups.isEmpty) {
            return KsaEmptyState(
              icon: Icons.workspace_premium_outlined,
              message: l10n.sponsorsEmpty,
            );
          }
          final isArabic = l10n.isArabic;
          return ListView(
            padding: const EdgeInsets.all(SimfTokens.space4),
            children: <Widget>[
              for (var i = 0; i < visibleGroups.length; i++) ...<Widget>[
                if (i > 0) const SizedBox(height: SimfTokens.space5),
                _TierLabel(label: visibleGroups[i].tierName),
                const SizedBox(height: SimfTokens.space4),
                for (final sponsor in visibleGroups[i].sponsors) ...<Widget>[
                  _SponsorCard(
                    name: sponsor.localizedName(isArabic),
                    badge: _badgeText(sponsor, isArabic),
                    // D-432 — prefer the authored tagline (Figma's "الراعي
                    // الاستراتيجي · …" line); fall back to the website link.
                    secondary: sponsor.localizedTagline(isArabic) ?? sponsor.url,
                    // Frame 922:2824 — the first (strategic) tier is the gold
                    // hero card; every later tier is the navy premium card.
                    hero: i == 0,
                  ),
                  const SizedBox(height: SimfTokens.space4),
                ],
              ],
            ],
          );
        },
      ),
    );
  }

  /// The short identifier shown in the square badge box (frame's "SAMI" /
  /// "GAMI" chip). The API has no acronym field, so derive initials from the
  /// localized name — the same interim logo-as-initials treatment the badge
  /// strip uses elsewhere.
  static String _badgeText(Sponsor sponsor, bool isArabic) {
    final name = sponsor.localizedName(isArabic);
    final words = name.trim().split(RegExp(r'\s+'));
    final letters = words
        .where((w) => w.isNotEmpty)
        .take(2)
        .map((w) => w.characters.first)
        .join();
    return letters.isEmpty ? '—' : letters.toUpperCase();
  }
}

/// A tier section label — frame 922:2824's right-aligned 16px Medium white
/// heading (e.g. "الرعاية الاستراتيجية", "رعاة بريميوم").
class _TierLabel extends StatelessWidget {
  const _TierLabel({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Text(
      label,
      style: const TextStyle(
        color: Colors.white,
        fontWeight: FontWeight.w500,
        fontSize: SimfTokens.textLg,
      ),
    );
  }
}

/// One sponsor card — frame 922:2824's 72-high row. RTL puts the name +
/// secondary line on the inline-start (right), the square initials badge next
/// to it, and the forward chevron on the far inline-end (physical left).
///
/// [hero] true is the strategic gold card (gold fill, dark text, gold initials
/// badge with a navy edge, navy chevron); false is the premium navy card
/// (navyDeep fill, beige hairline, white text, navy initials badge with a gold
/// edge, gold chevron).
class _SponsorCard extends StatelessWidget {
  const _SponsorCard({
    required this.name,
    required this.badge,
    required this.secondary,
    required this.hero,
  });

  final String name;
  final String badge;
  final String? secondary;
  final bool hero;

  @override
  Widget build(BuildContext context) {
    final Color nameColor = hero ? SimfTokens.navy : Colors.white;
    final Color subColor = hero ? SimfTokens.navyDeep : SimfTokens.beigeBorder;
    return KsaCard(
      color: hero ? SimfTokens.accent : SimfTokens.navyDeep,
      borderColor: SimfTokens.beigeBorder,
      child: ConstrainedBox(
        constraints: const BoxConstraints(minHeight: 72),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Row(
            children: <Widget>[
              // Frame 922:2824 — the chevron sits at the inline-end (physical
              // left under RTL). The bundled caret does not auto-mirror, so it
              // keeps pointing left as the design shows.
              SimfSvgIcon(
                'assets/icons/ic_caret_left.svg',
                size: 20,
                color: hero ? SimfTokens.navy : SimfTokens.accent,
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      name,
                      textAlign: TextAlign.right,
                      style: TextStyle(
                        color: nameColor,
                        fontWeight: FontWeight.w700,
                        fontSize: SimfTokens.textMd,
                        height: 1.3,
                      ),
                    ),
                    if (secondary != null &&
                        secondary!.trim().isNotEmpty) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        secondary!,
                        textAlign: TextAlign.right,
                        style: TextStyle(
                          color: subColor,
                          fontSize: SimfTokens.textSm,
                          height: 1.4,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              _BadgeBox(text: badge, hero: hero),
            ],
          ),
        ),
      ),
    );
  }
}

/// The square acronym chip on a sponsor card — frame's 53-wide box. On the gold
/// hero card it is gold-filled with a navy edge and navy text; on a navy
/// premium card it is navy-filled with a gold edge and white text.
class _BadgeBox extends StatelessWidget {
  const _BadgeBox({required this.text, required this.hero});

  final String text;
  final bool hero;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 53,
      constraints: const BoxConstraints(minHeight: 53),
      alignment: Alignment.center,
      padding: const EdgeInsets.all(SimfTokens.space1),
      decoration: BoxDecoration(
        color: hero ? SimfTokens.accent : SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: hero ? SimfTokens.navy : SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
      ),
      child: Text(
        text,
        textAlign: TextAlign.center,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(
          color: hero ? SimfTokens.navy : Colors.white,
          fontWeight: FontWeight.w600,
          fontSize: SimfTokens.textMd,
        ),
      ),
    );
  }
}
