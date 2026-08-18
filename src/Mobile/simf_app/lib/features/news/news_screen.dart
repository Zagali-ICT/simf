import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/media_coverage_tabs.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/news/widgets/news_list_body.dart';

/// News — route: `RouteNames.news` · Figma 1049:12629
class NewsScreen extends ConsumerWidget {
  const NewsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      // Frame header — the container is "التغطية الإعلامية" (Media coverage),
      // not the bare "الأخبار" tab label.
      title: l10n.mediaCoverageTitle,
      onBack: () => backOrHome(context),
      // News left the bottom nav in the KSA Wave-2 shell (the Profile tab took
      // its slot) — the bar stays, with no destination highlighted.
      body: const Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space2,
              SimfTokens.space4,
              SimfTokens.space2,
            ),
            child: MediaCoverageTabs(active: MediaCoverageTab.latestUpdates),
          ),
          Expanded(child: NewsListBody()),
        ],
      ),
    );
  }
}
