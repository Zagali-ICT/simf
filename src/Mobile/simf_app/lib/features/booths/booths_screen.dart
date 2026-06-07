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
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
      itemBuilder: (context, index) {
        final booth = _booths[index];
        return _BoothCard(
          booth: booth,
          isArabic: isArabic,
          onTap: () => _openBooth(booth),
        );
      },
    );
  }
}

/// One exhibitor card (mockup `.booth`): a top row carrying the storefront mark
/// and the accent code pill over a hairline, then a `.co` row with a square logo
/// tile (the booth initials) beside the company name and its grey sub-line.
/// The mockup's hall name / officer / contacts / directions blocks are omitted
/// — `GET /app/booths` carries only a bare `hallId` and no contact data (D11).
class _BoothCard extends StatelessWidget {
  const _BoothCard({
    required this.booth,
    required this.isArabic,
    required this.onTap,
  });

  final BoothSummary booth;
  final bool isArabic;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final name = booth.localizedName(isArabic);
    final exhibitor = booth.localizedExhibitor(isArabic);
    final sector = booth.localizedSector(isArabic);
    final sub = <String>[
      if (exhibitor != null) exhibitor,
      if (sector != null) sector,
    ];

    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              _BoothTop(code: booth.code),
              const SizedBox(height: SimfTokens.space3),
              Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: <Widget>[
                  _LogoTile(initials: _initials(name)),
                  const SizedBox(width: SimfTokens.space3),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          name,
                          style: const TextStyle(
                            color: SimfTokens.surface,
                            fontWeight: FontWeight.w700,
                            fontSize: SimfTokens.textMd,
                            height: 1.3,
                          ),
                        ),
                        if (sub.isNotEmpty) ...<Widget>[
                          const SizedBox(height: SimfTokens.space1),
                          Text(
                            sub.join(' · '),
                            style: const TextStyle(
                              color: SimfTokens.txtSecondary,
                              fontWeight: FontWeight.w500,
                              fontSize: SimfTokens.textSm,
                              height: 1.4,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(width: SimfTokens.space2),
                  const Icon(
                    Icons.chevron_left,
                    color: SimfTokens.txtTertiary,
                    size: 18,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The card header (mockup `.booth .top`): the storefront mark sits opposite
/// the accent code pill, with a hairline rule underneath.
class _BoothTop extends StatelessWidget {
  const _BoothTop({required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            const Expanded(
              child: Icon(
                Icons.storefront_outlined,
                color: SimfTokens.accent,
                size: 18,
              ),
            ),
            if (code.isNotEmpty) _CodePill(code: code),
          ],
        ),
        const SizedBox(height: SimfTokens.space2),
        const Divider(height: 1),
      ],
    );
  }
}

/// The accent booth-number pill (mockup `.booth .num`): an accent-tinted fill
/// with an accent border, the code in accent, always LTR.
class _CodePill extends StatelessWidget {
  const _CodePill({required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: 0.10),
        border: Border.all(color: SimfTokens.accent),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        code,
        textDirection: TextDirection.ltr,
        style: const TextStyle(
          color: SimfTokens.accent,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textXs,
        ),
      ),
    );
  }
}

/// The square company-logo tile (mockup `.booth .co .lg`): a small white square
/// holding the booth initials in navy.
class _LogoTile extends StatelessWidget {
  const _LogoTile({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 34,
      height: 34,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.surface,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        initials,
        textDirection: TextDirection.ltr,
        style: const TextStyle(
          color: SimfTokens.navy,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textXs,
        ),
      ),
    );
  }
}

/// The first two letters of a booth name, upper-cased, for the logo tile.
String _initials(String name) {
  final trimmed = name.trim();
  if (trimmed.isEmpty) {
    return '';
  }
  return trimmed.substring(0, trimmed.length >= 2 ? 2 : 1).toUpperCase();
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
              color: SimfTokens.surface,
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
                style: const TextStyle(color: SimfTokens.txtSecondary),
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
                  style: const TextStyle(color: SimfTokens.txtTertiary),
                );
              }
              final description = snapshot.data?.localizedDescription(isArabic);
              if (description == null) {
                return const SizedBox.shrink();
              }
              return Text(
                description,
                style: const TextStyle(
                  color: SimfTokens.txtSecondary,
                  height: 1.5,
                ),
              );
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
          const Icon(
            Icons.storefront_outlined,
            size: 56,
            color: SimfTokens.txtTertiary,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.txtSecondary)),
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
