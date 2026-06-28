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
    return KsaPage(
      title: l10n.archiveTitle,
      onBack: () => ksaBackOrHome(context),
      body: editions.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        // Pull-to-retry: a scrollable error state under KsaRefresh.
        error: (_, __) => KsaRefresh(
          onRefresh: _refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            children: <Widget>[
              KsaErrorState(
                message: l10n.archiveError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(archiveEditionsProvider),
              ),
            ],
          ),
        ),
        data: (items) {
          if (items.isEmpty) {
            // Pull-to-retry: a scrollable empty state under KsaRefresh.
            return KsaRefresh(
              onRefresh: _refresh,
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: <Widget>[
                  KsaEmptyState(
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
          return KsaRefresh(
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
          const SizedBox(height: SimfTokens.space4),
          for (var i = 0; i < d.sessionTitles.length; i++) ...<Widget>[
            if (i > 0) const SizedBox(height: SimfTokens.space2),
            _SessionTitleCard(text: d.sessionTitles[i].localized(isArabic)),
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
      height: 104,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: items.length,
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space2),
        itemBuilder: (context, index) =>
            _GalleryTile(item: items[index], isArabic: isArabic),
      ),
    );
  }
}

/// One gallery thumbnail (frame node 926:3299): a 104×104 rounded square — the
/// real photo (cover-filled) under a bottom-to-navy gradient scrim, or a navy
/// glyph placeholder for a video / non-servable url. No border, no caption
/// (matches the frame).
class _GalleryTile extends StatelessWidget {
  const _GalleryTile({required this.item, required this.isArabic});

  final ArchiveMediaItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final showImage = !item.isVideo && _isHttpUrl(item.url);
    return Container(
      width: 104,
      height: 104,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          if (showImage)
            Image.network(
              item.url,
              fit: BoxFit.cover,
              loadingBuilder: (context, child, progress) =>
                  progress == null ? child : _placeholder,
              errorBuilder: (context, error, stackTrace) => _placeholder,
            )
          else
            _placeholder,
          // Frame's bottom-to-rgba(0,16,48,0.8) scrim.
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            height: 40,
            child: DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.bottomCenter,
                  end: Alignment.topCenter,
                  colors: <Color>[
                    SimfTokens.navy.withValues(alpha: 0.8),
                    SimfTokens.navy.withValues(alpha: 0),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget get _placeholder => Center(
        child: Icon(
          item.isVideo ? Icons.play_circle_outline : Icons.image_outlined,
          color: SimfTokens.accent,
          size: 28,
        ),
      );
}

/// The past-speakers row (frame node 927:3347): up to four 72-wide tiles spread
/// across the width — each a 72×72 rounded-rect photo over a centred name. With
/// more than four speakers, the first three show + a bordered "+N / آخرون"
/// overflow card.
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
    final hasOverflow = speakers.length > 4;
    final shown =
        (hasOverflow ? speakers.take(3) : speakers.take(4)).toList();
    final overflow = speakers.length - shown.length;
    // Wrap (not Row) so the four fixed-72 tiles spread like the frame on a
    // normal width but wrap to a second line instead of overflowing on a very
    // narrow (~320px) device.
    return Wrap(
      alignment: WrapAlignment.spaceBetween,
      runSpacing: SimfTokens.space3,
      children: <Widget>[
        for (final s in shown)
          _PastSpeakerCard(
            name: s.localized(isArabic),
            photoUrl: s.photoRelativePath,
          ),
        if (overflow > 0)
          _PastSpeakerOverflow(count: overflow, label: l10n.archiveOthersLabel),
      ],
    );
  }
}

/// One past-speaker tile (frame node 927:3346): a 72×72 rounded-rect (r8) photo
/// — the real avatar when [photoUrl] is an absolute http(s) url, else the gold
/// initials — over a centred white 12px SemiBold name.
class _PastSpeakerCard extends StatelessWidget {
  const _PastSpeakerCard({required this.name, this.photoUrl});

  final String name;
  final String? photoUrl;

  @override
  Widget build(BuildContext context) {
    final initials = _speakerInitials(name);
    final fallback = Center(
      child: Text(
        initials,
        style: const TextStyle(
          color: SimfTokens.accent,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textLg,
        ),
      ),
    );
    final showPhoto = _isHttpUrl(photoUrl);
    return SizedBox(
      width: 72,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 72,
            height: 72,
            clipBehavior: Clip.antiAlias,
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(SimfTokens.radius),
            ),
            child: showPhoto
                ? Image.network(
                    photoUrl!,
                    width: 72,
                    height: 72,
                    fit: BoxFit.cover,
                    gaplessPlayback: true,
                    loadingBuilder: (context, child, progress) =>
                        progress == null ? child : fallback,
                    errorBuilder: (context, error, stackTrace) => fallback,
                  )
                : fallback,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            name,
            maxLines: 2,
            textAlign: TextAlign.center,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
              height: 1.2,
            ),
          ),
        ],
      ),
    );
  }
}

