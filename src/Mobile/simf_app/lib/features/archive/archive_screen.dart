import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/archive_models.dart';

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
/// edition-selector pills (active pill is solid gold), then the selected
/// edition's detail — bulleted gold title (عنوان الملتقى), summary (نبذة), a
/// two-column المكان / الزمن row, and three stat tiles (المتحدثون / الحضور /
/// الفعاليات). The frame's media-gallery, session-titles and past-speakers
/// sections are not backed by the current model (see blockers) and are omitted
/// rather than faked.
class ArchiveScreen extends ConsumerStatefulWidget {
  const ArchiveScreen({super.key});

  @override
  ConsumerState<ArchiveScreen> createState() => _ArchiveScreenState();
}

class _ArchiveScreenState extends ConsumerState<ArchiveScreen> {
  // The selected edition id; null until the editions arrive (then the first).
  String? _selectedId;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final editions = ref.watch(archiveEditionsProvider);
    return KsaPage(
      title: l10n.archiveTitle,
      onBack: () => ksaBackOrHome(context),
      body: editions.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => KsaErrorState(
          message: l10n.archiveError,
          retryLabel: l10n.retryLabel,
          onRetry: () => ref.invalidate(archiveEditionsProvider),
        ),
        data: (items) {
          if (items.isEmpty) {
            return KsaEmptyState(
              icon: Icons.bookmark_outline,
              message: l10n.archiveEmpty,
            );
          }
          // Default the selection to the most-recent edition (the list is
          // newest-first from the API) once, on first data.
          final selected = items.firstWhere(
            (e) => e.id == _selectedId,
            orElse: () => items.first,
          );
          return _ArchiveBody(
            l10n: l10n,
            editions: items,
            selected: selected,
            onSelect: (id) => setState(() => _selectedId = id),
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
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space2,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        // Notice banner — beige hairline, centred beige text (node 925:3222).
        _NoticeBanner(text: l10n.archiveNotice),
        const SizedBox(height: SimfTokens.space5),

        // "اختار ملتقى" + the edition-selector pill row (node 927:3352).
        _SectionLabel(text: l10n.archivePickEdition),
        const SizedBox(height: SimfTokens.space4),
        _EditionPills(
          editions: editions,
          selectedId: selected.id,
          onSelect: onSelect,
        ),
        const SizedBox(height: SimfTokens.space6),

        // عنوان الملتقى — bulleted gold title (node 926:3277).
        _SectionLabel(text: l10n.archiveTitleLabel),
        const SizedBox(height: SimfTokens.space2),
        _Bullet(
          text: selected.localizedTitle(isArabic),
          color: SimfTokens.accent,
          bold: true,
        ),

        // نبذة — summary (node 926:3276).
        if (summary != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          _SectionLabel(text: l10n.archiveSummaryLabel),
          const SizedBox(height: SimfTokens.space2),
          _Bullet(text: summary, color: SimfTokens.beigeBorder),
        ],

        // المكان / الزمن — two-column row (node 926:3284). Each shows only when
        // the lazily-loaded detail provides it.
        if (location != null || dateLabel != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          _PlaceTimeRow(
            l10n: l10n,
            location: location,
            dateLabel: dateLabel,
          ),
        ],

        // Stat tiles — المتحدثون / الحضور / الفعاليات (node 926:3285).
        const SizedBox(height: SimfTokens.space6),
        _StatRow(l10n: l10n, edition: selected),

        // D-432 — the rich lists, each shown only when the lazily-loaded detail
        // provides it (node 24-01): الصور والفيديو · عناوين الجلسات ·
        // المتحدثون السابقون.
        if (d != null && d.gallery.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          _SectionLabel(text: l10n.archiveGalleryLabel),
          const SizedBox(height: SimfTokens.space3),
          _GalleryRow(items: d.gallery, isArabic: isArabic),
        ],
        if (d != null && d.sessionTitles.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          _SectionLabel(text: l10n.archiveSessionsLabel),
          const SizedBox(height: SimfTokens.space2),
          for (final s in d.sessionTitles) ...<Widget>[
            _Bullet(text: s.localized(isArabic), color: SimfTokens.beigeBorder),
            const SizedBox(height: SimfTokens.space1),
          ],
        ],
        if (d != null && d.pastSpeakers.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          _SectionLabel(text: l10n.archivePastSpeakersLabel),
          const SizedBox(height: SimfTokens.space3),
          _PastSpeakersRow(
            speakers: d.pastSpeakers,
            isArabic: isArabic,
            l10n: l10n,
          ),
        ],
      ],
    );
  }
}

/// The archive gallery row (node 24-01 "الصور والفيديو"): a horizontal strip of
/// thumbnail tiles. P6 — D-440: an image item whose `url` is an absolute http(s)
/// link renders the real photo (Image.network, cover-filled); a video item or a
/// blank/relative url falls back to the photo / play glyph placeholder.
class _GalleryRow extends StatelessWidget {
  const _GalleryRow({required this.items, required this.isArabic});

