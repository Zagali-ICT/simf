import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
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
/// non-empty tier in the order the API returns them. Frame mapping (922:2824 —
/// three bands, position-based so it is faithful for any tier naming): the
/// **first** tier renders the gold hero card; the **lowest** tier (the last
/// group, when more than one) renders the compact 3-column logo grid; any tier
/// **in between** renders the navy premium card. P6 — D-440: each sponsor logo is
/// the real `SponsorLogo` asset (D-357) served anonymously at
/// `{base}/app/assets/SponsorLogo/{id}/image`, with the acronym initials as the
/// fallback. The loading / error / empty / RTL states are preserved.
class SponsorsScreen extends ConsumerWidget {
  const SponsorsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final groups = ref.watch(sponsorGroupsProvider);
    // Pull-to-refresh — re-fetch the tier-grouped sponsors. Invalidate the
    // FutureProvider then await its next value so the spinner shows until the
    // new read completes.
    Future<void> onRefresh() async {
      ref.invalidate(sponsorGroupsProvider);
      await ref.read(sponsorGroupsProvider.future);
    }

    return SimfPageShell(
      title: l10n.sponsorsTitle,
      onBack: () => backOrHome(context),
      showSweep: true,
      body: groups.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => SimfPullToRefresh(
          onRefresh: onRefresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            children: <Widget>[
              SimfErrorState(
                message: l10n.sponsorsError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(sponsorGroupsProvider),
              ),
            ],
          ),
        ),
        data: (data) {
          final visibleGroups = <SponsorTierGroup>[
            for (final group in data)
              if (group.sponsors.isNotEmpty) group,
          ];
          if (visibleGroups.isEmpty) {
            return SimfPullToRefresh(
              onRefresh: onRefresh,
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: <Widget>[
                  SimfEmptyState(
                    icon: Icons.workspace_premium_outlined,
                    message: l10n.sponsorsEmpty,
                  ),
                ],
              ),
            );
          }
          final isArabic = l10n.isArabic;
          // The logo image lives at {base}/app/assets/SponsorLogo/{id}/image (D-357).
          final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
          final lastIndex = visibleGroups.length - 1;
          return SimfPullToRefresh(
            onRefresh: onRefresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(SimfTokens.space4),
              children: <Widget>[
              for (var i = 0; i < visibleGroups.length; i++) ...<Widget>[
                if (i > 0) const SizedBox(height: SimfTokens.space6),
                _TierLabel(label: visibleGroups[i].tierName),
                const SizedBox(height: SimfTokens.space4),
                // Frame 922:2824 — three bands: the top tier is the gold hero
                // card, the lowest tier is a compact logo-tile grid, and any tier
                // in between is a navy premium card (position-based so it is
                // faithful for any tier naming, not just Platinum/Gold/Silver).
                if (i == lastIndex && visibleGroups.length > 1)
                  _SponsorGrid(
                    sponsors: visibleGroups[i].sponsors,
                    baseUrl: baseUrl,
                    isArabic: isArabic,
                  )
                else
                  for (final sponsor in visibleGroups[i].sponsors) ...<Widget>[
                    _SponsorCard(
                      id: sponsor.id,
                      baseUrl: baseUrl,
                      name: sponsor.localizedName(isArabic),
                      badge: _badgeText(sponsor, isArabic),
                      // D-432 — prefer the authored tagline (Figma's "الراعي
                      // الاستراتيجي · …" line); fall back to the website link.
                      secondary:
                          sponsor.localizedTagline(isArabic) ?? sponsor.url,
                      hero: i == 0,
                      // Wave 3 — tap → the sponsor detail (Figma 1439:11826).
                      onTap: () => context.pushNamed(
                        RouteNames.sponsorDetail,
                        pathParameters: <String, String>{
                          'sponsorId': sponsor.id,
                        },
                      ),
                    ),
                    const SizedBox(height: SimfTokens.space4),
                  ],
              ],
              ],
            ),
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

/// One sponsor card — frame 922:2824's 72-high row. RTL puts the square logo
/// badge on the inline-start (physical right), the name + secondary line next
/// to it, and the forward chevron on the far inline-end (physical left).
///
/// [hero] true is the strategic gold card (gold fill, dark text, gold initials
/// badge with a navy edge, navy chevron); false is the premium navy card
/// (navyDeep fill, beige hairline, white text, navy initials badge with a gold
/// edge, gold chevron).
class _SponsorCard extends StatelessWidget {
  const _SponsorCard({
    required this.id,
    required this.baseUrl,
    required this.name,
    required this.badge,
    required this.secondary,
    required this.hero,
    required this.onTap,
  });

