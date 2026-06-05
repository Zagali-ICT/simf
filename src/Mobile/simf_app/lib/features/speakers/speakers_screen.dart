import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
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
      appBar: AppBar(title: Text(l10n.speakersTitle)),
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
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space2),
      itemBuilder: (context, index) {
        final speaker = _speakers[index];
        final country = speaker.localizedCountry(isArabic);
        final subParts = <String>[
          if (speaker.rank != null && speaker.rank!.trim().isNotEmpty)
            speaker.rank!.trim(),
          if (country != null) country,
        ];
        return Card(
          margin: EdgeInsets.zero,
          clipBehavior: Clip.antiAlias,
          child: ListTile(
            onTap: () => context.pushNamed(
              RouteNames.speakerProfile,
              pathParameters: <String, String>{'speakerId': speaker.id},
            ),
            leading: CircleAvatar(
              backgroundColor: SimfTokens.field,
              child: Text(
                speakerInitials(speaker.localizedName(isArabic)),
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
            title: Text(
              speaker.localizedName(isArabic),
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            subtitle: subParts.isEmpty ? null : Text(subParts.join(' · ')),
            trailing: const Icon(Icons.chevron_right, color: SimfTokens.accent),
          ),
        );
      },
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
          const Icon(Icons.groups_outlined, size: 56, color: SimfTokens.inkMuted),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
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
