import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/bilingual.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// One news row — mirrors `PublicNewsListItem` (`GET /app/news`).
@immutable
class NewsListItem {
  const NewsListItem({
    required this.id,
    required this.title,
    required this.titleArabic,
    required this.category,
    required this.categoryArabic,
    required this.publishedAt,
    this.excerpt,
    this.excerptArabic,
    this.imageRelativePath,
  });

  factory NewsListItem.fromJson(Map<String, dynamic> json) => NewsListItem(
        id: json['id'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        category: json['category'] as String? ?? '',
        categoryArabic: json['categoryArabic'] as String? ?? '',
        publishedAt: _utc(json['publishedAt']),
        excerpt: json['excerpt'] as String?,
        excerptArabic: json['excerptArabic'] as String?,
        imageRelativePath: json['imageRelativePath'] as String?,
      );

  final String id;
  final String title;
  final String titleArabic;
  final String category;
  final String categoryArabic;
  final DateTime publishedAt;
  final String? excerpt;
  final String? excerptArabic;
  final String? imageRelativePath;

  String localizedTitle({required bool isArabic}) =>
      pickLocalized(titleArabic, title, isArabic: isArabic);
  String localizedCategory({required bool isArabic}) =>
      pickLocalized(categoryArabic, category, isArabic: isArabic);
  String? localizedExcerpt({required bool isArabic}) =>
      pickLocalizedOrNull(excerptArabic, excerpt, isArabic: isArabic);

  /// Reads `PublicNewsPage = { items: [...] }`.
  static List<NewsListItem> listFromData(Object? data) =>
      ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => NewsListItem.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// The full article — mirrors `PublicNewsArticle` (`GET /app/news/{id}`).
@immutable
class NewsArticle {
  const NewsArticle({
    required this.id,
    required this.title,
    required this.titleArabic,
    required this.body,
    required this.bodyArabic,
    required this.category,
    required this.categoryArabic,
    required this.publishedAt,
    this.imageRelativePath,
  });

  factory NewsArticle.fromJson(Map<String, dynamic> json) => NewsArticle(
        id: json['id'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        body: json['body'] as String? ?? '',
        bodyArabic: json['bodyArabic'] as String? ?? '',
        category: json['category'] as String? ?? '',
        categoryArabic: json['categoryArabic'] as String? ?? '',
        publishedAt: _utc(json['publishedAt']),
        imageRelativePath: json['imageRelativePath'] as String?,
      );

  final String id;
  final String title;
  final String titleArabic;
  final String body;
  final String bodyArabic;
  final String category;
  final String categoryArabic;
  final DateTime publishedAt;
  final String? imageRelativePath;

  String localizedTitle({required bool isArabic}) =>
      pickLocalized(titleArabic, title, isArabic: isArabic);
  String localizedCategory({required bool isArabic}) =>
      pickLocalized(categoryArabic, category, isArabic: isArabic);
  String localizedBody({required bool isArabic}) =>
      pickLocalized(bodyArabic, body, isArabic: isArabic);
}

DateTime _utc(Object? value) {
  if (value is String && value.isNotEmpty) {
    final parsed = parseWireOrNull(value);
    if (parsed != null) {
      return parsed;
    }
  }
  return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
}
