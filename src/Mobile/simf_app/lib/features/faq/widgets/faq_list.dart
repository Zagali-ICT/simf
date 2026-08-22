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
    // Flattened to one row per line so a long catalogue builds lazily. A row
    // whose entry is null is a group header; each row owns the trailing gap the
    // sibling spacers used to add, so the last tile keeps its gap as before.
    final rows = <(String?, FaqEntry?)>[];
    for (final group in groups) {
      if (group.entries.isEmpty) {
        continue;
      }
      if (showGroupHeaders) {
        rows.add((group.localizedName(isArabic: isArabic), null));
      }
      for (final entry in group.entries) {
        rows.add((null, entry));
      }
    }
    return SimfPullToRefresh(
      onRefresh: onRefresh,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        itemCount: rows.length,
        itemBuilder: (context, index) {
          final (groupName, entry) = rows[index];
          return Padding(
            padding: const EdgeInsets.only(bottom: SimfTokens.space3),
            child: entry == null
                ? SimfSectionHeader(title: groupName!)
                : FaqTile(
                    key: ValueKey<String>(entry.id),
                    entry: entry,
                    isArabic: isArabic,
                  ),
          );
        },
      ),
    );
  }
}
