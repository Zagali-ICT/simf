import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/widgets/request_status_style.dart';

/// The horizontally-scrolling status filter chips — each populated status with
/// its count (no "All" chip; السجل in the top row serves "all"). Figma chip row.
class RequestStatusChips extends StatelessWidget {
  const RequestStatusChips({
    required this.items,
    required this.selected,
    required this.l10n,
    required this.onSelect,
    super.key,
  });

  final List<AppRequestItem> items;
  final AppRequestStatus? selected;
  final AppL10n l10n;
  final ValueChanged<AppRequestStatus?> onSelect;

  @override
  Widget build(BuildContext context) {
    final chips = <Widget>[];
    for (final status in kRequestChipOrder) {
      final count = items.where((i) => i.status == status).length;
      if (count == 0) {
        continue;
      }
      if (chips.isNotEmpty) {
        chips.add(const SizedBox(width: SimfTokens.space4));
      }
      chips.add(
        _chip(
          label: requestStatusLabel(l10n, status),
          count: count,
          status: status,
        ),
      );
    }
    // The Figma lays the chips left→right (ملغى … مقبول) — match that order.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(children: chips),
      ),
    );
  }

  /// A status pill (Figma 1408:9761+): the status colour at 12% fill + 20%
  /// border, radius-4, h-32; the colour at full strength for the text. Tapping
  /// toggles the filter (a stronger fill marks the selected chip).
  Widget _chip({
    required String label,
    required int count,
    required AppRequestStatus status,
  }) {
    final color = requestStatusColor(status);
    final active = selected == status;
    return InkWell(
      onTap: () => onSelect(active ? null : status),
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        height: SimfTokens.statusChipHeight,
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.statusChipPadH,
        ),
        decoration: BoxDecoration(
          color: color.withValues(
            alpha: active
                ? SimfTokens.chipFillActiveOpacity
                : SimfTokens.chipFillOpacity,
          ),
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          border: Border.all(
            color: color.withValues(
              alpha: active ? 1 : SimfTokens.chipBorderOpacity,
            ),
          ),
        ),
        child: Text(
          '$label ($count)',
          style: TextStyle(
            color: color,
            fontSize: SimfTokens.textSm, // 12
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}
