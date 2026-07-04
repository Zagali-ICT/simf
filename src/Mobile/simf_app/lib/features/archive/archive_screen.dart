import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/archive_models.dart';
import 'widgets/archive_bullet.dart';
import 'widgets/archive_edition_pills.dart';
import 'widgets/archive_gallery_row.dart';
import 'widgets/archive_notice_banner.dart';
import 'widgets/archive_past_speakers_row.dart';
import 'widgets/archive_place_time_row.dart';
import 'widgets/archive_session_title_card.dart';
import 'widgets/archive_stat_row.dart';

/// `GET /app/archive` → the past editions (public, D-273).
final archiveEditionsProvider =
    FutureProvider.autoDispose<List<ArchiveEdition>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<ArchiveEdition>>(
    '/app/archive',
    decodeData: ArchiveEdition.listFromData,
  );
});

/// `GET /app/archive/{id}` → the fuller detail (location + date label) for one
/// edition, lazily loaded when an edition pill is selected (D-273).
final archiveEditionDetailProvider = FutureProvider.autoDispose
    .family<ArchiveEditionDetail?, String>((ref, id) async {
  final client = ref.watch(simfApiClientProvider);
  try {
    return await client.get<ArchiveEditionDetail>(
      '/app/archive/$id',
      decodeData: (data) => ArchiveEditionDetail.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  } on ApiFailure {
    return null;
  }
});

/// Page 024 — الأرشيف · Archive (#24, `/archive`, Guest+), rebuilt to the
/// KSA-Project frame **925:3079** on the shared navy shell.
///
/// **Public.** The same data contract as before: the list of past editions is
/// fetched once (`GET /app/archive`), and the fuller detail of the *selected*
/// edition (location + date label) is lazily loaded (`GET /app/archive/{id}`).
/// Frame mapping: a beige-bordered notice banner, an "اختار ملتقى" row of
/// equal-width edition-selector pills (active pill is solid gold), then the
/// selected edition's detail — bulleted gold title (عنوان الملتقى), summary
/// (نبذة), a two-column المكان / الزمن row, and **two** stat tiles (الفعاليات /
/// المتحدثون — the frame omits الحضور). When the lazily-loaded detail carries
/// them, the rich sections render to the frame: the الصور والفيديو gallery
/// (104×104 scrim tiles), عناوين الجلسات (bordered cards) and المتحدثون
/// السابقون (72×72 photo tiles + a "+N / آخرون" overflow card).
class ArchiveScreen extends ConsumerStatefulWidget {
  const ArchiveScreen({super.key});

  @override
  ConsumerState<ArchiveScreen> createState() => _ArchiveScreenState();
}

class _ArchiveScreenState extends ConsumerState<ArchiveScreen> {
  // The selected edition id; null until the editions arrive (then the first).
  String? _selectedId;

  // Pull-to-refresh: drop the cached editions AND the selected edition's detail
  // (the whole detail family, so whichever edition is shown — tapped or the
  // default most-recent — re-fetches its summary/gallery/sessions too), then
  // await the list re-fetch so the gold spinner stays until it arrives.
  Future<void> _refresh() async {
    ref.invalidate(archiveEditionsProvider);
    ref.invalidate(archiveEditionDetailProvider);
    await ref.read(archiveEditionsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final editions = ref.watch(archiveEditionsProvider);
    return SimfPageShell(
      title: l10n.archiveTitle,
      onBack: () => backOrHome(context),
      body: editions.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        // Pull-to-retry: a scrollable error state under SimfPullToRefresh.
        error: (_, __) => SimfPullToRefresh(
          onRefresh: _refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            children: <Widget>[
              SimfErrorState(
                message: l10n.archiveError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(archiveEditionsProvider),
              ),
            ],
          ),
        ),
        data: (items) {
          if (items.isEmpty) {
            // Pull-to-retry: a scrollable empty state under SimfPullToRefresh.
            return SimfPullToRefresh(
              onRefresh: _refresh,
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: <Widget>[
                  SimfEmptyState(
                    icon: Icons.bookmark_outline,
                    message: l10n.archiveEmpty,
                  ),
                ],
              ),
            );
          }
          // Default the selection to the most-recent edition (the list is
          // newest-first from the API) once, on first data.
          final selected = items.firstWhere(
            (e) => e.id == _selectedId,
            orElse: () => items.first,
          );
          return SimfPullToRefresh(
            onRefresh: _refresh,
            child: _ArchiveBody(
              l10n: l10n,
              editions: items,
              selected: selected,
              onSelect: (id) => setState(() => _selectedId = id),
            ),
          );
        },
      ),
    );
  }
}

