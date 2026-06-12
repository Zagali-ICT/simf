import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../venuemap/data/venue_map_models.dart';
import '../venuemap/data/venue_map_repository.dart';

/// LEGACY — the pre-redesign mockup Page 015 venue map, parked here when the
/// KSA Wave-2 map (frame 215:562 — venue plane, NOT the frame's Google map)
/// replaced it at `/map`. Never routed; kept compiling until the owner
/// approves deleting the legacy directory at programme close (§6 freeze
/// rules).
///
/// Page 015 — الخريطة · Venue map (2D map, #15, `/map`).
///
/// **Public** (Guest+). On open it loads the full node list (`/app/venue-map`)
/// and the booth summaries (`/app/booths`) in parallel (Page_015 L-1), then
/// renders every node at its `(x, y)` (normalised to the canvas — L-4) on a
/// pan/zoom plane. Tapping a Booth node opens a bottom-sheet popup composed from
/// the cached summary plus a lazy detail call for the description (L-5). The
/// canvas geometry is **not** mirrored in RTL — only the chrome/labels are (L-3).
/// UI is interim; final visuals come from SIMF-VID-001.
class VenueMapScreen extends ConsumerStatefulWidget {
  const VenueMapScreen({super.key});

  @override
  ConsumerState<VenueMapScreen> createState() => _VenueMapScreenState();
}

class _VenueMapScreenState extends ConsumerState<VenueMapScreen> {
  // The design-space canvas the normalised node coordinates map onto.
  static const double _canvas = 1000;
  static const double _pad = 80;