  final List<ArchiveMediaItem> items;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 96,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: items.length,
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space3),
        itemBuilder: (context, index) =>
            _GalleryTile(item: items[index], isArabic: isArabic),
      ),
    );
  }
}

/// One gallery thumbnail — the real photo when the item is an image with an
/// absolute url, else a glyph placeholder (with the caption).
class _GalleryTile extends StatelessWidget {
  const _GalleryTile({required this.item, required this.isArabic});

  final ArchiveMediaItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final caption = item.localizedCaption(isArabic);
    final placeholder = _placeholder(caption);
    final showImage = !item.isVideo && _isHttpUrl(item.url);
    return Container(
      width: 120,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: showImage
          ? Image.network(
              item.url,
              width: 120,
              height: 96,
              fit: BoxFit.cover,
              loadingBuilder: (context, child, progress) =>
                  progress == null ? child : placeholder,
              errorBuilder: (context, error, stackTrace) => placeholder,
            )
          : placeholder,
    );
  }

  Widget _placeholder(String? caption) {
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space2),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Icon(
            item.isVideo ? Icons.play_circle_outline : Icons.image_outlined,
            color: SimfTokens.accent,
            size: 28,
          ),
          if (caption != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space1),
            Text(
              caption,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textXs,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// The past-speakers row (node 24-01 "المتحدثون السابقون"): up to 4 initial
/// avatars, then a gold "+N آخرون" overflow tile.
class _PastSpeakersRow extends StatelessWidget {
  const _PastSpeakersRow({
    required this.speakers,
    required this.isArabic,
    required this.l10n,
  });

  final List<ArchivePastSpeaker> speakers;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    const maxAvatars = 4;
    final shown = speakers.take(maxAvatars).toList();
    final overflow = speakers.length - shown.length;
    return Wrap(
      spacing: SimfTokens.space3,
      runSpacing: SimfTokens.space3,
      children: <Widget>[
        for (final s in shown)
          _SpeakerChip(
            name: s.localized(isArabic),
            photoUrl: s.photoRelativePath,
          ),
        if (overflow > 0)
          Container(
            height: 40,
            padding:
                const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: SimfTokens.accent.withValues(alpha: 0.15),
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              border: Border.all(color: SimfTokens.accent),
            ),
            child: Text(
              l10n.archiveMoreCount(overflow),
              style: const TextStyle(
                color: SimfTokens.accent,
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
      ],
    );
  }
}

class _SpeakerChip extends StatelessWidget {
  const _SpeakerChip({required this.name, this.photoUrl});

  final String name;

  /// P6 — D-440: an absolute http(s) photo url renders the real avatar; a blank
  /// or relative value falls back to the initials.
  final String? photoUrl;

  @override
  Widget build(BuildContext context) {
    final initials = name.trim().isEmpty
        ? '—'
        : name
            .trim()
            .split(RegExp(r'\s+'))
            .where((w) => w.isNotEmpty)
            .take(2)
            .map((w) => w.characters.first)
            .join();
    final fallback = Text(
      initials,
      style: const TextStyle(
        color: SimfTokens.accent,
        fontWeight: FontWeight.w700,
        fontSize: SimfTokens.textSm,
      ),
    );
    final showPhoto = _isHttpUrl(photoUrl);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 40,
          height: 40,
          alignment: Alignment.center,
          clipBehavior: Clip.antiAlias,
          decoration: const BoxDecoration(
            color: SimfTokens.navyDeep,
            shape: BoxShape.circle,
          ),
          child: showPhoto
              ? Image.network(
                  photoUrl!,
                  width: 40,
                  height: 40,
                  fit: BoxFit.cover,
                  gaplessPlayback: true,
                  loadingBuilder: (context, child, progress) =>
                      progress == null ? child : fallback,
                  errorBuilder: (context, error, stackTrace) => fallback,
                )
              : fallback,
        ),
        const SizedBox(width: SimfTokens.space2),
        ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 120),
          child: Text(
            name,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ),
      ],
    );
  }
}

/// The beige-bordered notice banner (node 925:3222): centred beige text in a
/// navy-deep box with the 0.5px beige hairline.
class _NoticeBanner extends StatelessWidget {
  const _NoticeBanner({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      alignment: Alignment.center,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairlineBold,
        ),
      ),
      child: Text(
        text,
        textAlign: TextAlign.center,
        style: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textMd,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}

/// A plain white section label (node 925:3259 etc.): 16px / Medium.
class _SectionLabel extends StatelessWidget {
  const _SectionLabel({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: Colors.white,
        fontSize: SimfTokens.textLg,
        fontWeight: FontWeight.w500,
      ),
    );
  }
}