  final String id;
  final String baseUrl;
  final String name;
  final String badge;
  final String? secondary;
  final bool hero;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 922:2824 — the sponsor name is white on BOTH the gold hero card
    // (925:2979 `text-white`) and the navy premium card; only the secondary
    // line changes colour with the card.
    const Color nameColor = Colors.white;
    final Color subColor = hero ? SimfTokens.navyDeep : SimfTokens.beigeBorder;
    return SimfCard(
      onTap: onTap,
      color: hero ? SimfTokens.accent : SimfTokens.navyDeep,
      borderColor: SimfTokens.beigeBorder,
      child: ConstrainedBox(
        constraints: const BoxConstraints(minHeight: 72),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Row(
            children: <Widget>[
              // Frame 922:2824 — RTL order: the square logo badge sits at the
              // inline-start (physical right), the name + secondary line next to
              // it, and the forward chevron on the far inline-end (physical
              // left). The bundled caret does not auto-mirror, so it keeps
              // pointing left as the design shows.
              _BadgeBox(
                hero: hero,
                child: _SponsorLogo(
                  id: id,
                  baseUrl: baseUrl,
                  fallbackInitials: badge,
                  hero: hero,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Column(
                  // Figma 925:2978 — items-end / text-right. In RTL,
                  // CrossAxisAlignment.start is the physical RIGHT edge, so the
                  // name + tagline hug the badge. The tagline is capped to the
                  // frame's ~215px (ConstrainedBox) so its box has a definite
                  // width and textAlign.right keeps EVERY wrapped line flush to
                  // the badge (a free-width Text left short last lines centred).
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.center,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      name,
                      textAlign: TextAlign.start,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
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
                      // Responsive (owner 2026-06-28): fill the available column
                      // width instead of the frame's fixed 215px so the tagline
                      // stretches on a tablet; textAlign.right keeps the wrapped
                      // lines flush to the badge.
                      SizedBox(
                        width: double.infinity,
                        child: Text(
                          secondary!,
                          textAlign: TextAlign.start,
                          style: TextStyle(
                            color: subColor,
                            fontSize: SimfTokens.textSm,
                            height: 1.4,
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              SimfSvgIcon(
                // Frame 925:2990 — the iconamoon thin chevron (navy on the gold
                // hero card, gold on a premium card), NOT a filled triangle.
                'assets/icons/ic_back.svg',
                size: 20,
                color: hero ? SimfTokens.navy : SimfTokens.accent,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The square logo chip on a sponsor card — frame's 53×53 box. On the gold hero
/// card it is gold-filled with a navy edge; on a navy premium card it is
/// navy-filled with a gold edge. Hosts the real [_SponsorLogo] (clipped to fill),
/// falling back to the acronym initials.
class _BadgeBox extends StatelessWidget {
  const _BadgeBox({required this.child, required this.hero});

  final Widget child;
  final bool hero;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 53,
      height: 53,
      clipBehavior: Clip.antiAlias,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: hero ? SimfTokens.accent : SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: hero ? SimfTokens.navy : SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
      ),
      child: child,
    );
  }
}

/// The sponsor's real logo (D-357 `SponsorLogo` asset, served anonymously at
/// `{base}/app/assets/SponsorLogo/{id}/image`) clipped to fill its parent box,
/// falling back to the acronym initials while it loads or when no logo is set
/// (the route 404s). [hero] picks the initials colour for the box it sits in.
class _SponsorLogo extends StatelessWidget {
  const _SponsorLogo({
    required this.id,
    required this.baseUrl,
    required this.fallbackInitials,
    required this.hero,
  });

  final String id;
  final String baseUrl;
  final String fallbackInitials;
  final bool hero;

  @override
  Widget build(BuildContext context) {
    final fallback = Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space1),
        child: Text(
          fallbackInitials,
          textAlign: TextAlign.center,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: hero ? SimfTokens.navy : Colors.white,
            fontWeight: FontWeight.w600,
            fontSize: SimfTokens.textMd,
          ),
        ),
      ),
    );
    if (id.isEmpty) {
      return fallback;
    }
    return Image.network(
      '$baseUrl/app/assets/SponsorLogo/$id/image',
      fit: BoxFit.cover,
      gaplessPlayback: true,
      loadingBuilder: (context, child, progress) =>
          progress == null ? child : fallback,
      errorBuilder: (context, error, stackTrace) => fallback,
    );
  }
}

/// The lowest-tier band rendered as the frame's compact 3-column logo grid
/// (frame 922:2824 "رعاة ذهبيون"): each tile is the sponsor's logo over its
/// name. Non-scrolling (it lives inside the page's ListView).
class _SponsorGrid extends StatelessWidget {
  const _SponsorGrid({
    required this.sponsors,
    required this.baseUrl,
    required this.isArabic,
  });

  final List<Sponsor> sponsors;
  final String baseUrl;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    // Frame 922:2824 (925:3030..) — 3 columns, 16px row gap, 8px column gap,
    // each tile a fixed 72-high card.
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      padding: EdgeInsets.zero,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        mainAxisSpacing: SimfTokens.space4,
        crossAxisSpacing: SimfTokens.space2,
        mainAxisExtent: 72,
      ),
      itemCount: sponsors.length,
      itemBuilder: (context, i) => _SponsorGridTile(
        id: sponsors[i].id,
        baseUrl: baseUrl,
        name: sponsors[i].localizedName(isArabic),
        initials: SponsorsScreen._badgeText(sponsors[i], isArabic),
        // Wave 3 — tap → the sponsor detail (Figma 1439:11826).
        onTap: () => context.pushNamed(
          RouteNames.sponsorDetail,
          pathParameters: <String, String>{'sponsorId': sponsors[i].id},
        ),
      ),
    );
  }
}

/// One gold-tier grid tile (frame 925:3031): a single 72-high navy card on the
/// beige hairline holding the sponsor logo above its name (12px SemiBold white,
/// centred). The logo fills the area above the name; initials are the fallback.
class _SponsorGridTile extends StatelessWidget {
  const _SponsorGridTile({
    required this.id,
    required this.baseUrl,
    required this.name,
    required this.initials,
    required this.onTap,
  });

  final String id;
  final String baseUrl;
  final String name;
  final String initials;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.navyDeep,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Expanded(
                child: _SponsorLogo(
                  id: id,
                  baseUrl: baseUrl,
                  fallbackInitials: initials,
                  hero: false,
                ),
              ),
              const SizedBox(height: SimfTokens.space2),
              Text(
                name,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w600,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
