import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/speaker_models.dart';
import 'data/speakers_repository.dart';
import 'speaker_initials.dart';

/// Page 019 — المتحدثون · Speakers list (#19, `/speakers`, Guest+).
///
/// **Public.** One read (`GET /app/speakers`) draws the ordered speaker cards;
/// tapping a card opens the profile (20). UI is interim — the avatar renders as
/// initials and the country as text (the flag/photo asset pass is SIMF-VID-001).
class SpeakersScreen extends ConsumerStatefulWidget {
  const SpeakersScreen({super.key});

  @override
  ConsumerState<SpeakersScreen> createState() => _SpeakersScreenState();
}

class _SpeakersScreenState extends ConsumerState<SpeakersScreen> {
  bool _loading = true;
  bool _error = false;
  List<SpeakerSummary> _speakers = const <SpeakerSummary>[];

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final speakers = await ref.read(speakersRepositoryProvider).getSpeakers();
      if (!mounted) {
        return;
      }
      setState(() {
        _speakers = speakers;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.speakersTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _ErrorState(message: l10n.speakersError, onRetry: () => unawaited(_load()));
    }
    if (_speakers.isEmpty) {
      return _EmptyState(message: l10n.speakersEmpty);
    }
    final isArabic = l10n.isArabic;
    return ListView.separated(
      padding: const EdgeInsets.all(SimfTokens.space4),
      itemCount: _speakers.length,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
      itemBuilder: (context, index) {
        final speaker = _speakers[index];
        return _SpeakerCard(
          speaker: speaker,
          isArabic: isArabic,
          onTap: () => context.pushNamed(
            RouteNames.speakerProfile,
            pathParameters: <String, String>{'speakerId': speaker.id},
          ),
        );
      },
    );
  }
}

/// One speaker card (mockup `.sp-card`): a gold avatar on the leading edge, the
/// gold rank label above the white name, and a trailing go-arrow — on the navy
/// surface-tint card the theme already supplies.
class _SpeakerCard extends StatelessWidget {
  const _SpeakerCard({
    required this.speaker,
    required this.isArabic,
    required this.onTap,
  });

  final SpeakerSummary speaker;
  final bool isArabic;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final country = speaker.localizedCountry(isArabic);
    final rank = (speaker.rank != null && speaker.rank!.trim().isNotEmpty)
        ? speaker.rank!.trim()
        : null;
    final labelParts = <String>[
      if (rank != null) rank,
      if (country != null) country,
    ];
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Row(
            children: <Widget>[
              CircleAvatar(
                radius: 20,
                backgroundColor: SimfTokens.accent,
                foregroundColor: SimfTokens.navy,
                child: Text(
                  speakerInitials(speaker.localizedName(isArabic)),
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
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    if (labelParts.isNotEmpty) ...<Widget>[
                      Text(
                        labelParts.join(' · '),
                        style: const TextStyle(
                          color: SimfTokens.accent,
                          fontWeight: FontWeight.w600,
                          fontSize: SimfTokens.textXs,
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space1),
                    ],
                    Text(
                      speaker.localizedName(isArabic),
                      style: const TextStyle(
                        color: SimfTokens.surface,
                        fontWeight: FontWeight.w600,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              const Icon(
                Icons.chevron_left,
                color: SimfTokens.txtTertiary,
                size: 18,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.groups_outlined,
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

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

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
