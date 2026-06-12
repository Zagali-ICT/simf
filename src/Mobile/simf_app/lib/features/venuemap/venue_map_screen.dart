import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import 'data/venue_map_models.dart';
import 'data/venue_map_repository.dart';

/// Page 015 — الخريطة · Venue map (2D map, #15, `/map`), rebuilt to the KSA
/// Wave-2 frame **215:562 "Location"** — with the frame's Google geographic
/// map **replaced by the venue 2D node plane** (owner directive; the
/// `VenueMapNodes` data is the map).
///
/// **Public** (Guest+). Data contract unchanged: the node list
/// (`/app/venue-map`) and booth summaries (`/app/booths`) load in parallel
/// (Page_015 L-1); booth descriptions stay a lazy detail call (L-5). Frame
/// mapping: a full-bleed pan/zoom plane, the floating **gold zoom-in /
/// zoom-out / recentre controls**, and — on node tap — the **bottom white
/// info card** (gold code box, name, exhibitor · sector line, code chip,
/// gold **أرشدني** centring the map on the node + bordered **عرض التفاصيل**
/// opening the description sheet) instead of the old direct bottom sheet.
/// The canvas geometry is **not** mirrored in RTL (physical venue
/// coordinates, L-3). The old legend strip gave way to the frame's info
/// card (the card names the selection).
class VenueMapScreen extends ConsumerStatefulWidget {
  const VenueMapScreen({super.key});

  @override
  ConsumerState<VenueMapScreen> createState() => _VenueMapScreenState();
}

class _VenueMapScreenState extends ConsumerState<VenueMapScreen> {
  // The design-space canvas the normalised node coordinates map onto.
  static const double _canvas = 1000;
  static const double _pad = 80;
  static const double _zoomStep = 1.3;

  final TransformationController _transform = TransformationController();

  bool _loading = true;
  bool _error = false;
  List<VenueMapNode> _nodes = const <VenueMapNode>[];
  List<BoothSummary> _booths = const <BoothSummary>[];
  VenueMapNode? _selected;

