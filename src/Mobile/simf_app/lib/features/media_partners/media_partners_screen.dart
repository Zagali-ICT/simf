import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';

/// One media partner — mirrors `PublicMediaPartnerItem` (`name`/`nameArabic`).
@immutable
class MediaPartner {
  const MediaPartner({
    required this.id,
    required this.name,
    required this.nameArabic,
    this.logoRelativePath,
    this.url,
  });

  final String id;
  final String name;
  final String nameArabic;
  final String? logoRelativePath;
  final String? url;

  String localizedName(bool isArabic) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static MediaPartner fromJson(Map<String, dynamic> json) => MediaPartner(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        logoRelativePath: json['logoRelativePath'] as String?,
        url: json['url'] as String?,
      );
}

/// `GET /app/media-partners` → the flat partner list (public, D-199).
final mediaPartnersProvider =
    FutureProvider.autoDispose<List<MediaPartner>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<MediaPartner>>(
    '/app/media-partners',
    decodeData: (data) =>
        ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => MediaPartner.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
  );
});

/// Page 031 — الشركاء الإعلاميون · Media partners (#31, `/media-partners`, Guest+).
class MediaPartnersScreen extends ConsumerWidget {
  const MediaPartnersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final partners = ref.watch(mediaPartnersProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.mediaPartnersTitle)),
      body: SafeArea(
        child: partners.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _Error(
            message: l10n.mediaPartnersError,
            onRetry: () => ref.invalidate(mediaPartnersProvider),
          ),
          data: (items) {
            if (items.isEmpty) {
              return _Empty(message: l10n.mediaPartnersEmpty);
            }
            final isArabic = l10n.isArabic;
            return ListView.separated(
              padding: const EdgeInsets.all(SimfTokens.space4),
              itemCount: items.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(height: SimfTokens.space2),
              itemBuilder: (context, index) {
                final partner = items[index];
                return Card(
                  margin: EdgeInsets.zero,
                  clipBehavior: Clip.antiAlias,
                  child: ListTile(
                    leading: const Icon(
                      Icons.campaign_outlined,
                      color: SimfTokens.accent,
                    ),
                    title: Text(
                      partner.localizedName(isArabic),
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    subtitle: partner.url == null ? null : Text(partner.url!),
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(Icons.campaign_outlined, size: 56, color: SimfTokens.inkMuted),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
        ],
      ),
    );
  }
}

class _Error extends StatelessWidget {
  const _Error({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
