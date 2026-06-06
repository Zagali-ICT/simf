import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
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

/// Page 024 — الأرشيف · Archive (#24, `/archive`, Guest+).
///
/// **Public.** Lists the past editions (year · title · attendees/sessions/
/// speakers); tapping an edition opens a sheet that lazily loads the fuller
/// detail (`GET /app/archive/{id}` — location, dates, summary).
class ArchiveScreen extends ConsumerWidget {
  const ArchiveScreen({super.key});

  Future<ArchiveEditionDetail?> _detail(WidgetRef ref, String id) async {
    try {
      final client = ref.read(simfApiClientProvider);
      return await client.get<ArchiveEditionDetail>(
        '/app/archive/$id',
        decodeData: (data) => ArchiveEditionDetail.fromJson(
          (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
        ),
      );
    } on ApiFailure {
      return null;
    }
  }

  void _open(BuildContext context, WidgetRef ref, ArchiveEdition edition) {
    final l10n = AppL10n.of(context);
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (_) => _EditionSheet(
        l10n: l10n,
        edition: edition,
        detail: _detail(ref, edition.id),
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final editions = ref.watch(archiveEditionsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.archiveTitle)),
      body: SafeArea(
        child: editions.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _Error(
            message: l10n.archiveError,
            onRetry: () => ref.invalidate(archiveEditionsProvider),
          ),
          data: (items) {
            if (items.isEmpty) {
              return _Empty(message: l10n.archiveEmpty);
            }
            final isArabic = l10n.isArabic;
            return ListView.separated(
              padding: const EdgeInsets.all(SimfTokens.space4),
              itemCount: items.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(height: SimfTokens.space2),
              itemBuilder: (context, index) {
                final edition = items[index];
                return Card(
                  margin: EdgeInsets.zero,
                  clipBehavior: Clip.antiAlias,
                  child: ListTile(
                    onTap: () => _open(context, ref, edition),
                    leading: CircleAvatar(
                      backgroundColor: SimfTokens.field,
                      child: Text(
                        "'${(edition.year % 100).toString().padLeft(2, '0')}",
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                    ),
                    title: Text(
                      edition.localizedTitle(isArabic),
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    subtitle: Text(
                      l10n.archiveStats(
                        edition.attendees,
                        edition.sessions,
                        edition.speakers,
                      ),
                    ),
                    trailing:
                        const Icon(Icons.chevron_right, color: SimfTokens.accent),
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _EditionSheet extends StatelessWidget {
  const _EditionSheet({
    required this.l10n,
    required this.edition,
    required this.detail,
  });

  final AppL10n l10n;
  final ArchiveEdition edition;
  final Future<ArchiveEditionDetail?> detail;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space5,
        0,
        SimfTokens.space5,
        SimfTokens.space6,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            '${edition.year} · ${edition.localizedTitle(isArabic)}',
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.archiveStats(
              edition.attendees,
              edition.sessions,
              edition.speakers,
            ),
            style: const TextStyle(color: SimfTokens.inkMuted),
          ),
          const SizedBox(height: SimfTokens.space3),
          FutureBuilder<ArchiveEditionDetail?>(
            future: detail,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return Text(
                  l10n.loadingLabel,
                  style: const TextStyle(color: SimfTokens.inkMuted),
                );
              }
              final d = snapshot.data;
              final summary =
                  d?.localizedSummary(isArabic) ?? edition.localizedSummary(isArabic);
              final location = d?.localizedLocation(isArabic);
              final dateLabel = d?.localizedDateLabel(isArabic);
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  if (dateLabel != null || location != null)
                    Text(
                      <String>[
                        if (dateLabel != null) dateLabel,
                        if (location != null) location,
                      ].join(' · '),
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                  if (summary != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    Text(summary),
                  ],
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(Icons.bookmark_outline, size: 56, color: SimfTokens.inkMuted),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
        ],
      ),
    );
  }
}

class _Error extends StatelessWidget {
  const _Error({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
