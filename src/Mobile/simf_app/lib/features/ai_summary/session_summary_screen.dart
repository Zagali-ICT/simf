import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../sessions/data/session_models.dart';
import '../sessions/data/sessions_repository.dart';
import '../sessions/widgets/session_filter_tabs.dart';
import 'data/session_summary_models.dart';
import 'data/session_summary_repository.dart';
import 'widgets/summary_content_card.dart';
import 'widgets/summary_generate_card.dart';
import 'widgets/summary_session_card.dart';
import 'widgets/summary_video_card.dart';

/// The three summary tabs (Figma 1072:14647), in RTL display order
/// (right→left): أبرز النقاط · التوصيات · المتحدثون. أبرز النقاط is the default.
enum _SummaryTab { keyPoints, recommendations, speakers }

/// Page 034 — ملخص الجلسة · Session summary (#34, `/ai-summary?sessionId=`),
/// rebuilt to the KSA-Project Figma frame **1072:13518**.
///
/// **Public** (Guest+, `AllowAnonymous`). Reached with a `sessionId` from the
/// summaries list (#111) or the session-detail "ملخص الجلسة" button; with no id
/// it falls back to the first programme session. Top-to-bottom: the **"الجلسة"
/// info card** (the selected session's gold title + day·time·duration·hall over a
/// **day-agenda timeline** — that day's sessions, reused from the cached
/// programme, no new API), a **3-tab segmented control** (المتحدثون / أبرز النقاط
/// / التوصيات), a **tab-content card** rendering the active section as gold-dot
/// bullets, and a **"توليد ملخص للجلسة"** card whose gold button expands /
/// collapses the published AI summary paragraph.
///
/// Reads the published summary (`GET /app/programme/sessions/{id}/summary`); a
/// 404 = no published summary yet (the tabs + paragraph show the empty note). The
/// summary is **Committee-generated** in the Control Panel (D-237/D-472) — this
/// screen is a read-only consumer, so the gold button reveals the already-published
/// text rather than triggering generation.
class AiSummaryScreen extends ConsumerStatefulWidget {
  const AiSummaryScreen({this.sessionId, super.key});

  final String? sessionId;

  @override
  ConsumerState<AiSummaryScreen> createState() => _AiSummaryScreenState();
}

class _AiSummaryScreenState extends ConsumerState<AiSummaryScreen> {
  String? _selectedId;
  SessionListItem? _selectedSession;
  bool _loading = false;
  bool _error = false;
  SessionSummary? _summary;
  _SummaryTab _tab = _SummaryTab.keyPoints;
  bool _summaryExpanded = true;

  @override
  void initState() {
    super.initState();
    final id = widget.sessionId?.trim();
    if (id != null && id.isNotEmpty) {
      _selectedId = id;
      unawaited(_load());
    }
  }

  Future<void> _load() async {
    final id = _selectedId;
    if (id == null) {
      return;
    }
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final summary =
          await ref.read(sessionSummaryRepositoryProvider).getSummary(id);
      if (!mounted) {
        return;
      }
      setState(() {
        _summary = summary;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      // A 404 = no published summary yet → _summary stays null and the tabs +
      // paragraph show the empty note; any other failure → error + retry.
      setState(() {
        _loading = false;
        _error = failure.httpStatus != 404;
      });
    }
  }

  /// Resolve the selected session (the passed id or the first) + its metadata,
  /// firing the summary load once. Runs after the programme list resolves.
  void _ensureSelection(List<SessionListItem> sessions) {
    if (sessions.isEmpty || (_selectedId != null && _selectedSession != null)) {
      return;
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      if (_selectedId == null) {
        setState(() {
          _selectedSession = sessions.first;
          _selectedId = sessions.first.id;
        });
        unawaited(_load());
      } else if (_selectedSession == null) {
        final match = sessions.where((s) => s.id == _selectedId);
        setState(() {
          _selectedSession = match.isNotEmpty ? match.first : sessions.first;
        });
      }
    });
  }

  /// That day's sessions (the agenda timeline) — the cached programme filtered to
  /// the selected session's local calendar day, time-ordered.
  List<SessionListItem> _dayAgenda(List<SessionListItem> all) {
    final selected = _selectedSession;
    if (selected == null) {
      return const <SessionListItem>[];
    }
    final day = selected.startLocal;
    final rows = all
        .where((s) => s.id != selected.id && sameLocalDay(s.startLocal, day))
        .toList()
      ..sort((a, b) => a.startUtc.compareTo(b.startUtc));
    return rows;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final sessions = ref.watch(programmeSessionsProvider);
    return SimfPageShell(
      title: l10n.aiSummaryTitle,
      onBack: () => backOrHome(context),
      body: sessions.when(
        loading: () => const SimfLoadingState(),
        error: (_, __) => SimfEmptyState(
          icon: Icons.event_busy_outlined,
          message: l10n.aiSummaryNoSessions,
        ),
        data: (list) {
          if (list.isEmpty) {
            return SimfEmptyState(
              icon: Icons.event_busy_outlined,
              message: l10n.aiSummaryNoSessions,
            );
          }
          _ensureSelection(list);
          return _body(l10n, list);
        },
      ),
    );
  }

