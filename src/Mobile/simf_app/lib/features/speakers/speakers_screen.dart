import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
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
/// in RTL: a small beige caret at the inline start, the white name (16/SemiBold)
/// over the beige rank·affiliation line (12/Regular), and a 44×44 gold-bordered
/// tile holding an anchor glyph (speaker) or a star glyph (host · المضيف).
///
/// Behaviour is unchanged from the mockup build — the avatar is rendered as the
/// role tile and the country as text (the flag/photo asset pass is SIMF-VID-001).
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
      return KsaErrorState(
        message: l10n.speakersError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    if (_speakers.isEmpty) {
      return KsaEmptyState(
        icon: Icons.groups_outlined,
        message: l10n.speakersEmpty,
      );
    }
    final isArabic = l10n.isArabic;
    final hostLabel = l10n.hostLabel;
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space4,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      itemCount: _speakers.length,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space5),
      itemBuilder: (context, index) {
        final speaker = _speakers[index];
        return _SpeakerCard(
          speaker: speaker,
          isArabic: isArabic,
          hostLabel: hostLabel,
          onTap: () => context.pushNamed(
            RouteNames.speakerProfile,
            pathParameters: <String, String>{'speakerId': speaker.id},
          ),
        );
      },
    );
  }
}

/// One speaker card (frame 908:1999): the navy [KsaCard] chrome carrying — in
/// RTL — a small beige caret at the inline start, the white name over the beige
/// rank·affiliation line, and a 44×44 gold-bordered role tile at the inline end
/// (an anchor for a speaker, a star for a host · المضيف).
class _SpeakerCard extends StatelessWidget {
  const _SpeakerCard({
    required this.speaker,
    required this.isArabic,
    required this.hostLabel,
    required this.onTap,
  });

  final SpeakerSummary speaker;
  final bool isArabic;

  /// The localized "host" word (المضيف / Host) — used both to detect a host row
  /// and to render the star tile.
  final String hostLabel;
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
    final label = labelParts.join(' · ');
    // No role flag exists on the public summary (data gap — see report), so the
    // host tile is driven by the affiliation text carrying the host word, the
    // only available signal (the frame's host row reads "العميد ركن · المضيف").
    final isHost = label.contains(hostLabel);

    return KsaCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            // Inline-start caret (frame 908:2089) — a small beige left chevron;
            // the bundled SVG does not auto-mirror under RTL, so it always
            // points toward the inline-leading edge as the frame shows.
            const SimfSvgIcon(
              'assets/icons/ic_caret_left.svg',
              size: 20,
              color: SimfTokens.beigeBorder,
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: <Widget>[
                  Flexible(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Text(
                          speaker.localizedName(isArabic),
                          textAlign: TextAlign.end,
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
                            textAlign: TextAlign.end,
                            style: const TextStyle(
                              color: SimfTokens.beigeBorder,
                              fontSize: SimfTokens.textSm,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(width: SimfTokens.space4),
                  _RoleTile(isHost: isHost),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The 44×44 gold-bordered role tile (frame 908:2004): a gold-tinted square
/// (accent @ 15%) on a solid gold hairline, holding the anchor glyph for a
/// speaker or the star glyph for a host.
class _RoleTile extends StatelessWidget {
  const _RoleTile({required this.isHost});

  final bool isHost;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 44,
      height: 44,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: 0.15),
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
        border: Border.all(color: SimfTokens.accent),
      ),
      child: Icon(
        isHost ? Icons.star_border : Icons.anchor,
        size: 20,
        color: SimfTokens.accent,
      ),
    );
  }
}
