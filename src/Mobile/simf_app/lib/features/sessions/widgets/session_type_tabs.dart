import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/type_tab.dart';

/// The three type tabs (frame node 883:2320): الكل (all) / جلسات (sessions) /
/// ورش العمل (workshops). The active tab is solid gold; the rest are bordered
/// navy cards. الكل = no type filter. Client-side filter. The old fourth
/// احداث tab was dropped to match the frame (owner 2026-07-03, D-595-style
/// call) — event-type sessions remain visible under الكل.
///
/// Deliberately NOT the shared [SessionFilterTabs]: this frame boxes equal-width
/// 41px cells inside a navy container, while the shared bar renders freestanding
/// hairline pills (1388:8392 family) — different chrome per its frame.
class SessionTypeTabs extends StatelessWidget {
  const SessionTypeTabs({
    required this.l10n,
    required this.active,
    required this.onChanged,
    super.key,
  });

  final AppL10n l10n;
  final SessionType? active;
  final ValueChanged<SessionType?> onChanged;

  @override
  Widget build(BuildContext context) {
    // Frame 883:2320 (verified against the render): الكل (All) leads at the
    // inline-start — rightmost in RTL — then جلسات · ورش العمل. A Row lays
    // children start→end, so All is the first entry.
    final tabs = <(String, SessionType?)>[
      (l10n.sessionTypeAll, null),
      (l10n.sessionTypeSession, SessionType.session),
      (l10n.sessionTypeWorkshop, SessionType.workshop),
    ];
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          for (final (i, (label, type)) in tabs.indexed) ...<Widget>[
            if (i > 0) const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: TypeTab(
                label: label,
                active: type == active,
                onTap: () => onChanged(type),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

