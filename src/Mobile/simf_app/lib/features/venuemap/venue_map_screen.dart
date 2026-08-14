import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';
import 'package:simf_app/features/venuemap/widgets/venue_map_booth_sheet.dart';
import 'package:simf_app/features/venuemap/widgets/venue_map_controls.dart';
import 'package:simf_app/features/venuemap/widgets/venue_map_geometry.dart';
import 'package:simf_app/features/venuemap/widgets/venue_map_info_card.dart';
import 'package:simf_app/features/venuemap/widgets/venue_map_marker.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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
///
/// Route: `RouteNames.venueMap`.
/// Data: [simfDataConfigProvider], [venueMapRepositoryProvider].
/// Perf: no list — a single-screen layout.
// The design-space canvas the normalised node coordinates map onto. Moved out
// of the State so the provider can derive positions without one.
const double _canvas = 1000;
const double _pad = 80;
const double _zoomStep = 1.3;

/// Normalises every node's `(x, y)` onto the canvas once (Page_015 L-4).
Map<String, Offset> _canvasPositions(List<VenueMapNode> nodes) {
  if (nodes.isEmpty) {
    return const <String, Offset>{};
  }
  final bounds = VenueMapBounds.of(nodes);
  return <String, Offset>{
    for (final node in nodes)
      node.id: Offset(
        _pad + bounds.normX(node.x) * (_canvas - 2 * _pad),
        _pad + bounds.normY(node.y) * (_canvas - 2 * _pad),
      ),
  };
}

/// The map's nodes, plus the two lookups derived from them ONCE.
///
/// The derivation used to happen in `setState` after the load, which is what
/// made those two maps state. Computing them here keeps them beside the data
/// they come from, and there is exactly one place that can get them out of step
/// with it: none.
@immutable
class VenueMapData {
  const VenueMapData({
    required this.nodes,
    required this.positions,
    required this.boothById,
  });

  final List<VenueMapNode> nodes;
  final Map<String, Offset> positions;
  final Map<String, BoothSummary> boothById;

  BoothSummary? boothFor(VenueMapNode node) =>
      node.boothId == null ? null : boothById[node.boothId];
}

/// The venue map (`GET /app/venue-map/nodes` + `GET /app/booths`).
final venueMapDataProvider = FutureProvider.autoDispose<VenueMapData>(
  (ref) async {
    final repo = ref.watch(venueMapRepositoryProvider);
    // Both reads in flight together (L-1); the screen is ready when both land.
    final results = await Future.wait(<Future<Object>>[
      repo.getNodes(),
      repo.getBooths(),
    ]);
    final nodes = results[0] as List<VenueMapNode>;
    final booths = results[1] as List<BoothSummary>;
    return VenueMapData(
      nodes: nodes,
      positions: _canvasPositions(nodes),
      boothById: <String, BoothSummary>{
        for (final booth in booths) booth.id: booth,
      },
    );
  },
);

class VenueMapScreen extends ConsumerStatefulWidget {
  const VenueMapScreen({this.targetBoothId, super.key});

  /// #9 — when set (the booth card's "أرشدني" CTA pushes the map with a booth
  /// id), the map selects + centres the node for that booth once loaded.
  final String? targetBoothId;

  @override
  ConsumerState<VenueMapScreen> createState() => _VenueMapScreenState();
}

class _VenueMapScreenState extends ConsumerState<VenueMapScreen> {
  final TransformationController _transform = TransformationController();

  VenueMapNode? _selected;

  @override
  void initState() {
    super.initState();
    unawaited(_focusTargetBooth());
  }

  @override
  void dispose() {
    _transform.dispose();
    super.dispose();
  }

  Future<void> _refresh() => refreshAsync(ref, venueMapDataProvider.future);

  /// The map viewport — the page body's render size at action time.
  Size get _viewport => context.size ?? Size.zero;

