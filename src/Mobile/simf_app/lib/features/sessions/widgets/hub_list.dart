import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/hub_row.dart';

class HubList extends StatelessWidget {
  const HubList({required this.items, required this.l10n});

  final List<SessionListItem> items;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    return ListView.separated(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space3,
        SimfTokens.space4,
        SimfTokens.space5,
      ),
      itemCount: items.length + 1,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
      itemBuilder: (context, index) {
        if (index == 0) {
          return Padding(
            padding: const EdgeInsets.only(bottom: SimfTokens.space2),
            child: Text(
              l10n.joinHubHint,
              textAlign: TextAlign.center,
              style: SimfTokens.labelBeigeSm,
            ),
          );
        }
        final item = items[index - 1];
        return HubRow(
          title: item.localizedTitle(isArabic),
          subtitle: _subtitle(context, item, isArabic),
          onTap: () => context.pushNamed(
            RouteNames.sessionDetail,
            pathParameters: <String, String>{RouteParams.sessionId: item.id},
          ),
        );
      },
    );
  }

  String _subtitle(BuildContext context, SessionListItem item, bool isArabic) {
    final time = TimeOfDay.fromDateTime(item.startLocal).format(context);
    final hall = item.localizedHall(isArabic);
    return hall == null || hall.isEmpty ? time : '$time · $hall';
  }
}
