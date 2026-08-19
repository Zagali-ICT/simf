import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/accessibility/widgets/accessibility_toggle_row.dart';

/// The قارئ الشاشة switch (frame 1116:16630).
///
/// Switching it ON also announces the setting straight away, through the same
/// platform channel the assist itself uses — the confirmation a user of this
/// switch can actually perceive.
class AccessibilityScreenReaderRow extends StatelessWidget {
  const AccessibilityScreenReaderRow({
    required this.value,
    required this.onChanged,
    super.key,
  });

  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return AccessibilityToggleRow(
      title: l10n.accessibilityScreenReaderTitle,
      hint: l10n.accessibilityScreenReaderSubtitle,
      value: value,
      onChanged: (v) {
        onChanged(v);
        if (v) {
          unawaited(
            SemanticsService.sendAnnouncement(
              View.of(context),
              l10n.accessibilityScreenReaderTitle,
              Directionality.of(context),
            ),
          );
        }
      },
    );
  }
}
