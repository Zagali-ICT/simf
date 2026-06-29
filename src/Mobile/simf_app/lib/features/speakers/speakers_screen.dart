import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/country_flag.dart';
import '../../app/widgets/simf_search_field.dart';
import '../../app/widgets/simf_page_shell.dart';
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
/// `#192B41` card on the beige `0.2px` hairline (the shared [SimfCard]) carrying,
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
  // Frame 908:1744 — client-side search + alphabetical sort over the loaded
  // list. Default preserves the API's curated order; the sort control toggles
  // an A→Z alphabetical sort.
  String _query = '';
  bool _alphaSorted = false;

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
    // DRY (owner 2026-06-28): the shared SimfPageShell standard nav now renders the
    // Figma sub-page header (forced-LTR back-left + centred title + hairline),
    // so the old per-screen header is gone.
    return SimfPageShell(
      title: l10n.speakersTitle,
      onBack: () => backOrHome(context),
      body: _buildBody(l10n),
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
      return SimfPullToRefresh(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            SimfErrorState(
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
      return SimfPullToRefresh(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            SimfEmptyState(
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
    final visible = _visibleSpeakers(isArabic);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // Frame 908:1744 — the search box (start/right) + the sort control
        // (end/left). Width-flexible: the search Expands to fill the remaining
        // width on any screen (owner responsive + DRY 2026-06-28).
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space2,
          ),
          child: Row(
            children: <Widget>[
              Expanded(
                child: SimfSearchField(
                  hint: l10n.speakersSearchHint,
                  onChanged: (v) => setState(() => _query = v),
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              _SortControl(
                label: l10n.speakersSortAlpha,
                selected: _alphaSorted,
                onTap: () => setState(() => _alphaSorted = !_alphaSorted),
              ),
            ],
          ),
        ),
        Expanded(
          child: SimfPullToRefresh(
            onRefresh: _load,
            child: visible.isEmpty
                ? ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    children: <Widget>[
                      SimfEmptyState(
                        icon: Icons.search_off_outlined,
                        message: l10n.speakersNoMatches,
                      ),
                    ],
                  )
                : ListView.separated(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(
                      SimfTokens.space4,
                      SimfTokens.space2,
                      SimfTokens.space4,
                      SimfTokens.space6,
                    ),
                    itemCount: visible.length,
                    // Frame 908:1744 — cards pitch 76px (card 60 + 16 gap).
                    separatorBuilder: (_, __) =>
                        const SizedBox(height: SimfTokens.space4),
                    itemBuilder: (context, index) {
                      final speaker = visible[index];
                      return _SpeakerCard(
                        speaker: speaker,
                        isArabic: isArabic,
                        baseUrl: baseUrl,
                        onTap: () => context.pushNamed(
                          RouteNames.speakerProfile,
                          pathParameters: <String, String>{
                            'speakerId': speaker.id,
                          },
                        ),
                      );
                    },
                  ),
          ),
        ),
      ],
    );
  }

  /// The loaded speakers after the search query + alphabetical sort (908:1744).
  List<SpeakerSummary> _visibleSpeakers(bool isArabic) {
    final q = _query.trim().toLowerCase();
    final list = _speakers.where((s) {
      if (q.isEmpty) {
        return true;
      }
      final name = s.localizedName(isArabic).toLowerCase();
      final rank = (s.rank ?? '').toLowerCase();
      return name.contains(q) || rank.contains(q);
    }).toList();
    if (_alphaSorted) {
      list.sort(
        (a, b) => a.localizedName(isArabic).compareTo(b.localizedName(isArabic)),
      );
    }
    return list;
  }
}

