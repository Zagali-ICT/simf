import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/country_flag_badge.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_svg_icon.dart';
import 'data/speaker_models.dart';
import 'data/speakers_repository.dart';

/// Page 019 — المتحدثون · Speakers list (#19, `/speakers`, Guest+), rebuilt to
/// the KSA-Project Figma frame **908:1744 "Speakers"** on the shared navy shell.
///
/// **Public.** One read (`GET /app/speakers`) draws the ordered speaker cards;
/// tapping a card opens the profile (Page 020). Frame mapping: the navy shell
/// with the centred header المتحدثون + circled back chevron (the profile's
/// header pattern, 908:2110), then a vertical list of cards — each a navy
/// `#192B41` card on the beige `0.2px` hairline (the shared [KsaCard]) carrying,
/// in RTL: a 44×44 gold-bordered tile holding an anchor glyph at the inline
/// start (right), the white name (16/SemiBold) over the beige rank·affiliation
/// line (12/Regular), and a small beige caret at the inline end (left).
///
/// The avatar tile renders the speaker's uploaded SpeakerPhoto asset (D-357),
/// falling back to the gold anchor glyph when none; the country renders as text.
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
    return KsaPage(
      onBack: () => ksaBackOrHome(context),
      header: _buildHeader(l10n),
      body: _buildBody(l10n),
    );
  }

  /// The frame's centred title flanked by the circled back chevron and a
  /// balancing spacer (the speaker-profile header pattern, 908:2110), so the
  /// title stays optically centred under the navy shell.
  Widget _buildHeader(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          SizedBox(
            width: 42,
            height: 42,
            child: KsaBackButton(onBack: () => ksaBackOrHome(context)),
          ),
          Expanded(
            child: Text(
              l10n.speakersTitle,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: SimfTokens.textTitle,
                fontWeight: FontWeight.w600,
                color: Colors.white,
              ),
            ),
          ),
          // Balances the leading back button so the title stays centred.
          const SizedBox(width: 42, height: 42),
        ],
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_error) {
      // Hosted in a scrollable so pull-to-refresh works in the error state
      // (lets the user pull to retry).
      return KsaRefresh(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            KsaErrorState(
              message: l10n.speakersError,
              retryLabel: l10n.retryLabel,
              onRetry: () => unawaited(_load()),
            ),
          ],
        ),
      );
    }
    if (_speakers.isEmpty) {
      // Hosted in a scrollable so pull-to-refresh works in the empty state.
      return KsaRefresh(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            KsaEmptyState(
              icon: Icons.groups_outlined,
              message: l10n.speakersEmpty,
            ),
          ],
        ),
      );
    }
    final isArabic = l10n.isArabic;
    // The card builds `{base}/app/assets/SpeakerPhoto/{id}/image` for the avatar.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    return KsaRefresh(
      onRefresh: _load,
      child: ListView.separated(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        itemCount: _speakers.length,
        // Frame 908:1744 — cards pitch 76px (card 60 + 16 gap).
        separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space4),
        itemBuilder: (context, index) {
          final speaker = _speakers[index];
          return _SpeakerCard(
            speaker: speaker,
            isArabic: isArabic,
            baseUrl: baseUrl,
            onTap: () => context.pushNamed(
              RouteNames.speakerProfile,
              pathParameters: <String, String>{'speakerId': speaker.id},
            ),
          );
        },
      ),
    );
  }
}

/// One speaker card (frame 908:1999): the navy [KsaCard] chrome carrying — in
/// RTL — a 44×44 gold-bordered anchor tile at the inline start (right), the
/// white name over the beige rank·affiliation line, and a small beige caret at
/// the inline end (left). D-432: the host/speaker distinction is per-session
/// (it lives on the
/// session↔speaker join), not a global speaker attribute, so the global list
/// shows the anchor for everyone; the host star appears on the session detail.
class _SpeakerCard extends StatelessWidget {
  const _SpeakerCard({
    required this.speaker,
    required this.isArabic,
    required this.baseUrl,
    required this.onTap,
  });

  final SpeakerSummary speaker;
  final bool isArabic;
  final String baseUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // The country now shows as a small flag badge on the avatar's top-left
    // corner (owner, ref node 889:2722), so the sub-line carries only the rank.
    final label = (speaker.rank != null && speaker.rank!.trim().isNotEmpty)
        ? speaker.rank!.trim()
        : '';

    return KsaCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        // Figma 908:1744 (Arabic/RTL frame): the photo tile sits at the
        // inline-start (right) beside the name, the navigation caret at the
        // inline-end (left). A Row lays children start→end, so the order is
        // avatar → name → caret.
        child: Row(
          children: <Widget>[
            _SpeakerAvatar(
              imageUrl: '$baseUrl/app/assets/SpeakerPhoto/${speaker.id}/image',
              countryId: speaker.countryId,
            ),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    speaker.localizedName(isArabic),
                    textAlign: TextAlign.start,
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textLg,
                    ),
                  ),
                  if (label.isNotEmpty) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      label,
                      textAlign: TextAlign.start,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Inline-end caret (frame 908:2089) — a small beige chevron on the
            // trailing (left, in RTL) edge.
            const SimfSvgIcon(
              'assets/icons/ic_caret_left.svg',
              size: 20,
              color: SimfTokens.beigeBorder,
            ),
          ],
        ),
      ),
    );
  }
}

/// The 44×44 speaker avatar (frame 908:2004): a gold-tinted square (accent @ 15%)
/// on a solid gold hairline showing the speaker's uploaded **photo** (the D-357
/// `SpeakerPhoto` asset) clipped to the tile, falling back to the design's gold
/// **anchor** glyph while it loads or when no photo is set (the asset route
/// 204s). The speaker's **country flag** (from [countryId]) renders as a small
/// badge on the **top-left corner** (owner request); absent when the speaker has
/// no recorded country.
class _SpeakerAvatar extends StatelessWidget {
  const _SpeakerAvatar({required this.imageUrl, this.countryId});

  final String imageUrl;
  final int? countryId;

  @override
  Widget build(BuildContext context) {
    const fallback = Icon(Icons.anchor, size: 20, color: SimfTokens.accent);
    final avatar = Container(
      width: 44,
      height: 44,
      alignment: Alignment.center,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: 0.15),
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
        border: Border.all(color: SimfTokens.accent),
      ),
      child: Image.network(
        imageUrl,
        width: 44,
        height: 44,
        fit: BoxFit.cover,
        gaplessPlayback: true,
        loadingBuilder: (context, child, progress) =>
            progress == null ? child : fallback,
        errorBuilder: (context, error, stackTrace) => fallback,
      ),
    );
    // The country flag renders as a small badge on the avatar's top-left corner.
    return CountryFlagBadge(countryId: countryId, child: avatar);
  }
}