/// The scrolling content: notice banner → edition selector → selected-edition
/// detail (title / summary / place·time / stats).
class _ArchiveBody extends ConsumerWidget {
  const _ArchiveBody({
    required this.l10n,
    required this.editions,
    required this.selected,
    required this.onSelect,
  });

  final AppL10n l10n;
  final List<ArchiveEdition> editions;
  final ArchiveEdition selected;
  final ValueChanged<String> onSelect;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isArabic = l10n.isArabic;
    final detail = ref.watch(archiveEditionDetailProvider(selected.id));
    final ArchiveEditionDetail? d = detail.asData?.value;

    final summary =
        d?.localizedSummary(isArabic) ?? selected.localizedSummary(isArabic);
    final location = d?.localizedLocation(isArabic);
    final dateLabel = d?.localizedDateLabel(isArabic);

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space2,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        // Notice banner — beige hairline, centred beige text (node 925:3222).
        ArchiveNoticeBanner(text: l10n.archiveNotice),
        const SizedBox(height: SimfTokens.space5),

        // "اختار ملتقى" + the edition-selector pill row (node 927:3352).
        SimfSectionHeader(title: l10n.archivePickEdition),
        const SizedBox(height: SimfTokens.space4),
        ArchiveEditionPills(
          editions: editions,
          selectedId: selected.id,
          onSelect: onSelect,
        ),
        const SizedBox(height: SimfTokens.space6),

        // عنوان الملتقى — bulleted gold title (node 926:3277).
        SimfSectionHeader(title: l10n.archiveTitleLabel),
        const SizedBox(height: SimfTokens.space2),
        ArchiveBullet(
          text: selected.localizedTitle(isArabic),
          color: SimfTokens.accent,
          bold: true,
        ),

        // نبذة — summary (node 926:3276).
        if (summary != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          SimfSectionHeader(title: l10n.archiveSummaryLabel),
          const SizedBox(height: SimfTokens.space2),
          ArchiveBullet(text: summary, color: SimfTokens.beigeBorder),
        ],

        // المكان / الزمن — two-column row (node 926:3284). Each shows only when
        // the lazily-loaded detail provides it.
        if (location != null || dateLabel != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          ArchivePlaceTimeRow(
            l10n: l10n,
            location: location,
            dateLabel: dateLabel,
          ),
        ],

        // Stat tiles — المتحدثون / الحضور / الفعاليات (node 926:3285).
        const SizedBox(height: SimfTokens.space6),
        ArchiveStatRow(l10n: l10n, edition: selected),

        // D-432 — the rich lists, each shown only when the lazily-loaded detail
        // provides it (node 24-01): الصور والفيديو · عناوين الجلسات ·
        // المتحدثون السابقون.
        if (d != null && d.gallery.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          SimfSectionHeader(title: l10n.archiveGalleryLabel),
          const SizedBox(height: SimfTokens.space3),
          ArchiveGalleryRow(items: d.gallery, isArabic: isArabic),
        ],
        if (d != null && d.sessionTitles.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          SimfSectionHeader(title: l10n.archiveSessionsLabel),
          const SizedBox(height: SimfTokens.space4),
          for (var i = 0; i < d.sessionTitles.length; i++) ...<Widget>[
            if (i > 0) const SizedBox(height: SimfTokens.space2),
            ArchiveSessionTitleCard(text: d.sessionTitles[i].localized(isArabic)),
          ],
        ],
        if (d != null && d.pastSpeakers.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          SimfSectionHeader(title: l10n.archivePastSpeakersLabel),
          const SizedBox(height: SimfTokens.space3),
          ArchivePastSpeakersRow(
            speakers: d.pastSpeakers,
            isArabic: isArabic,
            l10n: l10n,
          ),
        ],
      ],
    );
  }
}
