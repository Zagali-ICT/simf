import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/data/news_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The full news article (Page_029 detail, `GET /app/news/{id}`). Pushed from the
/// news list via an imperative route (the screen has no go_router entry).
class NewsArticleScreen extends ConsumerStatefulWidget {
  const NewsArticleScreen({required this.newsId, super.key});

  final String newsId;

  @override
  ConsumerState<NewsArticleScreen> createState() => _NewsArticleScreenState();
}

class _NewsArticleScreenState extends ConsumerState<NewsArticleScreen> {
  bool _loading = true;
  bool _error = false;
  bool _notFound = false;
  NewsArticle? _article;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _notFound = false;
    });
    try {
      final article =
          await ref.read(newsRepositoryProvider).getArticle(widget.newsId);
      if (!mounted) {
        return;
      }
      setState(() {
        _article = article;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _notFound = failure.httpStatus == 404;
        _error = failure.httpStatus != 404;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.newsTitle,
      onBack: () => backOrHome(context),
      body: SimfPullToRefresh(
        onRefresh: _load,
        child: _buildBody(l10n),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return SimfPullableHost(
        child: Center(
          child: Text(
            l10n.newsNotFound,
            style: SimfTokens.bodyInkMuted,
          ),
        ),
      );
    }
    if (_error || _article == null) {
      return SimfPullableHost(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(SimfTokens.space6),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(l10n.newsError, textAlign: TextAlign.center),
                const SizedBox(height: SimfTokens.space4),
                FilledButton(
                  onPressed: () => unawaited(_load()),
                  child: Text(l10n.retryLabel),
                ),
              ],
            ),
          ),
        ),
      );
    }
    final article = _article!;
    final isArabic = l10n.isArabic;
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        Text(
          article.localizedCategory(isArabic),
          style: SimfTokens.labelGoldBoldXs,
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          article.localizedTitle(isArabic),
          style: SimfTokens.titleBoldXl,
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(article.localizedBody(isArabic)),
      ],
    );
  }
}
