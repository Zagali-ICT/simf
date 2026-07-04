import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/request_models.dart';

/// The localized label for a request [status] (shared by the status chips + the
/// expanded card detail).
String requestStatusLabel(AppL10n l10n, AppRequestStatus status) {
  switch (status) {
    case AppRequestStatus.accepted:
      return l10n.requestStatusAccepted;
    case AppRequestStatus.rejected:
      return l10n.requestStatusRejected;
    case AppRequestStatus.cancelled:
      return l10n.requestStatusCancelled;
    case AppRequestStatus.pending:
      return l10n.requestStatusPending;
  }
}

/// The theme colour for a request [status].
Color requestStatusColor(AppRequestStatus status) {
  switch (status) {
    case AppRequestStatus.accepted:
      return SimfTokens.statusAccepted;
    case AppRequestStatus.rejected:
      return SimfTokens.statusRejected;
    case AppRequestStatus.cancelled:
      return SimfTokens.statusCancelled;
    case AppRequestStatus.pending:
      return SimfTokens.qStage; // amber #F59E0B (Figma قيد المراجعة)
  }
}

/// The display order of the status chips (Figma 1408:9760): cancelled, rejected,
/// pending, accepted — only those with at least one request are shown.
const List<AppRequestStatus> kRequestChipOrder = <AppRequestStatus>[
  AppRequestStatus.cancelled,
  AppRequestStatus.rejected,
  AppRequestStatus.pending,
  AppRequestStatus.accepted,
];