  // The map viewport size from the last layout pass — drives "centre on node".
  Size _viewport = Size.zero;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  @override
  void dispose() {
    _transform.dispose();
    super.dispose();
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

  Offset _toCanvas(VenueMapNode node, _Bounds bounds) => Offset(
        _pad + bounds.normX(node.x) * (_canvas - 2 * _pad),
        _pad + bounds.normY(node.y) * (_canvas - 2 * _pad),
      );

  void _zoomBy(double factor) {
    final scale = (_transform.value.getMaxScaleOnAxis() * factor)
        .clamp(0.3, 4.0)
        .toDouble();
    final centre = _viewport.center(Offset.zero);
    // Re-anchor the zoom on the viewport centre.
    final scenePoint = _transform.toScene(centre);
    _transform.value = Matrix4.identity()
      ..translateByDouble(
        centre.dx - scenePoint.dx * scale,
        centre.dy - scenePoint.dy * scale,
        0,
        1,
      )
      ..scaleByDouble(scale, scale, 1, 1);
  }

  /// Centres the plane on [node] at a readable scale (the أرشدني action).
  void _centreOn(VenueMapNode node) {
    const scale = 1.5;
    final position = _toCanvas(node, _Bounds.of(_nodes));
    _transform.value = Matrix4.identity()
      ..translateByDouble(
        _viewport.width / 2 - position.dx * scale,
        _viewport.height / 2 - position.dy * scale,
        0,
        1,
      )
      ..scaleByDouble(scale, scale, 1, 1);
  }

  void _resetView() {
    _transform.value = Matrix4.identity();
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

  /// The عرض التفاصيل action — the lazy-description sheet (L-5).
  void _openDetails(VenueMapNode node, BoothSummary? summary) {
    final l10n = AppL10n.of(context);
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
      backgroundColor: SimfTokens.navySurface,
      bottomNavigationBar: const SimfBottomNav(current: SimfTab.map),
      body: SafeArea(child: _buildBody(l10n)),
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
            Text(
              l10n.venueMapError,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
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
          const Icon(
            Icons.map_outlined,
            size: 56,
            color: SimfTokens.beigeBorder,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(
            l10n.venueMapEmpty,
            style: const TextStyle(color: SimfTokens.beigeBorder),
          ),
        ],
      ),
    );
  }

  Widget _buildMap(AppL10n l10n) {
    final isArabic = l10n.isArabic;
    final bounds = _Bounds.of(_nodes);
    final selected = _selected;

    return LayoutBuilder(
      builder: (context, constraints) {
        _viewport = constraints.biggest;
        return Stack(
          children: <Widget>[
            // The canvas geometry must NOT mirror in RTL (node positions are
            // physical venue coordinates, L-3) — force LTR for the map plane.
            Directionality(
              textDirection: TextDirection.ltr,
              child: InteractiveViewer(
                constrained: false,
                transformationController: _transform,
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
                          left: _toCanvas(node, bounds).dx - 40,
                          top: _toCanvas(node, bounds).dy - 40,
                          width: 80,
                          child: _NodeMarker(
                            node: node,
                            isArabic: isArabic,
                            selected: node.id == selected?.id,
                            onTap: () => setState(() => _selected = node),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ),
            // Floating gold map controls (frame right-edge stack).
            PositionedDirectional(
              end: SimfTokens.space4,
              top: SimfTokens.space4,
              child: Column(
                children: <Widget>[
                  _MapControl(
                    icon: Icons.my_location,
                    tooltip: l10n.retryLabel,
                    onTap: _resetView,
                  ),
                  const SizedBox(height: SimfTokens.space2),
                  _MapControl(icon: Icons.add, onTap: () => _zoomBy(_zoomStep)),
                  const SizedBox(height: SimfTokens.space2),
                  _MapControl(
                    icon: Icons.remove,
                    onTap: () => _zoomBy(1 / _zoomStep),
                  ),
                ],
              ),
            ),
            if (selected != null)
              Positioned(
                left: SimfTokens.space4,
                right: SimfTokens.space4,
                bottom: SimfTokens.space4,
                child: _NodeInfoCard(
                  l10n: l10n,
                  node: selected,
                  booth: _boothFor(selected),
                  onDirect: () => _centreOn(selected),
                  onDetails: selected.isBooth
                      ? () => _openDetails(selected, _boothFor(selected))
                      : null,
                  onClose: () => setState(() => _selected = null),
                ),
              ),
          ],
        );
      },
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

/// One floating gold map control (frame nodes: locate / + / −).
class _MapControl extends StatelessWidget {
  const _MapControl({required this.icon, required this.onTap, this.tooltip});

  final IconData icon;
  final VoidCallback onTap;
  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.accent,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: SizedBox(
          width: 40,
          height: 40,
          child: Icon(icon, size: 22, color: SimfTokens.navy),
        ),
      ),
    );
  }
}

/// One node marker, styled by [VenueMapNode.kind]; the selected node carries
/// a gold ring. All markers are tappable (selection drives the info card).
class _NodeMarker extends StatelessWidget {
  const _NodeMarker({
    required this.node,
    required this.isArabic,
    required this.selected,
    required this.onTap,
  });

  final VenueMapNode node;
  final bool isArabic;
  final bool selected;
  final VoidCallback onTap;

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
            borderRadius: style.shape == BoxShape.rectangle
                ? BorderRadius.circular(SimfTokens.radiusSmall)
                : null,
            border: Border.all(
              color: selected ? SimfTokens.accent : style.border,
              width: selected ? 3 : 1.5,
            ),
          ),
          child: Icon(style.icon, size: 18, color: style.foreground),
        ),
        const SizedBox(height: 2),
        Text(
          node.localizedLabel(isArabic),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontSize: 9,
            fontWeight: FontWeight.w600,
            color: Colors.white,
          ),
        ),
      ],
    );

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
          foreground: Colors.white,
          border: SimfTokens.beigeBorder,
          shape: BoxShape.rectangle,
        );
      case VenueMapNodeKind.zone:
        return const _MarkerStyle(
          icon: Icons.crop_din,
          fill: SimfTokens.navyDeep,
          foreground: SimfTokens.beigeBorder,
          border: SimfTokens.beigeBorder,
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
          fill: Colors.white,
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

/// The bottom white info card for the selected node (frame node 215:562's
/// SAMI card): gold code box · name + exhibitor/sector line · code chip,
/// then the gold أرشدني + bordered عرض التفاصيل actions.
class _NodeInfoCard extends StatelessWidget {
  const _NodeInfoCard({
    required this.l10n,
    required this.node,
    required this.booth,
    required this.onDirect,
    required this.onClose,
    this.onDetails,
  });

  final AppL10n l10n;
  final VenueMapNode node;
  final BoothSummary? booth;
  final VoidCallback onDirect;
  final VoidCallback onClose;
  final VoidCallback? onDetails;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final title = booth?.localizedName(isArabic) ?? node.localizedLabel(isArabic);
    final subtitleParts = <String>[
      if (booth?.localizedExhibitor(isArabic) != null)
        booth!.localizedExhibitor(isArabic)!,
      if (booth?.localizedSector(isArabic) != null)
        booth!.localizedSector(isArabic)!,
    ];
    final code = booth?.code;

    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(SimfTokens.radiusXl - 4),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Row(
            children: <Widget>[
              if (code != null)
                Container(
                  width: 64,
                  height: 56,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: SimfTokens.accent,
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(SimfTokens.space1),
                    child: FittedBox(
                      child: Text(
                        title,
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w700,
                          fontSize: SimfTokens.textMd,
                        ),
                      ),
                    ),
                  ),
                ),
              if (code != null) const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: SimfTokens.headlineInk,
                        fontWeight: FontWeight.w700,
                        fontSize: SimfTokens.textLg,
                      ),
                    ),
                    if (subtitleParts.isNotEmpty) ...<Widget>[
                      const SizedBox(height: 2),
                      Text(
                        subtitleParts.join(' · '),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: SimfTokens.greyText,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              if (code != null)
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: SimfTokens.space3,
                    vertical: SimfTokens.space2,
                  ),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                    border: Border.all(color: SimfTokens.accent),
                  ),
                  child: Text(
                    code,
                    textDirection: TextDirection.ltr,
                    style: const TextStyle(
                      color: SimfTokens.accent,
                      fontWeight: FontWeight.w700,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                )
              else
                IconButton(
                  onPressed: onClose,
                  icon: const Icon(
                    Icons.close,
                    size: 20,
                    color: SimfTokens.greyText,
                  ),
                ),
            ],
          ),
          const SizedBox(height: SimfTokens.space3),
          Row(
            children: <Widget>[
              Expanded(
                child: FilledButton.icon(
                  onPressed: onDirect,
                  style: FilledButton.styleFrom(
                    minimumSize: const Size.fromHeight(44),
                  ),
                  icon: const Icon(Icons.navigation_outlined, size: 18),
                  label: Text(l10n.venueMapDirectMe),
                ),
              ),
              if (onDetails != null) ...<Widget>[
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: OutlinedButton(
                    onPressed: onDetails,
                    style: OutlinedButton.styleFrom(
                      minimumSize: const Size.fromHeight(44),
                      side: const BorderSide(color: SimfTokens.lineLight),
                      foregroundColor: SimfTokens.accent,
                    ),
                    child: Text(l10n.venueMapViewDetails),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

/// The booth detail sheet (the عرض التفاصيل action). Shows the cached summary
/// immediately; the description streams in from the lazy detail call — a null
/// result (404 / transport) simply omits the description (Page_015 L-5/L-8).
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
