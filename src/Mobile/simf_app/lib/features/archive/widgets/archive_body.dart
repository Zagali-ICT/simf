import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/archive/data/archive_models.dart';
import 'package:simf_app/features/archive/data/archive_repository.dart';
import 'package:simf_app/features/archive/widgets/archive_bullet.dart';
import 'package:simf_app/features/archive/widgets/archive_edition_pills.dart';
import 'package:simf_app/features/archive/widgets/archive_gallery_row.dart';
import 'package:simf_app/features/archive/widgets/archive_notice_banner.dart';
import 'package:simf_app/features/archive/widgets/archive_past_speakers_row.dart';
import 'package:simf_app/features/archive/widgets/archive_place_time_row.dart';
import 'package:simf_app/features/archive/widgets/archive_session_title_card.dart';
import 'package:simf_app/features/archive/widgets/archive_stat_row.dart';

class ArchiveBody extends ConsumerWidget {
  const ArchiveBody({
    required this.l10n,
    required this.editions,
    required this.selected,
    required this.onSelect,
    super.key,
  });

  final AppL10n l10n;
  final List<ArchiveEdition> editions;
  final ArchiveEdition selected;
  final ValueChanged<String> onSelect;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isArabic = l10n.isArabic;
    final detail = ref.watch(archiveEditionDetailProvider(selected.id));
    final d = detail.asData?.value;

    final summary = d?.localizedSummary(isArabic: isArabic) ??
        selected.localizedSummary(isArabic: isArabic);
    final location = d?.localizedLocation(isArabic: isArabic);
    final dateLabel = d?.localizedDateLabel(isArabic: isArabic);

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
          text: selected.localizedTitle(isArabic: isArabic),
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
            ArchiveSessionTitleCard(
                text: d.sessionTitles[i].localized(isArabic: isArabic),),
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