/// The edition-selector pills (node 925:3248): one pill per edition. The
/// selected pill is solid gold (white text); the rest are bordered navy
/// cards with beige text. Horizontally scrollable when the editions overflow.
class _EditionPills extends StatelessWidget {
  const _EditionPills({
    required this.editions,
    required this.selectedId,
    required this.onSelect,
  });

  final List<ArchiveEdition> editions;
  final String selectedId;
  final ValueChanged<String> onSelect;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SizedBox(
      height: 48,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: editions.length,
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space4),
        itemBuilder: (context, index) {
          final edition = editions[index];
          final active = edition.id == selectedId;
          return _EditionPill(
            label: l10n.archiveEditionPill(edition.year),
            active: active,
            onTap: () => onSelect(edition.id),
          );
        },
      ),
    );
  }
}

class _EditionPill extends StatelessWidget {
  const _EditionPill({
    required this.label,
    required this.active,
    required this.onTap,
  });

  final String label;
  final bool active;
  final VoidCallback onTap;

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
        child: Container(
          height: 48,
          constraints: const BoxConstraints(minWidth: 96),
          alignment: Alignment.center,
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
          child: Text(
            label,
            style: TextStyle(
              color: active ? Colors.white : SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
    );
  }
}

/// A bulleted list item (the frame's disc bullets, node 925:3258 / 925:3264):
/// a leading inline-start disc and the value text. The disc colour matches the
/// text so gold titles get a gold bullet, beige body gets a beige bullet.
class _Bullet extends StatelessWidget {
  const _Bullet({
    required this.text,
    required this.color,
    this.bold = false,
  });

  final String text;
  final Color color;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Padding(
          padding: const EdgeInsetsDirectional.only(
            top: 7,
            end: SimfTokens.space2,
          ),
          child: Container(
            width: 5,
            height: 5,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
        ),
        Expanded(
          child: Text(
            text,
            style: TextStyle(
              color: color,
              fontSize: bold ? SimfTokens.textLg : SimfTokens.textMd,
              fontWeight: bold ? FontWeight.w600 : FontWeight.w500,
              height: 1.4,
            ),
          ),
        ),
      ],
    );
  }
}

/// The المكان / الزمن two-column row (node 926:3284): a gold inline-start
/// divider between the place column and the time column.
class _PlaceTimeRow extends StatelessWidget {
  const _PlaceTimeRow({
    required this.l10n,
    required this.location,
    required this.dateLabel,
  });

  final AppL10n l10n;
  final String? location;
  final String? dateLabel;

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: _LabelledBullet(
              label: l10n.archivePlaceLabel,
              value: location,
            ),
          ),
          Container(
            width: SimfTokens.hairlineBold,
            color: SimfTokens.accent,
            margin: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
          ),
          Expanded(
            child: _LabelledBullet(
              label: l10n.archiveTimeLabel,
              value: dateLabel,
            ),
          ),
        ],
      ),
    );
  }
}

/// A white label over an optional beige bulleted value (one column of the
/// المكان / الزمن row).
class _LabelledBullet extends StatelessWidget {
  const _LabelledBullet({required this.label, required this.value});

  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        _SectionLabel(text: label),
        if (value != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          _Bullet(text: value!, color: SimfTokens.beigeBorder),
        ],
      ],
    );
  }
}

/// The three stat tiles (node 926:3285): a big gold number over its label, in
/// a bordered navy box. Order matches the frame: المتحدثون / الحضور / الفعاليات.
class _StatRow extends StatelessWidget {
  const _StatRow({required this.l10n, required this.edition});

  final AppL10n l10n;
  final ArchiveEdition edition;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: _StatTile(
            value: edition.speakers,
            label: l10n.archiveStatSpeakers,
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _StatTile(
            value: edition.attendees,
            label: l10n.archiveStatAttendees,
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _StatTile(
            value: edition.sessions,
            label: l10n.archiveStatSessions,
          ),
        ),
      ],
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({required this.value, required this.label});

  final int value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Column(
        children: <Widget>[
          Text(
            '$value',
            textDirection: TextDirection.ltr,
            style: const TextStyle(
              color: SimfTokens.accent,
              fontSize: SimfTokens.textTitle,
              fontWeight: FontWeight.w600,
              height: 1,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            label,
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ],
      ),
    );
  }
}

/// P6 — D-440: true when [value] is an absolute http(s) URL the app can load
/// directly. The archive media `url` / past-speaker `photoRelativePath` hold
/// either such a URL (rendered) or a legacy relative path (falls back to the
/// glyph / initials), so the app never tries to load an unservable path.
bool _isHttpUrl(String? value) {
  final v = value?.trim().toLowerCase() ?? '';
  return v.startsWith('http://') || v.startsWith('https://');
}
