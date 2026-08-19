import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/home/widgets/highlights_carousel.dart';
import 'package:simf_app/features/news/data/news_models.dart';

/// ابرز الاحداث (frame node 758:1239) — the highlights carousel (image + title
/// slides, auto-advancing). Owns its leading gap; the caller hides the whole
/// section until a post exists.
class HomeHighlightsSection extends StatelessWidget {
  const HomeHighlightsSection({
    required this.l10n,
    required this.items,
    required this.baseUrl,
    super.key,
  });

  final AppL10n l10n;
  final List<NewsListItem> items;
  final String baseUrl;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space6),
        SimfSectionHeader(title: l10n.featuredEventsSection),
        const SizedBox(height: SimfTokens.space4),
        HighlightsCarousel(
          l10n: l10n,
          items: items,
          baseUrl: baseUrl,
          onTap: (post) => context.pushNamed(
            RouteNames.newsArticle,
            pathParameters: <String, String>{RouteParams.newsId: post.id},
          ),
        ),
      ],
    );
  }
}
