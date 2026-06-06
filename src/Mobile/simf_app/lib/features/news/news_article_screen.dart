import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/news_models.dart';

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
      final client = ref.read(simfApiClientProvider);
      final article = await client.get<NewsArticle>(
        '/app/news/${widget.newsId}',
        decodeData: (data) => NewsArticle.fromJson(
          (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
        ),
      );
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
    return Scaffold(
      appBar: AppBar(title: Text(l10n.newsTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return Center(
        child: Text(
          l10n.newsNotFound,
          style: const TextStyle(color: SimfTokens.inkMuted),
        ),
      );
    }
    if (_error || _article == null) {
      return Center(
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
      );
    }
    final article = _article!;
    final isArabic = l10n.isArabic;
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        Text(
          article.localizedCategory(isArabic),
          style: const TextStyle(
            color: SimfTokens.accent,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textXs,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          article.localizedTitle(isArabic),
          style: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textXl,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(article.localizedBody(isArabic)),
      ],
    );
  }
}
