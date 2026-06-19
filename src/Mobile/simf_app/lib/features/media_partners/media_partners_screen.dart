import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';

/// One media partner — mirrors `PublicMediaPartnerItem` (`name`/`nameArabic`).
@immutable
class MediaPartner {
  const MediaPartner({
    required this.id,
    required this.name,
    required this.nameArabic,
    this.logoRelativePath,
  });

  /// The D-357 asset category whose anonymous serve route carries a partner's
  /// uploaded logo bytes.
  static const String _logoAssetCategory = 'MediaPartnerLogo';

  final String id;
  final String name;
  final String nameArabic;

  /// Legacy free-text path carried on the public wire — **not** the rendered
  /// logo source. The card renders the partner's uploaded logo from the D-357
  /// asset route (see [logoAssetUrl]); this field (which historically held an
  /// arbitrary path / placeholder URL) is kept only to mirror the wire shape.
  final String? logoRelativePath;

  String localizedName(bool isArabic) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  /// The public, anonymous URL that serves this partner's uploaded logo bytes
  /// via the D-357 unified media-asset pipeline (the one place the route shape
  /// lives). [baseUrl] already includes the `/api/v1` segment. The route 404s
  /// when the partner has no uploaded logo, so the caller falls back to the
  /// partner's initials.
  String logoAssetUrl(String baseUrl) =>
      '$baseUrl/app/assets/$_logoAssetCategory/$id/image';

  static MediaPartner fromJson(Map<String, dynamic> json) => MediaPartner(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        logoRelativePath: json['logoRelativePath'] as String?,
      );
}

/// `GET /app/media-partners` → the flat partner list (public, D-199).
final mediaPartnersProvider =
    FutureProvider.autoDispose<List<MediaPartner>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<MediaPartner>>(
    '/app/media-partners',
    decodeData: (data) =>
        ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => MediaPartner.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
  );
});

