import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/faq/data/faq_models.dart';
import 'package:simf_app/features/faq/widgets/faq_tile.dart';

/// The FAQ accordion itself: one expandable tile per entry, under a group
/// header when the catalogue has more than one group (a single-group catalogue
/// renders the flat accordion the design shows).
class FaqList extends StatelessWidget {
  const FaqList({
    required this.groups,
    required this.isArabic,
    required this.onRefresh,
    super.key,
  });

  final List<FaqGroup> groups;
  final bool isArabic;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    final showGroupHeaders = groups.length > 1;
    return SimfPullToRefresh(
      onRefresh: onRefresh,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        children: <Widget>[
          for (final group in groups)
            if (group.entries.isNotEmpty) ...<Widget>[
              if (showGroupHeaders) ...<Widget>[
                SimfSectionHeader(
                  title: group.localizedName(isArabic: isArabic),
                ),
                const SizedBox(height: SimfTokens.space3),
              ],
              for (final entry in group.entries) ...<Widget>[
                FaqTile(entry: entry, isArabic: isArabic),
                const SizedBox(height: SimfTokens.space3),
              ],
            ],
        ],
      ),
    );
  }
}
