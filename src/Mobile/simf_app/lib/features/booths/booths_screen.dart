import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../venuemap/data/venue_map_models.dart';
import '../venuemap/data/venue_map_repository.dart';

/// Page 022 — الأجنحة · Booths (#22, `/booths`, Guest+).
///
/// **Public.** Reuses the shipped booth reads (`GET /app/booths` + `/{id}`,
/// D-199 / D-230) already wired in [VenueMapRepository] — the list of exhibitor
/// booths; tapping a booth opens a bottom sheet with the lazily-loaded
/// description. UI is interim (final visuals from SIMF-VID-001).
class BoothsScreen extends ConsumerStatefulWidget {
  const BoothsScreen({super.key});

  @override
  ConsumerState<BoothsScreen> createState() => _BoothsScreenState();
}

class _BoothsScreenState extends ConsumerState<BoothsScreen> {
  bool _loading = true;
  bool _error = false;
  List<BoothSummary> _booths = const <BoothSummary>[];

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final booths = await ref.read(venueMapRepositoryProvider).getBooths();
      if (!mounted) {
        return;
      }
      setState(() {
        _booths = booths;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _loading = false;
      });
    }
  }

  Future<BoothDetail?> _safeDetail(String id) async {
    try {
      return await ref.read(venueMapRepositoryProvider).getBoothDetail(id);
    } on ApiFailure {
      return null;
    }
  }

  void _openBooth(BoothSummary booth) {
    final l10n = AppL10n.of(context);
    final detail = _safeDetail(booth.id);
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (_) => _BoothSheet(l10n: l10n, booth: booth, detail: detail),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.boothsTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _ErrorState(message: l10n.boothsError, onRetry: () => unawaited(_load()));
    }
    if (_booths.isEmpty) {
      return _EmptyState(message: l10n.boothsEmpty);
    }
    final isArabic = l10n.isArabic;
    return ListView.separated(
      padding: const EdgeInsets.all(SimfTokens.space4),
      itemCount: _booths.length,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space2),
      itemBuilder: (context, index) {
        final booth = _booths[index];
        final exhibitor = booth.localizedExhibitor(isArabic);
        final sector = booth.localizedSector(isArabic);
        final sub = <String>[
          if (exhibitor != null) exhibitor,
          if (sector != null) sector,
        ];
        return Card(
          margin: EdgeInsets.zero,
          clipBehavior: Clip.antiAlias,
          child: ListTile(
            onTap: () => _openBooth(booth),
            leading: const Icon(Icons.storefront_outlined, color: SimfTokens.accent),
            title: Text(
              booth.localizedName(isArabic),
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            subtitle: sub.isEmpty ? null : Text(sub.join(' · ')),
            trailing: booth.code.isEmpty
                ? null
                : Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: SimfTokens.space2,
                      vertical: SimfTokens.space1,
                    ),
                    decoration: BoxDecoration(
                      color: SimfTokens.field,
                      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                    ),
                    child: Text(
                      booth.code,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: SimfTokens.textXs,
                      ),
                    ),
                  ),
          ),
        );
      },
    );
  }
}

class _BoothSheet extends StatelessWidget {
  const _BoothSheet({required this.l10n, required this.booth, required this.detail});

  final AppL10n l10n;
  final BoothSummary booth;
  final Future<BoothDetail?> detail;

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
            booth.localizedName(isArabic),
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Builder(
            builder: (_) {
              final exhibitor = booth.localizedExhibitor(isArabic);
              final sector = booth.localizedSector(isArabic);
              final parts = <String>[
                if (exhibitor != null) exhibitor,
                if (sector != null) sector,
              ];
              if (parts.isEmpty) {
                return const SizedBox.shrink();
              }
              return Text(
                parts.join(' · '),
                style: const TextStyle(color: SimfTokens.inkMuted),
              );
            },
          ),
          const SizedBox(height: SimfTokens.space3),
          FutureBuilder<BoothDetail?>(
            future: detail,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return Text(
                  l10n.loadingLabel,
                  style: const TextStyle(color: SimfTokens.inkMuted),
                );
              }
              final description = snapshot.data?.localizedDescription(isArabic);
              if (description == null) {
                return const SizedBox.shrink();
              }
              return Text(description);
            },
          ),
        ],
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(Icons.storefront_outlined, size: 56, color: SimfTokens.inkMuted),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

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
