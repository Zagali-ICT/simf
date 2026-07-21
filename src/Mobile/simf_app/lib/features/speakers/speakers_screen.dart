import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_search_field.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/speaker_models.dart';
import 'data/speakers_repository.dart';
import 'widgets/speaker_list_card.dart';
import 'widgets/speaker_sort_control.dart';

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
              SpeakerSortControl(
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
                      return SpeakerListCard(
                        speaker: speaker,
                        isArabic: isArabic,
                        baseUrl: baseUrl,
                        onTap: () => context.pushNamed(
                          RouteNames.speakerProfile,
                          pathParameters: <String, String>{
                            RouteParams.speakerId: speaker.id,
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
      final rankArabic = (s.rankArabic ?? '').toLowerCase();
      return name.contains(q) || rank.contains(q) || rankArabic.contains(q);
    }).toList();
    if (_alphaSorted) {
      list.sort(
        (a, b) => a.localizedName(isArabic).compareTo(b.localizedName(isArabic)),
      );
    }
    return list;
  }
}
