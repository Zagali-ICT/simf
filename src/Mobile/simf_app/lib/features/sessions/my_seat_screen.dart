import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/app_assets.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_svg_icon.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';
import 'widgets/hall_seat_map.dart';
import 'widgets/seat_map_async_view.dart';

/// Page 018 — مقعدي · My Seat map (#18,
/// `/sessions/:sessionId/my-seat`, **auth-gated**, approved Visitor only),
/// rebuilt to the KSA-Project frame **898:2873 "Your seat"** on the shared
/// navy shell.
///
/// Behaviour is unchanged from the previous build: one read
/// (`GET /app/sessions/{id}/seats`) draws the whole hall grid, every seat
/// coloured by **derived** status — mine / reserved / available (Page_018
/// L-2) — with the caller's own seat highlighted gold. Read-only as drawn.
/// Frame mapping: the circled-back shell header, a navy "الجلسة" card holding
/// the seat (مقعد) + row (الصف) chips, the navy hall card with the gold stage
/// band, the A–H seat grid and the محجوز/متاح/مقعدك legend, then the two gold
/// action buttons (share location / guide me to my seat). Navigate opens the
/// venue map (15); share opens the native sheet (E3).
class MySeatScreen extends ConsumerWidget {
  const MySeatScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final value = ref.watch(seatMapProvider(sessionId));
    return SimfPageShell(
      title: l10n.mySeatTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: SeatMapAsyncView(
        value: value,
        onRetry: () => ref.invalidate(seatMapProvider(sessionId)),
        builder: (map) => _SeatMapView(
          map: map,
          l10n: l10n,
          onNavigate: () => context.pushNamed(RouteNames.venueMap),
          onShare: map.myCell == null
              ? null
              : () => unawaited(
                    ref.read(seatShareProvider).shareText(
                          l10n.seatShareText(
                            map.myCell!.rowLabel,
                            map.myCell!.seatNumber,
                          ),
                        ),
                  ),
        ),
      ),
    );
  }
}

class _SeatMapView extends StatelessWidget {
  const _SeatMapView({
    required this.map,
    required this.l10n,
    required this.onNavigate,
    this.onShare,
  });

  final SessionSeatMap map;
  final AppL10n l10n;
  final VoidCallback onNavigate;
  final VoidCallback? onShare;

  @override
  Widget build(BuildContext context) {
    // Arabic is primary; the whole page reads right-to-left (frame 898:2873).
    return Directionality(
      textDirection: TextDirection.rtl,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space2,
          SimfTokens.space4,
          SimfTokens.space5,
        ),
        children: <Widget>[
          _SessionCard(map: map, l10n: l10n),
          // Frame gaps: session card (ends y265) → hall card (y289) = 24; hall
          // card (ends y699) → actions (y739) = 40 (no 40 in the spacing scale).
          const SizedBox(height: SimfTokens.space6),
          // Read-only defaults = this frame (898:2873): beige available
          // border, 20px seat cap, 14px reserved/mine swatches.
          HallSeatMapCard(map: map, l10n: l10n),
          const SizedBox(height: SimfTokens.space10),
          _Actions(l10n: l10n, onNavigate: onNavigate, onShare: onShare),
        ],
      ),
    );
  }
}

/// The "الجلسة" card (frame 905:1556): the session label, its title, then the
/// seat (مقعد) + row (الصف) chips — right-aligned on the navy `navyDeep` fill.
class _SessionCard extends StatelessWidget {
  const _SessionCard({required this.map, required this.l10n});

  final SessionSeatMap map;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final cell = map.myCell;
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.sessionLabel,
            textAlign: TextAlign.start,
            style: SimfTokens.labelBeigeSemiboldLg,
          ),
          const SizedBox(height: SimfTokens.space4),
          Text(
            // D-432 — the real session title now ships on the seat map; the
            // seat row/number are shown by the chips below. Fall back to the
            // seat location (or the no-seat hint) when the title is absent.
            map.localizedSessionTitle(l10n.isArabic) ??
                (cell != null
                    ? l10n.seatLocation(cell.rowLabel, cell.seatNumber)
                    : l10n.noSeatYet),
            textAlign: TextAlign.center,
            style: SimfTokens.labelWhiteSemiboldTitle,
          ),
          const SizedBox(height: SimfTokens.space4),
          Row(
            children: <Widget>[
              // RTL: the row (الصف) chip sits at the inline-start (right), the
              // seat (مقعد) chip at the inline-end (left) — frame 905:1576.
              Expanded(
                child: _SeatChip(
                  goldLabel: l10n.rowChipLabel,
                  value: cell != null ? cell.rowLabel : '—',
                  borderColor: SimfTokens.accent,
                  borderWidth: SimfTokens.hairlineBold,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: _SeatChip(
                  goldLabel: l10n.seatChipLabel,
                  value: cell != null ? '${cell.seatNumber}' : '—',
                  borderColor: SimfTokens.beigeBorder,
                  borderWidth: SimfTokens.hairline,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// One bordered seat/row chip (frame 905:1577 / 905:1579): a gold label word
/// next to its value, centred on a navyDeep fill with a thin gold/beige border.
class _SeatChip extends StatelessWidget {
  const _SeatChip({
    required this.goldLabel,
    required this.value,
    required this.borderColor,
    required this.borderWidth,
  });

  final String goldLabel;
  final String value;
  final Color borderColor;
  final double borderWidth;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.actionChipHeight,
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(color: borderColor, width: borderWidth),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text.rich(
        TextSpan(
          children: <InlineSpan>[
            TextSpan(
              text: goldLabel,
              style: SimfTokens.labelGoldSemiboldSm,
            ),
            const TextSpan(text: ' '),
            TextSpan(
              text: value,
              // Frame 905:1577/1579 — the value (12 / B) is white; only the
              // leading label word (مقعد / الصف) is gold.
              style: SimfTokens.labelWhiteSemibold,
            ),
          ],
        ),
        textAlign: TextAlign.center,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}

/// The action row (frame 908:1733 / 908:1737): a gold-outlined "share location"
/// next to a gold-filled "guide me to my seat".
class _Actions extends StatelessWidget {
  const _Actions({required this.l10n, required this.onNavigate, this.onShare});

  final AppL10n l10n;
  final VoidCallback onNavigate;
  final VoidCallback? onShare;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        // RTL: the gold-filled "guide me" CTA sits at the inline-start (right),
        // the outlined "share location" at the inline-end (left) — frame
        // 908:1733 / 908:1737.
        Expanded(
          child: FilledButton.icon(
            onPressed: onNavigate,
            style: FilledButton.styleFrom(
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space4,
                vertical: SimfTokens.space3,
              ),
            ),
            icon: const SimfSvgIcon(
              AppAssets.icLocation,
              size: 18,
              color: SimfTokens.surface,
            ),
            label: Text(
              l10n.navigateToSeat,
              style: SimfTokens.labelSemiboldSm,
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: OutlinedButton.icon(
            onPressed: onShare,
            style: OutlinedButton.styleFrom(
              foregroundColor: SimfTokens.surface,
              side: const BorderSide(color: SimfTokens.accent),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space4,
                vertical: SimfTokens.space3,
              ),
            ),
            icon: const Icon(Icons.share_outlined, size: 18),
            label: Text(
              l10n.shareLocation,
              style: SimfTokens.labelSemiboldSm,
            ),
          ),
        ),
      ],
    );
  }
}
