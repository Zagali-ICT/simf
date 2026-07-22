import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/gregorian_month_names.dart';
import 'data/feedback_repository.dart';
import 'data/rating_models.dart';
import 'widgets/rate_category_row.dart';
import 'widgets/rate_gold_button.dart';
import 'widgets/rate_load_error.dart';
import 'widgets/rate_navy_note_chip.dart';
import 'widgets/star_row.dart';

/// Page 040 — تقييم الملتقى · Rate (#40, `/rate`, login-only).
///
/// Dynamic, config-driven rating screen. It fetches the form for a rating type
/// (resolved by [code] — e.g. "App" / "Session" — or [ratingTypeId]) and optional
/// [targetId] (a session id for a per-session type), then renders the optional
/// overall star row, the server-defined grouped + flat questions (each a 1–5 star
/// bar) and the optional comment box, prefilled from any existing submission.
/// `GET /app/feedback/form` then `POST /app/feedback/submit`.
class RateScreen extends ConsumerStatefulWidget {
  const RateScreen({
    super.key,
    this.code,
    this.ratingTypeId,
    this.targetId,
  });

  final String? code;
  final String? ratingTypeId;
  final String? targetId;

  @override
  ConsumerState<RateScreen> createState() => _RateScreenState();
}

class _RateScreenState extends ConsumerState<RateScreen> {
  final TextEditingController _comment = TextEditingController();

  RatingFormView? _form;
  bool _loading = true;
  bool _loadFailed = false;
  bool _submitting = false;

  int _overall = 0;
  // questionId → stars (0 = unscored).
  final Map<String, int> _answers = <String, int>{};

  @override
  void initState() {
    super.initState();
    unawaited(_loadForm());
  }

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _loadForm() async {
    setState(() {
      _loading = true;
      _loadFailed = false;
    });
    try {
      // Default to the global "App" rating when no type was specified (the
      // More-menu entry point).
      final form = await ref.read(feedbackRepositoryProvider).getForm(
            code: widget.ratingTypeId == null ? (widget.code ?? 'App') : null,
            ratingTypeId: widget.ratingTypeId,
            targetId: widget.targetId,
          );
      if (!mounted) {
        return;
      }
      // Prefill from any existing submission.
      final existing = form.existing;
      if (existing != null) {
        _overall = existing.overallStars ?? 0;
        _answers
          ..clear()
          ..addAll(existing.answers);
        if ((existing.comment ?? '').isNotEmpty) {
          _comment.text = existing.comment!;
        }
      }
      setState(() {
        _form = form;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _loadFailed = true;
      });
    }
  }