  void _zoomBy(double factor) {
    final scale =
        (_transform.value.getMaxScaleOnAxis() * factor).clamp(0.3, 4.0);
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
  void _centreOn(VenueMapNode node, Map<String, Offset> positions) {
    const scale = 1.5;
    final position = positions[node.id] ?? Offset.zero;
    _transform.value = Matrix4.identity()
      ..translateByDouble(
        _viewport.width / 2 - position.dx * scale,
        _viewport.height / 2 - position.dy * scale,
        0,
        1,
      )
      ..scaleByDouble(scale, scale, 1, 1);
  }

  /// #9 — select + centre the map on the target booth's node once the nodes
  /// have loaded and (via the post-frame callback) the plane has laid out so
  /// the viewport size is available. A no-op when no target / no matching node.
  /// Centres the map on the booth this screen was pushed for (#9), once.
  ///
  /// Awaits the provider's FIRST future rather than listening for data, so a
  /// later pull-to-refresh does not yank the camera back to the target after
  /// the user has panned away.
  Future<void> _focusTargetBooth() async {
    final target = widget.targetBoothId?.trim();
    if (target == null || target.isEmpty) {
      return;
    }
    final VenueMapData data;
    try {
      data = await ref.read(venueMapDataProvider.future);
    } on Object {
      return; // The error branch renders; there is nothing to centre on.
    }
    if (!mounted) {
      return;
    }
    VenueMapNode? match;
    for (final node in data.nodes) {
      if (node.boothId == target) {
        match = node;
        break;
      }
    }
    if (match == null) {
      return;
    }
    final node = match;
    setState(() => _selected = node);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        _centreOn(node, data.positions);
      }
    });
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
    unawaited(showModalBottomSheet<void>(
        context: context,
        showDragHandle: true,
        builder: (_) => VenueMapBoothSheet(
          l10n: l10n,
          node: node,
          summary: summary,
          detail: detail,
        ),
      ),);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // Full-bleed page: no title/back → the SimfPageShell header collapses.
    return SimfPageShell(
      tab: SimfTab.map,
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    return ref.watch(venueMapDataProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _buildError(l10n),
          data: (data) =>
              data.nodes.isEmpty ? _buildEmpty(l10n) : _buildMap(l10n, data),
        );
  }

  /// Only the error and empty branches are pull-to-refreshable. The map itself
  /// is pan/zoom, and a RefreshIndicator over it would swallow every downward
  /// drag — the gesture belongs where the user is otherwise stuck.
  Widget _buildError(AppL10n l10n) {
    return SimfRefreshableMessage(
      onRefresh: _refresh,
      child: SimfErrorState(
        message: l10n.venueMapError,
        retryLabel: l10n.retryLabel,
        onRetry: () => ref.invalidate(venueMapDataProvider),
      ),
    );
  }

  Widget _buildEmpty(AppL10n l10n) {
    return SimfRefreshableMessage(
      onRefresh: _refresh,
      child: SimfEmptyState(
        icon: Icons.map_outlined,
        message: l10n.venueMapEmpty,
      ),
    );
  }

  Widget _buildMap(AppL10n l10n, VenueMapData data) {
    final isArabic = l10n.isArabic;
    final selected = _selected;
    final selectedBooth = selected == null ? null : data.boothFor(selected);

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
            boundaryMargin: const EdgeInsets.all(SimfTokens.venueMapPanMargin),
            child: SizedBox(
              width: _canvas,
              height: _canvas,
              child: Stack(
                children: <Widget>[
                  for (final node in data.nodes)
                    Positioned(
                      left: (data.positions[node.id] ?? Offset.zero).dx - 40,
                      top: (data.positions[node.id] ?? Offset.zero).dy - 40,
                      width: SimfTokens.venueMapScreenWidth,
                      child: VenueMapMarker(
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
              VenueMapControl(
                icon: Icons.my_location,
                label: l10n.venueMapResetView,
                onTap: _resetView,
              ),
              const SizedBox(height: SimfTokens.space2),
              VenueMapControl(
                icon: Icons.add,
                label: l10n.venueMapZoomIn,
                onTap: () => _zoomBy(_zoomStep),
              ),
              const SizedBox(height: SimfTokens.space2),
              VenueMapControl(
                icon: Icons.remove,
                label: l10n.venueMapZoomOut,
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
            child: VenueMapInfoCard(
              l10n: l10n,
              node: selected,
              booth: selectedBooth,
              // FR-LGO-005 — the badge builds
              // `{base}/app/assets/BoothLogo/{id}/image` (the D-357 anonymous
              // asset route), exactly like the booths list.
              baseUrl: ref.watch(simfDataConfigProvider).baseUrl,
              onDirect: () => _centreOn(selected, data.positions),
              onDetails: selected.isBooth
                  ? () => _openDetails(selected, selectedBooth)
                  : null,
              onClose: () => setState(() => _selected = null),
            ),
          ),
      ],
    );
  }
}
