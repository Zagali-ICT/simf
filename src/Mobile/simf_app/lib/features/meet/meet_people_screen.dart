import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/meet_models.dart';

/// `GET /app/account/recommendations/meet-like-you` → the visitor's "meet
/// someone like you" matches (RequireApprovedAccount).
final meetRecommendationsProvider =
    FutureProvider.autoDispose<List<Recommendation>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<Recommendation>>(
    '/app/account/recommendations/meet-like-you',
    decodeData: Recommendation.listFromData,
  );
});

/// Page 035 — قابل أشخاص مثلك · Meet people (#35, `/meet`, approved account).
///
/// **Auth-gated** (route 35). One read returns the recommended profiles scored
/// by shared interests; the screen renders a card per match — an initials
/// avatar, the localized name, a `jobTitle · profileType` sub-line, a Wrap of
/// the shared-interest chips and the shared-interest count. UI is interim
/// (avatar = initials) until the SIMF-VID-001 pass.
class MeetPeopleScreen extends ConsumerWidget {
  const MeetPeopleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final matches = ref.watch(meetRecommendationsProvider);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.meetPeopleTitle)),
      body: SafeArea(
        child: matches.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _Error(
            message: l10n.meetPeopleError,
            onRetry: () => ref.invalidate(meetRecommendationsProvider),
          ),
          data: (data) {
            if (data.isEmpty) {
              return _Empty(message: l10n.meetPeopleEmpty);
            }
            final isArabic = l10n.isArabic;
            return ListView.builder(
              padding: const EdgeInsets.all(SimfTokens.space4),
              itemCount: data.length,
              itemBuilder: (context, index) => _MatchCard(
                match: data[index],
                isArabic: isArabic,
                l10n: l10n,
              ),
            );
          },
        ),
      ),
    );
  }
}

class _MatchCard extends StatelessWidget {
  const _MatchCard({
    required this.match,
    required this.isArabic,
    required this.l10n,
  });

  final Recommendation match;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final name = match.localizedName(isArabic);
    final profileType = match.localizedProfileType(isArabic);
    final jobTitle = match.jobTitle?.trim();
    final sub = <String>[
      if (jobTitle != null && jobTitle.isNotEmpty) jobTitle,
      if (profileType != null) profileType,
    ];
    return Card(
      margin: const EdgeInsets.only(bottom: SimfTokens.space3),
      clipBehavior: Clip.antiAlias,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                CircleAvatar(
                  radius: 24,
                  backgroundColor: SimfTokens.field,
                  child: Text(
                    _initials(name),
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: SimfTokens.textMd,
                    ),
                  ),
                ),
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        name,
                        style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: SimfTokens.textLg,
                        ),
                      ),
                      if (sub.isNotEmpty) ...<Widget>[
                        const SizedBox(height: SimfTokens.space1),
                        Text(
                          sub.join(' · '),
                          style: const TextStyle(color: SimfTokens.inkMuted),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
            if (match.sharedInterests.isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              Wrap(
                spacing: SimfTokens.space2,
                runSpacing: SimfTokens.space1,
                children: <Widget>[
                  for (final interest in match.sharedInterests)
                    Chip(
                      label: Text(interest.localizedName(isArabic)),
                      backgroundColor: SimfTokens.field,
                      visualDensity: VisualDensity.compact,
                    ),
                ],
              ),
            ],
            const SizedBox(height: SimfTokens.space2),
            Text(
              l10n.meetPeopleSharedInterests(match.sharedInterestCount),
              style: const TextStyle(
                color: SimfTokens.accent,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

String _initials(String name) {
  final parts =
      name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) {
    return '?';
  }
  if (parts.length == 1) {
    return parts.first.substring(0, 1).toUpperCase();
  }
  return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
      .toUpperCase();
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
            Icons.people_outline,
            size: 56,
            color: SimfTokens.inkMuted,
          ),
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
