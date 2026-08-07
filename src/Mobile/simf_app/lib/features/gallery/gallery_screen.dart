import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/refresh.dart';
import 'data/media_repository.dart';
import 'widgets/coverage_tabs.dart';
import 'widgets/gallery_body.dart';

// The media model + provider live in `data/`; re-exported so existing imports
// (`MediaItem` / `MediaKind` / `mediaItemsProvider` off this screen — the gallery
// + archive tests) keep resolving.
export 'data/media_models.dart';
export 'data/media_repository.dart';

/// Page 030 — التغطية الإعلامية · معرض الصور والفيديوهات · Media gallery
/// (#30, `/media`, Guest+), rebuilt to the KSA-Project frame **947:3764** on the
/// shared shell.
///
/// **Public.** The frame is the *media-coverage* hub: a three-tab selector
/// (الأخبار · الشركاء الإعلاميون · معرض الصور والفيديوهات) over the active tab's
/// content. This screen owns the **gallery** tab; the other two tabs navigate
/// to their own routes ([RouteNames.news] / [RouteNames.mediaPartners]). The
/// gallery splits the media cache into two labelled sections — **الصور**
/// (image tiles) and **الفيديوهات** (video tiles with a centred play glyph) —
/// each a two-up grid of rounded tiles with a navy bottom-gradient. Tiles with
/// an uploaded bitmap render it from the public `…/app/media/{id}/…` route
/// (thumbnail preferred, image fallback) with a loading spinner and a graceful
/// fall-back to the kind icon when there is no bitmap or the fetch fails. Video
/// *playback* (opening the external `VideoUrl`) is still deferred.
class GalleryScreen extends ConsumerWidget {
  const GalleryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final media = ref.watch(mediaItemsProvider);
    // The data-package base URL already includes `/api/v1`; the tile builds
    // `{base}/app/media/{id}/(thumbnail|image)` from it.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    // Pull-to-refresh — re-fetch the media items (invalidate + await next).
    Future<void> onRefresh() => refreshAsync(ref, mediaItemsProvider.future);

    return SimfPageShell(
      title: l10n.mediaCoverageTitle,
      onBack: () => backOrHome(context),
      body: Column(
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space2,
              SimfTokens.space4,
              SimfTokens.space4,
            ),
            child: CoverageTabs(l10n: l10n),
          ),
          Expanded(
            child: media.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, __) => SimfPullToRefresh(
                onRefresh: onRefresh,
                child: SimfPullableHost(
                  child: SimfErrorState(
                    message: l10n.galleryError,
                    retryLabel: l10n.retryLabel,
                    onRetry: () => ref.invalidate(mediaItemsProvider),
                  ),
                ),
              ),
              data: (items) {
                if (items.isEmpty) {
                  return SimfPullToRefresh(
                    onRefresh: onRefresh,
                    child: SimfPullableHost(
                      child: SimfEmptyState(
                        icon: Icons.photo_library_outlined,
                        message: l10n.galleryEmpty,
                      ),
                    ),
                  );
                }
                return SimfPullToRefresh(
                  onRefresh: onRefresh,
                  child: GalleryBody(
                    items: items,
                    isArabic: l10n.isArabic,
                    baseUrl: baseUrl,
                    imagesLabel: l10n.galleryImagesSection,
                    videosLabel: l10n.galleryVideosSection,
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
