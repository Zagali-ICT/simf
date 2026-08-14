import 'package:flutter/foundation.dart';
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

  String localizedTitle(bool isArabic) => _pick(titleArabic, title, isArabic);
  String localizedCategory(bool isArabic) =>
      _pick(categoryArabic, category, isArabic);
  String? localizedExcerpt(bool isArabic) =>
      _pickOpt(excerptArabic, excerpt, isArabic);

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

  String localizedTitle(bool isArabic) => _pick(titleArabic, title, isArabic);
  String localizedCategory(bool isArabic) =>
      _pick(categoryArabic, category, isArabic);
  String localizedBody(bool isArabic) => _pick(bodyArabic, body, isArabic);
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

String _pick(String arabic, String english, bool isArabic) {
  final ar = arabic.trim();
  final en = english.trim();
  return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
}

String? _pickOpt(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}