  bool _loading = true;
  bool _error = false;
  List<VenueMapNode> _nodes = const <VenueMapNode>[];
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
      final repo = ref.read(venueMapRepositoryProvider);
      // Both reads in flight together (L-1); the screen is ready when both land.
      final results = await Future.wait(<Future<Object>>[
        repo.getNodes(),
        repo.getBooths(),
      ]);
      if (!mounted) {
        return;
      }
      setState(() {
        _nodes = results[0] as List<VenueMapNode>;
        _booths = results[1] as List<BoothSummary>;
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

  BoothSummary? _boothFor(VenueMapNode node) {
    if (node.boothId == null) {
      return null;
    }
    for (final booth in _booths) {
      if (booth.id == node.boothId) {
        return booth;
      }
    }
    return null;
  }

  Future<BoothDetail?> _safeDetail(String? boothId) async {
    if (boothId == null) {
      return null;
    }
    try {
      return await ref.read(venueMapRepositoryProvider).getBoothDetail(boothId);
    } on ApiFailure {
      // 404 / transport: keep the summary, drop the description (L-8).
      return null;
    }
  }

  void _openBooth(VenueMapNode node) {
    final l10n = AppL10n.of(context);
    final summary = _boothFor(node);
    final detail = _safeDetail(summary?.id);
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (_) => _BoothSheet(
        l10n: l10n,
        node: node,
        summary: summary,
        detail: detail,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.venueMapTitle)),
      bottomNavigationBar: const SimfBottomNav(current: SimfTab.map),
      body: SafeArea(top: false, child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _buildError(l10n);
    }
    if (_nodes.isEmpty) {
      return _buildEmpty(l10n);
    }
    return _buildMap(l10n);
  }

  Widget _buildError(AppL10n l10n) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(l10n.venueMapError, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(
              onPressed: () => unawaited(_load()),
              child: Text(l10n.retryLabel),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildEmpty(AppL10n l10n) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(Icons.map_outlined, size: 56, color: SimfTokens.inkMuted),
          const SizedBox(height: SimfTokens.space3),
          Text(
            l10n.venueMapEmpty,
            style: const TextStyle(color: SimfTokens.inkMuted),
          ),
        ],
      ),
    );
  }

  Widget _buildMap(AppL10n l10n) {
    final isArabic = l10n.isArabic;
    final bounds = _Bounds.of(_nodes);

    Offset toCanvas(VenueMapNode node) => Offset(
          _pad + bounds.normX(node.x) * (_canvas - 2 * _pad),
          _pad + bounds.normY(node.y) * (_canvas - 2 * _pad),
        );

    return Stack(
      children: <Widget>[
        // The canvas geometry must NOT mirror in RTL (node positions are
        // physical venue coordinates, L-3) — force LTR for the map plane.
        Directionality(
          textDirection: TextDirection.ltr,
          child: InteractiveViewer(
            constrained: false,
            minScale: 0.3,
            maxScale: 4,
            boundaryMargin: const EdgeInsets.all(200),
            child: SizedBox(
              width: _canvas,
              height: _canvas,
              child: Stack(
                children: <Widget>[
                  for (final node in _nodes)
                    Positioned(
                      left: toCanvas(node).dx - 40,
                      top: toCanvas(node).dy - 40,
                      width: 80,
                      child: _NodeMarker(
                        node: node,
                        isArabic: isArabic,
                        onTap: node.isBooth ? () => _openBooth(node) : null,
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
        Positioned(
          left: SimfTokens.space3,
          right: SimfTokens.space3,
          bottom: SimfTokens.space3,
          child: _Legend(l10n: l10n),
        ),
      ],
    );
  }
}

/// The min/max extent of the loaded nodes, used to normalise `(x, y)` into
/// `[0, 1]` before mapping onto the canvas (Page_015 L-4).
class _Bounds {
  const _Bounds(this.minX, this.maxX, this.minY, this.maxY);

  factory _Bounds.of(List<VenueMapNode> nodes) {
    var minX = nodes.first.x;
    var maxX = nodes.first.x;
    var minY = nodes.first.y;
    var maxY = nodes.first.y;
    for (final node in nodes) {
      minX = node.x < minX ? node.x : minX;
      maxX = node.x > maxX ? node.x : maxX;
      minY = node.y < minY ? node.y : minY;
      maxY = node.y > maxY ? node.y : maxY;
    }
    return _Bounds(minX, maxX, minY, maxY);
  }

  final double minX;
  final double maxX;
  final double minY;
  final double maxY;

  double normX(double x) => (maxX - minX) <= 0 ? 0.5 : (x - minX) / (maxX - minX);
  double normY(double y) => (maxY - minY) <= 0 ? 0.5 : (y - minY) / (maxY - minY);
}

/// One node marker, styled by [VenueMapNode.kind]. Booth markers are tappable.
class _NodeMarker extends StatelessWidget {
  const _NodeMarker({required this.node, required this.isArabic, this.onTap});

  final VenueMapNode node;
  final bool isArabic;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final style = _markerStyle(node.kind);
    final marker = Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 34,
          height: 34,
          decoration: BoxDecoration(
            color: style.fill,
            shape: style.shape,
            borderRadius:
                style.shape == BoxShape.rectangle ? BorderRadius.circular(SimfTokens.radiusSmall) : null,
            border: Border.all(color: style.border, width: 1.5),
          ),
          child: Icon(style.icon, size: 18, color: style.foreground),
        ),
        const SizedBox(height: 2),
        Text(
          node.localizedLabel(isArabic),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 9, fontWeight: FontWeight.w600),
        ),
      ],
    );

    if (onTap == null) {
      return marker;
    }
    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: Semantics(
        button: true,
        label: node.localizedLabel(isArabic),
        child: marker,
      ),
    );
  }

  _MarkerStyle _markerStyle(VenueMapNodeKind kind) {
    switch (kind) {
      case VenueMapNodeKind.hall:
        return const _MarkerStyle(
          icon: Icons.meeting_room_outlined,
          fill: SimfTokens.navy,
          foreground: SimfTokens.surface,
          border: SimfTokens.navy,
          shape: BoxShape.rectangle,
        );
      case VenueMapNodeKind.zone:
        return const _MarkerStyle(
          icon: Icons.crop_din,
          fill: SimfTokens.surface,
          foreground: SimfTokens.inkMuted,
          border: SimfTokens.inkMuted,
          shape: BoxShape.rectangle,
        );
      case VenueMapNodeKind.booth:
        return const _MarkerStyle(
          icon: Icons.storefront,
          fill: SimfTokens.accent,
          foreground: SimfTokens.navy,
          border: SimfTokens.accent,
          shape: BoxShape.circle,
        );
      case VenueMapNodeKind.pointOfInterest:
        return const _MarkerStyle(
          icon: Icons.place,
          fill: SimfTokens.surface,
          foreground: SimfTokens.danger,
          border: SimfTokens.danger,
          shape: BoxShape.circle,
        );
    }
  }
}

