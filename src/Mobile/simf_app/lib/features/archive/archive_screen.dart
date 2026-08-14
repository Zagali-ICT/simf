import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/archive/data/archive_repository.dart';
import 'package:simf_app/features/archive/widgets/archive_body.dart';

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
  Future<void> _refresh() {
    ref.invalidate(archiveEditionDetailProvider);
    return refreshAsync(ref, archiveEditionsProvider.future);
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
        error: (_, __) => SimfRefreshableMessage(
          onRefresh: _refresh,
          child: SimfErrorState(
            message: l10n.archiveError,
            retryLabel: l10n.retryLabel,
            onRetry: () => ref.invalidate(archiveEditionsProvider),
          ),
        ),
        data: (items) {
          if (items.isEmpty) {
            return SimfRefreshableMessage(
              onRefresh: _refresh,
              child: SimfEmptyState(
                icon: Icons.bookmark_outline,
                message: l10n.archiveEmpty,
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
            child: ArchiveBody(
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
