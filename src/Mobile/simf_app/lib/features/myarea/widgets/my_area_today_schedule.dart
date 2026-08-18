import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/widgets/my_area_schedule_group_header.dart';
import 'package:simf_app/features/myarea/widgets/my_area_schedule_row.dart';

/// The جدولي اليوم section of the My-Area dashboard. Frame 758:1283 — the day
/// splits into a "جلسات" group and a "مقابلات" group, each under its own gold
/// sub-header.
class MyAreaTodaySchedule extends StatelessWidget {
  const MyAreaTodaySchedule({
    required this.items,
    required this.isArabic,
    super.key,
  });

  final List<MyAreaScheduleItem> items;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final sessions = items.where((i) => i.isSession).toList();
    final meetings = items.where((i) => !i.isSession).toList();

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfSectionHeader(title: l10n.todayScheduleTitle),
        const SizedBox(height: SimfTokens.space3),
        if (sessions.isEmpty && meetings.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: SimfTokens.space4),
            child: Text(
              l10n.scheduleEmpty,
              style: SimfTokens.hintBeige,
            ),
          )
        else
          for (final (groupLabel, items)
              in <(String, List<MyAreaScheduleItem>)>[
            (l10n.scheduleSessionsGroup, sessions),
            (l10n.scheduleMeetingsGroup, meetings),
          ])
            if (items.isNotEmpty) ...<Widget>[
              MyAreaScheduleGroupHeader(label: groupLabel),
              const SizedBox(height: SimfTokens.space3),
              for (final item in items)
                Padding(
                  padding: const EdgeInsets.only(bottom: SimfTokens.space3),
                  child: MyAreaScheduleRow(
                    item: item,
                    isArabic: isArabic,
                    onTap: item.isSession && item.sessionId != null
                        ? () => context.pushNamed(
                              RouteNames.sessionDetail,
                              pathParameters: <String, String>{
                                RouteParams.sessionId: item.sessionId!,
                              },
                            )
                        : null,
                  ),
                ),
            ],
      ],
    );
  }
}