  Widget _body(AppL10n l10n, List<SessionListItem> list) {
    final isArabic = l10n.isArabic;
    final selected = _selectedSession;
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        if (selected != null)
          SummarySessionCard(
            label: l10n.aiSummarySessionLabel,
            session: selected,
            agenda: _dayAgenda(list),
            isArabic: isArabic,
            durationLabel: l10n.sessionDurationMinutes(
              selected.endLocal.difference(selected.startLocal).inMinutes,
            ),
          ),
        const SizedBox(height: SimfTokens.space4),
        // Item #35 — the two labeled video players: the session's FULL live
        // recording and the team's short summary video. Each is present only when
        // its URL is set (both come from the published summary); when neither is
        // set this contributes nothing, leaving the layout unchanged.
        ..._videoPlayers(l10n),
        SessionFilterTabs(
          labels: <String>[
            l10n.aiSummaryKeyPointsHeading,
            l10n.aiSummaryRecommendationsHeading,
            l10n.aiSummarySpeakersHeading,
          ],
          selectedIndex: _SummaryTab.values.indexOf(_tab),
          onSelected: (i) => setState(() => _tab = _SummaryTab.values[i]),
          equalWidth: true,
        ),
        const SizedBox(height: SimfTokens.space4),
        SummaryTabContentCard(
          heading: _activeLabel(l10n),
          child: _tabBody(l10n, isArabic),
        ),
        const SizedBox(height: SimfTokens.space4),
        SummaryGenerateCard(
          label: l10n.aiSummaryGenerateButton,
          expanded: _summaryExpanded,
          onToggle: () => setState(() => _summaryExpanded = !_summaryExpanded),
          paragraph: _summaryParagraph(l10n, isArabic),
        ),
      ],
    );
  }

  /// Item #35 — the labeled video players for the published summary: the full
  /// live recording (from `recordingUrl`) then the team's short summary video
  /// (from `summaryVideoUrl`). Each is added only when its URL is non-empty, so
  /// a session with neither contributes no widgets (no layout shift). Each is
  /// followed by a spacer so it sits above the tabs like every other block.
  List<Widget> _videoPlayers(AppL10n l10n) {
    final summary = _summary;
    if (summary == null) {
      return const <Widget>[];
    }
    final players = <Widget>[];
    final recording = summary.recordingUrl?.trim();
    if (recording != null && recording.isNotEmpty) {
      players
        ..add(
          SummaryVideoCard(label: l10n.aiSummaryRecordingLabel, url: recording),
        )
        ..add(const SizedBox(height: SimfTokens.space4));
    }
    final video = summary.summaryVideoUrl?.trim();
    if (video != null && video.isNotEmpty) {
      players
        ..add(SummaryVideoCard(label: l10n.aiSummaryVideoLabel, url: video))
        ..add(const SizedBox(height: SimfTokens.space4));
    }
    return players;
  }

  String _activeLabel(AppL10n l10n) => switch (_tab) {
        _SummaryTab.keyPoints => l10n.aiSummaryKeyPointsHeading,
        _SummaryTab.recommendations => l10n.aiSummaryRecommendationsHeading,
        _SummaryTab.speakers => l10n.aiSummarySpeakersHeading,
      };

  /// The active tab's content — bullets, the empty note, a loader, or retry.
  Widget _tabBody(AppL10n l10n, bool isArabic) {
    if (_loading) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: SimfTokens.space4),
        child: Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
      );
    }
    if (_error) {
      return SimfErrorState(
        message: l10n.aiSummaryError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    final summary = _summary;
    final block = summary == null
        ? ''
        : switch (_tab) {
            _SummaryTab.keyPoints => summary.localizedKeyPoints(isArabic),
            _SummaryTab.recommendations =>
              summary.localizedRecommendations(isArabic),
            _SummaryTab.speakers => summary.localizedSpeakers(isArabic),
          };
    final lines = block
        .split('\n')
        .map((l) => l.trim())
        .where((l) => l.isNotEmpty)
        .toList(growable: false);
    if (lines.isEmpty) {
      return Text(
        l10n.aiSummaryNone,
        textAlign: TextAlign.start,
        style: SimfTokens.bodyBeige,
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        for (var i = 0; i < lines.length; i++) ...<Widget>[
          if (i != 0) const SizedBox(height: SimfTokens.space3),
          SummaryBullet(text: lines[i]),
        ],
      ],
    );
  }

  /// The published full-text paragraph (or the empty note) under the generate
  /// button.
  String _summaryParagraph(AppL10n l10n, bool isArabic) {
    final text = _summary?.localizedFullText(isArabic).trim() ?? '';
    return text.isEmpty ? l10n.aiSummaryNone : text;
  }
}