/// The frame's sort control (908:1744) — a navy rounded box with a sort glyph,
/// the "ترتيب حسب الابجدية" label and a direction chevron; tapping flips the
/// alphabetical order of the list.
class _SortControl extends StatelessWidget {
  const _SortControl({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;

  /// Whether the alphabetical sort is currently applied (gold-highlighted).
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tint = selected ? SimfTokens.accent : SimfTokens.beigeBorder;
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          height: 48,
          // Frame 1341:3583 — 8px horizontal padding, 0.2px beige hairline.
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            border: Border.all(
              color: tint,
              width: selected ? 1 : SimfTokens.hairline,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Icon(Icons.swap_vert, size: 18, color: tint),
              const SizedBox(width: SimfTokens.space2),
              Text(
                label,
                maxLines: 1,
                // Frame 1341:3582 — Inter Medium 12px, beige (#C2B8A2).
                style: TextStyle(
                  color: tint,
                  fontWeight: FontWeight.w500,
                  fontSize: SimfTokens.textSm,
                ),
              ),
              const SizedBox(width: SimfTokens.space1),
              Icon(Icons.keyboard_arrow_down, size: 18, color: tint),
            ],
          ),
        ),
      ),
    );
  }
}

/// One speaker card (frame 908:1999): the navy [SimfCard] chrome carrying — in
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
    // Figma 908:1744 (node 1318:3391): the country flag is an inline glyph at the
    // trailing (left, in RTL) edge of the name — NOT a badge on the avatar — and
    // the sub-line carries only the rank.
    final flag = countryFlagEmoji(speaker.countryId);
    final label = (speaker.rank != null && speaker.rank!.trim().isNotEmpty)
        ? speaker.rank!.trim()
        : '';

    return SimfCard(
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
            ),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  // Name with the country flag inline at its trailing (left, in
                  // RTL) edge — Figma node 1318:3391 (flag left of the name, an
                  // 8px gap), right-aligned in the column.
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Flexible(
                        child: Text(
                          speaker.localizedName(isArabic),
                          textAlign: TextAlign.start,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: Colors.white,
                            fontWeight: FontWeight.w600,
                            fontSize: SimfTokens.textLg,
                          ),
                        ),
                      ),
                      if (flag != null) ...<Widget>[
                        const SizedBox(width: SimfTokens.space2),
                        Text(
                          flag,
                          textDirection: TextDirection.ltr,
                          // Frame 1318:3392 — the flag glyph is 12px.
                          style: const TextStyle(
                            fontSize: SimfTokens.textSm,
                            height: 1,
                          ),
                        ),
                      ],
                    ],
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
            // Inline-end caret (frame 908:2089) — the iconamoon thin chevron in
            // GOLD on the trailing (left, in RTL) edge. NOT the beige filled
            // triangle (ic_caret_left): the frame draws a stroked chevron.
            const SimfSvgIcon(
              'assets/icons/ic_back.svg',
              size: 20,
              color: SimfTokens.accent,
            ),
          ],
        ),
      ),
    );
  }
}

/// The 44×44 speaker avatar (frame 908:2004): a **rounded-square (4px)** navy
/// tile on a 0.2px beige hairline showing the speaker's uploaded **photo** (the
/// D-357 `SpeakerPhoto` asset) clipped to the same 4px rounding, falling back to
/// the design's gold **anchor** glyph while it loads or when no photo is set (the
/// asset route 204s). Per Figma 908:1744 the **country flag is inline beside the
/// name** (see _SpeakerCard), not a badge on the avatar — so this tile is a clean
/// photo.
class _SpeakerAvatar extends StatelessWidget {
  const _SpeakerAvatar({required this.imageUrl});

  final String imageUrl;

  @override
  Widget build(BuildContext context) {
    const fallback = Icon(Icons.anchor, size: 20, color: SimfTokens.accent);
    return Container(
      width: 44,
      height: 44,
      alignment: Alignment.center,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        // Frame 908:2004 — rounded-square (4px) navy tile with a 0.2px beige
        // hairline (no gold fill); the photo covers it (clipped to the same 4px
        // rounding), the gold anchor is the fallback glyph.
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
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
  }
}