/// Page 031 — الشركاء الإعلاميون · Media partners (#31, `/media-partners`,
/// Guest+), rebuilt to the KSA-Project frame **947:3764 "Media coverage"** on
/// the shared navy shell.
///
/// **Public.** The frame is the two-tab "المركز الاعلامي" (Media center)
/// container — الشركاء الإعلاميون (this screen, active gold) · احدث المستجدات.
/// The inactive pill navigates to the news (#29) route. (Figma 947/1049 dropped
/// the معرض الصور tab; the gallery screen #30 stays in the app, reached
/// elsewhere.) The body is a two-column grid of
/// partner cards (frame node 958:2388): a gold rounded-square logo holder over
/// the partner name on the navy KSA card. The logo is the partner's uploaded
/// asset, fetched from the public anonymous route
/// `…/app/assets/MediaPartnerLogo/{id}/image` (the D-357 unified media-asset
/// pipeline — same mechanism the CP upload writes to) with a loading spinner
/// and a graceful fall-back to the partner's initials on a gold tile when there
/// is no logo or the fetch fails.
class MediaPartnersScreen extends ConsumerWidget {
  const MediaPartnersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final partners = ref.watch(mediaPartnersProvider);
    // The data-package base URL already includes `/api/v1`; the card builds
    // `{base}/app/assets/MediaPartnerLogo/{id}/image` from it.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    return KsaPage(
      // Frame header — the container is "التغطية الإعلامية" (Media coverage),
      // not the bare "الشركاء الإعلاميون" tab label.
      title: l10n.mediaCoverageTitle,
      onBack: () => ksaBackOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space2,
              SimfTokens.space4,
              SimfTokens.space2,
            ),
            child: _MediaTabs(l10n: l10n),
          ),
          Expanded(
            child: partners.when(
              loading: () =>
                  const Center(child: CircularProgressIndicator()),
              error: (_, __) => KsaErrorState(
                message: l10n.mediaPartnersError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(mediaPartnersProvider),
              ),
              data: (items) {
                if (items.isEmpty) {
                  return KsaEmptyState(
                    icon: Icons.campaign_outlined,
                    message: l10n.mediaPartnersEmpty,
                  );
                }
                final isArabic = l10n.isArabic;
                // Frame 958:2388 — a 2-column grid of 163.5×104 partner cards
                // with a 16px gap (≈1.57 aspect).
                return GridView.builder(
                  padding: const EdgeInsets.fromLTRB(
                    SimfTokens.space4,
                    SimfTokens.space2,
                    SimfTokens.space4,
                    SimfTokens.space6,
                  ),
                  gridDelegate:
                      const SliverGridDelegateWithFixedCrossAxisCount(
                    crossAxisCount: 2,
                    mainAxisSpacing: SimfTokens.space4,
                    crossAxisSpacing: SimfTokens.space4,
                    childAspectRatio: 163.5 / 104,
                  ),
                  itemCount: items.length,
                  itemBuilder: (context, index) {
                    final partner = items[index];
                    return _PartnerCard(
                      name: partner.localizedName(isArabic),
                      logoUrl: partner.logoAssetUrl(baseUrl),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

/// The two media-center tabs (Figma 947:3764): الشركاء الإعلاميون (active gold,
/// at the inline start / right) · احدث المستجدات (inactive, routes to the news
/// screen). Shared, identical to `news_screen.dart:_MediaTabs` (the Gallery tab
/// was dropped from the strip — Figma 947/1049 show two tabs only).
class _MediaTabs extends StatelessWidget {
  const _MediaTabs({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    // Figma 947:3764 (Arabic/RTL): right→left الشركاء الإعلاميون · احدث المستجدات.
    // A Row lays children start→end, so under RTL the first child is the
    // right-most: partners (active here) then latest-updates.
    return Row(
      children: <Widget>[
        Expanded(
          child: _MediaTab(label: l10n.mediaPartnersTitle, active: true),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _MediaTab(
            label: l10n.latestUpdatesTitle,
            active: false,
            onTap: () => context.pushReplacementNamed(RouteNames.news),
          ),
        ),
      ],
    );
  }
}

/// One media-center tab pill (frame node 947:3872 / 947:3874): a 48-high,
/// 8px-radius pill — active is solid gold with **white** text; inactive is a
/// transparent pill with a beige 0.2 hairline and beige text.
class _MediaTab extends StatelessWidget {
  const _MediaTab({required this.label, required this.active, this.onTap});

  final String label;
  final bool active;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: active ? SimfTokens.accent : Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: active
            ? BorderSide.none
            : const BorderSide(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
      ),
      child: InkWell(
        onTap: active ? null : onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: SizedBox(
          height: 48,
          child: Center(
            child: Padding(
              padding:
                  const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
              child: Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                  // Figma 947 — the active gold pill carries white text.
                  color: active ? Colors.white : SimfTokens.beigeBorder,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// One partner — frame node 958:2263: the navy KSA card with a centred gold
/// rounded-square logo holder over the partner name (white 12px SemiBold).
class _PartnerCard extends StatelessWidget {
  const _PartnerCard({required this.name, required this.logoUrl});

  final String name;
  final String logoUrl;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            _PartnerLogo(url: logoUrl, name: name),
            const SizedBox(height: SimfTokens.space2),
            // Flexible so a long Arabic name (or a large OS text-scale) shrinks
            // + ellipsises inside the fixed-aspect grid cell instead of
            // overflowing the column.
            Flexible(
              child: Text(
                name,
                textAlign: TextAlign.center,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                  height: 1.3,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The gold rounded-square logo holder (frame node 958:2264). Renders the
/// partner's uploaded logo from the public anonymous asset route with a spinner
/// while it loads; falls back to the partner's initials on a gold tile when the
/// partner has no logo (the route 404s) or the fetch fails — mirroring
/// `gallery_screen.dart:_Thumbnail`.
class _PartnerLogo extends StatelessWidget {
  const _PartnerLogo({required this.url, required this.name});

  final String url;
  final String name;

  static const double _size = 48;
  static final RegExp _whitespace = RegExp(r'\s+');

  String get _initials {
    final words = name.trim().split(_whitespace);
    final letters = words
        .where((w) => w.isNotEmpty)
        .take(2)
        .map((w) => w.characters.first)
        .join();
    return letters.isEmpty ? '—' : letters.toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius:
          const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
      child: SizedBox(
        width: _size,
        height: _size,
        child: Image.network(
          url,
          fit: BoxFit.cover,
          gaplessPlayback: true,
          loadingBuilder: (context, child, progress) {
            if (progress == null) {
              return child;
            }
            return const ColoredBox(
              color: SimfTokens.navyDeep,
              child: Center(
                child: SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            );
          },
          // Initials are computed only when the fetch fails — the common
          // success path skips the split.
          errorBuilder: (context, error, stackTrace) =>
              _InitialsTile(initials: _initials),
        ),
      ),
    );
  }
}

/// The no-logo / failed-fetch fall-back: the partner's initials on the frame's
/// gold tile (navy text for contrast on gold).
class _InitialsTile extends StatelessWidget {
  const _InitialsTile({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.accent,
      child: Center(
        child: Text(
          initials,
          style: const TextStyle(
            color: SimfTokens.navy,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
            letterSpacing: 0.5,
          ),
        ),
      ),
    );
  }
}