  Future<void> _submit(AppL10n l10n, RatingFormView form) async {
    final messenger = ScaffoldMessenger.of(context);

    // Owner 2026-07-19 — you may only rate what you attended. The server
    // hard-gates submit with 403; stop the round-trip and explain why.
    if (!form.isEligible) {
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.rateAttendRequired)));
      return;
    }

    if (form.hasOverallStars && _overall < 1) {
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.rateStarsRequired)));
      return;
    }

    // Every required question must be scored.
    final required = <RatingFormQuestion>[
      for (final g in form.groups) ...g.questions.where((q) => q.isRequired),
      ...form.ungroupedQuestions.where((q) => q.isRequired),
    ];
    final missingRequired = required.any((q) => (_answers[q.id] ?? 0) < 1);
    if (missingRequired) {
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.rateRequiredQuestions)));
      return;
    }

    setState(() => _submitting = true);
    try {
      // Only send answered questions (stars 1–5).
      final answers = <String, int>{
        for (final e in _answers.entries)
          if (e.value >= 1) e.key: e.value,
      };
      await ref.read(feedbackRepositoryProvider).submit(
            ratingTypeId: form.ratingTypeId,
            targetId: form.targetId,
            overallStars: form.hasOverallStars ? _overall : null,
            comment: form.allowComment && _comment.text.trim().isNotEmpty
                ? _comment.text.trim()
                : null,
            answers: answers,
          );
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.rateThanks)));
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.rateFailed)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final form = _form;
    return SimfPageShell(
      title: l10n.rateTitle,
      onBack: () => backOrHome(context),
      body: _loading
          ? const Center(
              child: CircularProgressIndicator(color: SimfTokens.accent),
            )
          : _loadFailed || form == null
              ? RateLoadError(message: l10n.rateLoadFailed, onRetry: _loadForm)
              : _buildForm(l10n, form),
    );
  }

  Widget _buildForm(AppL10n l10n, RatingFormView form) {
    final isArabic = l10n.isArabic;
    final children = <Widget>[];

    // D-713 (item 8) — the "watched at" context header on a per-session rating.
    final watchedSession = form.localizedTargetName(isArabic);
    if (watchedSession != null) {
      children.add(RateNavyNoteChip(
        icon: Icons.event_available_outlined,
        text: l10n.rateWatchedAt(
          watchedSession,
          _watchedWhen(isArabic, form.targetStartUtc),
        ),
      ));
      children.add(const SizedBox(height: SimfTokens.space5));
    }

    if (form.hasOverallStars) {
      children.add(Column(
        children: <Widget>[
          Text(
            l10n.rateKicker,
            textAlign: TextAlign.center,
            style: SimfTokens.bodyBeigeMd,
          ),
          const SizedBox(height: SimfTokens.space6),
          Text(
            l10n.rateLead,
            textAlign: TextAlign.center,
            style: SimfTokens.labelWhiteBoldTitleTall,
          ),
          const SizedBox(height: SimfTokens.space6),
          StarRow(
            value: _overall,
            size: 30,
            gap: SimfTokens.space3,
            onChanged: (v) => setState(() => _overall = v),
          ),
          const SizedBox(height: SimfTokens.space5),
          if (_overall < 1)
            const SizedBox(height: SimfTokens.space5)
          else
            Text(
              l10n.rateScoreSummary(_overall),
              textAlign: TextAlign.center,
              style: SimfTokens.bodyBeigeMd,
            ),
        ],
      ),);
      children.add(const SizedBox(height: SimfTokens.space5));
    }

    // Grouped questions — a section per group.
    for (final group in form.groups) {
      children.add(SimfSectionHeader(title: group.localizedName(isArabic)));
      children.add(const SizedBox(height: SimfTokens.space3));
      for (final q in group.questions) {
        children.add(_questionRow(isArabic, q));
        children.add(const SizedBox(height: SimfTokens.space4));
      }
      children.add(const SizedBox(height: SimfTokens.space3));
    }

    // Flat (ungrouped) questions — under the generic "Rate the elements" title.
    if (form.ungroupedQuestions.isNotEmpty) {
      children.add(SimfSectionHeader(title: l10n.rateElementsTitle));
      children.add(const SizedBox(height: SimfTokens.space3));
      for (final q in form.ungroupedQuestions) {
        children.add(_questionRow(isArabic, q));
        children.add(const SizedBox(height: SimfTokens.space4));
      }
    }

    if (form.allowComment) {
      final commentLabel =
          form.localizedCommentLabel(isArabic) ?? l10n.rateCommentLabel;
      children.add(const SizedBox(height: SimfTokens.space5));
      children.add(SimfSectionHeader(title: commentLabel));
      children.add(const SizedBox(height: SimfTokens.space2));
      children.add(TextField(
        controller: _comment,
        maxLength: 2000,
        maxLines: 4,
        minLines: 4,
        style: SimfTokens.bodyWhiteMd,
        decoration: InputDecoration(
          filled: true,
          fillColor: SimfTokens.navyDeep,
          hintText: l10n.rateCommentHint,
          hintStyle: SimfTokens.labelBeigeSm,
          counterText: '',
          contentPadding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space3,
            vertical: SimfTokens.space3,
          ),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            borderSide: BorderSide.none,
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            borderSide: BorderSide.none,
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            borderSide: const BorderSide(color: SimfTokens.accent),
          ),
        ),
      ),);
    }

    children.add(const SizedBox(height: SimfTokens.space5));
    // Owner 2026-07-19 — when the visitor did not attend what this rates, keep
    // the form visible but leave submit disabled (the server also 403s).
    if (!form.isEligible) {
      children.add(
        RateNavyNoteChip(
          icon: Icons.info_outline,
          text: l10n.rateAttendRequired,
        ),
      );
      children.add(const SizedBox(height: SimfTokens.space3));
    }
    children.add(RateGoldButton(
      label: l10n.rateSubmit,
      loading: _submitting,
      onTap: form.isEligible ? () => unawaited(_submit(l10n, form)) : null,
    ),);

    return ListView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space5,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: children,
    );
  }

  Widget _questionRow(bool isArabic, RatingFormQuestion q) => RateCategoryRow(
        label: q.localizedText(isArabic),
        value: _answers[q.id] ?? 0,
        onChanged: (v) => setState(() => _answers[q.id] = v),
      );

  /// The "{day} {month} · {HH:MM}" watch time for the header, device-local and in
  /// the active locale (mirrors the session-header card). Empty when the session
  /// start is unknown (an older API), in which case the header shows the title
  /// alone.
  String _watchedWhen(bool isArabic, DateTime? startUtc) {
    if (startUtc == null) {
      return '';
    }
    final local = startUtc.toLocal();
    final hh = local.hour.toString().padLeft(2, '0');
    final mm = local.minute.toString().padLeft(2, '0');
    return '${local.day.toString().padLeft(2, '0')} '
        '${gregorianMonthName(local.month, isArabic)} · $hh:$mm';
  }
}
