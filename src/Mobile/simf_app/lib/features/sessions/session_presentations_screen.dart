import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../core/sharing/content_sharer.dart';
import 'data/presentation_models.dart';
import 'data/presentation_repository.dart';
import 'widgets/session_filter_tabs.dart';

/// **Session presentations** — App "عروض الجلسات" (Figma 1388:7621, Approved
/// account). The downloadable decks grouped by event day, each card a file icon
/// + the session title + the presenting speaker + a gold تحميل button that
/// fetches the bytes through the authenticated client and hands them to the OS
/// share/save sheet. Reads `GET /app/presentations`.
class SessionPresentationsScreen extends ConsumerStatefulWidget {
  const SessionPresentationsScreen({super.key});

  @override
  ConsumerState<SessionPresentationsScreen> createState() =>
      _SessionPresentationsScreenState();
}

class _SessionPresentationsScreenState
    extends ConsumerState<SessionPresentationsScreen> {
  // 0 = الكل (all); 1..n = the nth distinct event day.
  int _dayTab = 0;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final presentations = ref.watch(presentationsProvider);

    return KsaPage(
      title: l10n.sessionPresentationsTitle,
      onBack: () => ksaBackOrHome(context),
      body: presentations.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
        error: (_, __) => KsaErrorState(
          message: l10n.presentationsError,
          retryLabel: l10n.retryLabel,
          onRetry: () => ref.invalidate(presentationsProvider),
        ),
        data: (page) => _Body(
          items: page.items,
          dayTab: _dayTab,
          onDayTab: (i) => setState(() => _dayTab = i),
          l10n: l10n,
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body({
    required this.items,
    required this.dayTab,
    required this.onDayTab,
    required this.l10n,
  });

  final List<PresentationItem> items;
  final int dayTab;
  final ValueChanged<int> onDayTab;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return KsaEmptyState(
        icon: Icons.description_outlined,
        message: l10n.presentationsEmpty,
      );
    }

    final isArabic = l10n.isArabic;
    final days = _distinctDays(items);
    final tabLabels = <String>[
      l10n.sessionsTabAll,
      for (var i = 0; i < days.length; i++) l10n.eventDayLabel(i + 1),
    ];
    // Guard against a stale selection when the day set shrinks on refresh.
    final activeTab = dayTab < tabLabels.length ? dayTab : 0;
    final visible = activeTab == 0
        ? items
        : items
            .where((p) => _sameDay(p.sessionStartLocal, days[activeTab - 1]))
            .toList(growable: false);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space2),
        SessionFilterTabs(
          labels: tabLabels,
          selectedIndex: activeTab,
          onSelected: onDayTab,
        ),
        const SizedBox(height: SimfTokens.space3),
        Expanded(
          child: ListView.separated(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              0,
              SimfTokens.space4,
              SimfTokens.space5,
            ),
            itemCount: visible.length,
            separatorBuilder: (_, __) =>
                const SizedBox(height: SimfTokens.space3),
            itemBuilder: (context, index) => _PresentationCard(
              item: visible[index],
              isArabic: isArabic,
            ),
          ),
        ),
      ],
    );
  }

  /// The distinct device-local days present in [items], ascending.
  List<DateTime> _distinctDays(List<PresentationItem> items) {
    final byKey = <String, DateTime>{};
    for (final p in items) {
      final local = p.sessionStartLocal;
      final key = '${local.year}-${local.month}-${local.day}';
      byKey.putIfAbsent(key, () => DateTime(local.year, local.month, local.day));
    }
    final days = byKey.values.toList()..sort();
    return days;
  }

  bool _sameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;
}

/// One presentation card with its own download-in-progress state.
class _PresentationCard extends ConsumerStatefulWidget {
  const _PresentationCard({required this.item, required this.isArabic});

  final PresentationItem item;
  final bool isArabic;

  @override
  ConsumerState<_PresentationCard> createState() => _PresentationCardState();
}

class _PresentationCardState extends ConsumerState<_PresentationCard> {
  bool _downloading = false;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final item = widget.item;
    final speaker = item.localizedSpeaker(widget.isArabic);

    return KsaCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            const _FileIcon(),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    item.localizedSessionTitle(widget.isArabic),
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textLg,
                    ),
                  ),
                  if (speaker != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space1),
                    Text(
                      speaker,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ],
                  const SizedBox(height: SimfTokens.space3),
                  _DownloadButton(
                    downloading: _downloading,
                    label: _downloading
                        ? l10n.presentationDownloading
                        : l10n.presentationDownload,
                    onTap: _downloading ? null : () => _download(l10n),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _download(AppL10n l10n) async {
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _downloading = true);
    try {
      final bytes = await ref
          .read(presentationRepositoryProvider)
          .downloadFile(widget.item.id);
      await shareBinaryContent(
        bytes: bytes,
        filename: widget.item.fileName,
        mimeType: widget.item.contentType,
      );
    } catch (_) {
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.presentationDownloadError)),
      );
    } finally {
      if (mounted) {
        setState(() => _downloading = false);
      }
    }
  }
}

class _FileIcon extends StatelessWidget {
  const _FileIcon();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 40,
      height: 40,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: const Icon(
        Icons.description_outlined,
        size: 20,
        color: SimfTokens.beigeBorder,
      ),
    );
  }
}

/// The gold تحميل button (Figma 1388:7621), with a spinner while downloading.
class _DownloadButton extends StatelessWidget {
  const _DownloadButton({
    required this.downloading,
    required this.label,
    required this.onTap,
  });

  final bool downloading;
  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.accent,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space3,
            vertical: SimfTokens.space2,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              if (downloading)
                const SizedBox(
                  width: 14,
                  height: 14,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: SimfTokens.navy,
                  ),
                )
              else
                const Icon(
                  Icons.download_rounded,
                  size: 16,
                  color: SimfTokens.navy,
                ),
              const SizedBox(width: SimfTokens.space1),
              Text(
                label,
                style: const TextStyle(
                  color: SimfTokens.navy,
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
