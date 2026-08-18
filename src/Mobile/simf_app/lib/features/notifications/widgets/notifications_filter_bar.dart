import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/notifications/notification_filters.dart';
import 'package:simf_app/features/notifications/widgets/notification_filter_chip.dart';

/// The inbox chip row: the الكل / جلسات / VIP filter chips, with the
/// "mark all read" action at the inline end while anything is still unread.
class NotificationsFilterBar extends StatelessWidget {
  const NotificationsFilterBar({
    required this.l10n,
    required this.filter,
    required this.onFilter,
    required this.showMarkAll,
    required this.onMarkAll,
    super.key,
  });

  final AppL10n l10n;
  final NotificationFilter filter;
  final ValueChanged<NotificationFilter> onFilter;
  final bool showMarkAll;

  /// Null while a mark-all is in flight, which renders the button disabled.
  final VoidCallback? onMarkAll;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        NotificationFilterChip(
          label: l10n.notificationsFilterAll,
          selected: filter == NotificationFilter.all,
          onTap: () => onFilter(NotificationFilter.all),
        ),
        const SizedBox(width: SimfTokens.space2),
        NotificationFilterChip(
          label: l10n.notificationsFilterSessions,
          selected: filter == NotificationFilter.sessions,
          onTap: () => onFilter(NotificationFilter.sessions),
        ),
        const SizedBox(width: SimfTokens.space2),
        NotificationFilterChip(
          label: l10n.notificationsFilterVip,
          selected: filter == NotificationFilter.vip,
          onTap: () => onFilter(NotificationFilter.vip),
        ),
        const Spacer(),
        if (showMarkAll)
          TextButton(
            onPressed: onMarkAll,
            child: Text(
              l10n.notificationsMarkAll,
              style: SimfTokens.labelGoldSm,
            ),
          ),
      ],
    );
  }
}