class _MarkerStyle {
  const _MarkerStyle({
    required this.icon,
    required this.fill,
    required this.foreground,
    required this.border,
    required this.shape,
  });

  final IconData icon;
  final Color fill;
  final Color foreground;
  final Color border;
  final BoxShape shape;
}

/// A compact legend for the four node kinds.
class _Legend extends StatelessWidget {
  const _Legend({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space2,
        ),
        child: Wrap(
          alignment: WrapAlignment.center,
          spacing: SimfTokens.space3,
          runSpacing: SimfTokens.space1,
          children: <Widget>[
            _LegendItem(Icons.meeting_room_outlined, SimfTokens.navy, l10n.legendHall),
            _LegendItem(Icons.crop_din, SimfTokens.inkMuted, l10n.legendZone),
            _LegendItem(Icons.storefront, SimfTokens.accent, l10n.legendBooth),
            _LegendItem(Icons.place, SimfTokens.danger, l10n.legendPoi),
          ],
        ),
      ),
    );
  }
}

class _LegendItem extends StatelessWidget {
  const _LegendItem(this.icon, this.color, this.label);

  final IconData icon;
  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: 14, color: color),
        const SizedBox(width: SimfTokens.space1),
        Text(label, style: const TextStyle(fontSize: SimfTokens.textXs)),
      ],
    );
  }
}

/// The booth popup (bottom sheet). Shows the cached summary immediately; the
/// description streams in from the lazy detail call ([detail]) — a null result
/// (404 / transport) simply omits the description (Page_015 L-5/L-8). When no
/// summary is loaded for the node, only the node label shows (L-8).
class _BoothSheet extends StatelessWidget {
  const _BoothSheet({
    required this.l10n,
    required this.node,
    required this.summary,
    required this.detail,
  });

  final AppL10n l10n;
  final VenueMapNode node;
  final BoothSummary? summary;
  final Future<BoothDetail?> detail;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final booth = summary;
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
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  booth?.localizedName(isArabic) ?? node.localizedLabel(isArabic),
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textLg,
                  ),
                ),
              ),
              if (booth != null)
                Container(
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
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ),
            ],
          ),
          if (booth != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            _SubLine(
              booth.localizedExhibitor(isArabic),
              booth.localizedSector(isArabic),
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
                final description =
                    snapshot.data?.localizedDescription(isArabic);
                if (description == null) {
                  return const SizedBox.shrink();
                }
                return Text(description);
              },
            ),
          ],
        ],
      ),
    );
  }
}

/// The "Exhibitor · Sector" sub-line; renders only the parts that are present.
class _SubLine extends StatelessWidget {
  const _SubLine(this.exhibitor, this.sector);

  final String? exhibitor;
  final String? sector;

  @override
  Widget build(BuildContext context) {
    final parts = <String>[
      if (exhibitor != null) exhibitor!,
      if (sector != null) sector!,
    ];
    if (parts.isEmpty) {
      return const SizedBox.shrink();
    }
    return Text(
      parts.join(' · '),
      style: const TextStyle(color: SimfTokens.inkMuted),
    );
  }
}
