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
            // Mockup Page 031 `.partners` — a 2-column grid of `.partner`
            // cards (logo box + caption).
            return GridView.builder(
              padding: const EdgeInsets.all(SimfTokens.space4),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 2,
                mainAxisSpacing: SimfTokens.space2,
                crossAxisSpacing: SimfTokens.space2,
                childAspectRatio: 1.6,
              ),
              itemCount: items.length,
              itemBuilder: (context, index) {
                final partner = items[index];
                return _PartnerCard(name: partner.localizedName(isArabic));
              },
            );
          },
        ),
      ),
    );
  }
}

/// One partner — mockup `.partner` card (logo box · caption). The logo box
/// renders the partner's initials (interim — no logo asset yet); the caption
/// below carries the partner name.
class _PartnerCard extends StatelessWidget {
  const _PartnerCard({required this.name});

  final String name;

  String get _initials {
    final words = name.trim().split(RegExp(r'\s+'));
    final letters = words
        .where((w) => w.isNotEmpty)
        .take(2)
        .map((w) => w.characters.first)
        .join();
    return letters.isEmpty ? '—' : letters.toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space3,
        ),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Text(
              _initials,
              style: const TextStyle(
                color: SimfTokens.surface,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textMd,
                letterSpacing: 0.5,
              ),
            ),
            const SizedBox(height: SimfTokens.space1),
            Text(
              name,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: SimfTokens.txtTertiary,
                fontSize: SimfTokens.textXs,
                height: 1.4,
              ),
            ),
          ],
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
          const Icon(
            Icons.campaign_outlined,
            size: 56,
            color: SimfTokens.txtTertiary,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.txtSecondary)),
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
