import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart' show RouteNames;
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/gallery/data/media_repository.dart';
import 'package:simf_app/features/gallery/widgets/coverage_tabs.dart';
import 'package:simf_app/features/gallery/widgets/gallery_body.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// The media model + provider live in `data/`; re-exported so existing imports
// (`MediaItem` / `MediaKind` / `mediaItemsProvider` off this screen — the gallery
// + archive tests) keep resolving.
export 'data/media_models.dart';
export 'data/media_repository.dart';

/// Media gallery — route: `RouteNames.gallery` · Figma 947:3764
///
/// Contract: this screen owns the **gallery** tab of the media-coverage hub;
/// the other two tabs navigate to [RouteNames.news] / [RouteNames.mediaPartners]
/// rather than swapping content in place. Video *playback* (opening the
/// external `VideoUrl`) is still deferred.
class GalleryScreen extends ConsumerWidget {
  const GalleryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final media = ref.watch(mediaItemsProvider);
    // The data-package base URL already includes `/api/v1`; the tile builds
    // `{base}/app/media/{id}/(thumbnail|image)` from it.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
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
              error: (_, __) => SimfRefreshableMessage(
                onRefresh: onRefresh,
                child: SimfErrorState(
                  message: l10n.galleryError,
                  retryLabel: l10n.retryLabel,
                  onRetry: () => ref.invalidate(mediaItemsProvider),
                ),
              ),
              data: (items) {
                if (items.isEmpty) {
                  return SimfRefreshableMessage(
                    onRefresh: onRefresh,
                    child: SimfEmptyState(
                      icon: Icons.photo_library_outlined,
                      message: l10n.galleryEmpty,
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
