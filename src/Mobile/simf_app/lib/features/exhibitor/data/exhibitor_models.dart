import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

import '../../contacts/data/contact_models.dart';

/// D-426 — one captured visitor in an exhibitor's "My Visitors" list. Mirrors
/// `SIMF.Contracts.Exhibitors.ExhibitorVisitorRow`: scan metadata plus the
/// visitor's full [VisitorCard] (reused from the contact-share feature; same
/// wire shape). Decodes tolerantly (append-only, D-219).
@immutable
class ExhibitorVisitor {
  const ExhibitorVisitor({
    required this.id,
    required this.scannedAt,
    required this.card,
    this.note,
  });

  final String id;
  final DateTime scannedAt;
  final String? note;
  final VisitorCard card;

  static ExhibitorVisitor fromData(Object? data) {
    final map = data is Map ? data.cast<String, dynamic>() : const <String, dynamic>{};
    return ExhibitorVisitor(
      id: map['id'] as String? ?? '',
      scannedAt: parseWireOrNull(map['scannedAt'] as String? ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      note: map['note'] as String?,
      card: VisitorCard.fromData(map['card']),
    );
  }

  static List<ExhibitorVisitor> listFromData(Object? data) =>
      (data is List ? data : const <dynamic>[])
          .map(ExhibitorVisitor.fromData)
          .toList();
}