/// The past-speakers overflow tile (frame node 927:3343): a 72×72 beige-bordered
/// rounded-rect (r8) with a big gold "+N" over the white "آخرون" label.
class _PastSpeakerOverflow extends StatelessWidget {
  const _PastSpeakerOverflow({required this.count, required this.label});

  final int count;
  final String label;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 72,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 72,
            height: 72,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(SimfTokens.radius),
              border: Border.all(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
            ),
            child: Text(
              '+$count',
              textDirection: TextDirection.ltr,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontSize: SimfTokens.textTitle,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            label,
            maxLines: 1,
            textAlign: TextAlign.center,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

/// The first letters of up to two words of a speaker name, for the avatar
/// fallback.
String _speakerInitials(String name) {
  final trimmed = name.trim();
  if (trimmed.isEmpty) {
    return '—';
  }
  return trimmed
      .split(RegExp(r'\s+'))
      .where((w) => w.isNotEmpty)
      .take(2)
      .map((w) => w.characters.first)
      .join();
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

/// One session-title card (frame node 927:3308): a 48-high navy box on the beige
/// 0.2px hairline, the title right-aligned in beige 14px SemiBold (frame's
/// bordered card, not a bare bullet).
class _SessionTitleCard extends StatelessWidget {
  const _SessionTitleCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      width: double.infinity,
      alignment: Alignment.centerRight,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        textAlign: TextAlign.right,
        style: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontWeight: FontWeight.w600,
          fontSize: SimfTokens.textMd,
        ),
      ),
    );
  }
}

/// The edition-selector pills (frame node 925:3248): one pill per edition,
/// **equal-width** and filling the row (frame `flex-1`, 16px gap). The selected
/// pill is solid gold (white text); the rest are bordered navy cards with beige
/// text. Equal-flex means N pills always fit (they share the width); the frame
/// shows three.
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
    return Row(
      children: <Widget>[
        for (var i = 0; i < editions.length; i++) ...<Widget>[
          if (i > 0) const SizedBox(width: SimfTokens.space4),
          Expanded(
            child: _EditionPill(
              label: l10n.archiveEditionPill(editions[i].year),
              active: editions[i].id == selectedId,
              onTap: () => onSelect(editions[i].id),
            ),
          ),
        ],
      ],
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
          alignment: Alignment.center,
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
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

/// The two stat tiles (frame node 926:3285): a big gold number over its label,
/// in a bordered navy box. The current frame shows exactly **two** tiles —
/// الفعاليات (activities/sessions) and المتحدثون (speakers); it omits the
/// الحضور (attendees) tile the earlier mock carried (owner: match the frame).
class _StatRow extends StatelessWidget {
  const _StatRow({required this.l10n, required this.edition});

  final AppL10n l10n;
  final ArchiveEdition edition;

  @override
  Widget build(BuildContext context) {
    // RTL: the first child is at the inline start (physical right) — الفعاليات,
    // then المتحدثون at the inline end (left), mirroring the frame's two tiles.
    return Row(
      children: <Widget>[
        Expanded(
          child: _StatTile(
            value: edition.sessions,
            label: l10n.archiveStatSessions,
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _StatTile(
            value: edition.speakers,
            label: l10n.archiveStatSpeakers,
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
