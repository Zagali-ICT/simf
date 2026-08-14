import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// A published CMS content block — mirrors `SIMF.Contracts.Cms.PublicContentBlock`
/// (`GET /app/content/{key}` → `{ key, content, contentArabic, lastUpdatedAt }`).
/// Read-only; the app never writes content. Used by the Terms page (key `terms`)
/// and any future static-content screen.
@immutable
class ContentBlock {
  const ContentBlock({
    required this.key,
    required this.content,
    required this.contentArabic,
    this.lastUpdatedAt,
  });

  factory ContentBlock.fromJson(Map<String, dynamic> json) => ContentBlock(
        key: json['key'] as String? ?? '',
        content: json['content'] as String? ?? '',
        contentArabic: json['contentArabic'] as String? ?? '',
        lastUpdatedAt: parseWireOrNull(json['lastUpdatedAt'] as String? ?? ''),
      );

  final String key;
  final String content; // English body (HTML/markdown)
  final String contentArabic; // Arabic body (HTML/markdown)
  final DateTime? lastUpdatedAt;

  /// The body for the active locale, falling back to the other language when the
  /// requested one is empty (Arabic primary, English secondary — Page_009 L-8).
  String localizedBody({required bool isArabic}) {
    if (isArabic) {
      return contentArabic.trim().isNotEmpty ? contentArabic : content;
    }
    return content.trim().isNotEmpty ? content : contentArabic;
  }

  bool get hasBody =>
      content.trim().isNotEmpty || contentArabic.trim().isNotEmpty;
}
