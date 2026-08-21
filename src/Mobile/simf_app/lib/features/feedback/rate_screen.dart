import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/gregorian_month_names.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/features/feedback/data/feedback_repository.dart';
import 'package:simf_app/features/feedback/data/rating_models.dart';
import 'package:simf_app/features/feedback/widgets/rate_category_row.dart';
import 'package:simf_app/features/feedback/widgets/rate_gold_button.dart';
import 'package:simf_app/features/feedback/widgets/rate_load_error.dart';
import 'package:simf_app/features/feedback/widgets/rate_navy_note_chip.dart';
import 'package:simf_app/features/feedback/widgets/star_row.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Rate — route: `RouteNames.rate` · Figma 1116:16894
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

  bool _submitting = false;

  int _overall = 0;
  // questionId → stars (0 = unscored).
  final Map<String, int> _answers = <String, int>{};

  @override
  void initState() {
    super.initState();
    unawaited(_prefillFromExisting());
  }

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  /// Default to the global "App" rating when no type was specified (the
  /// More-menu entry point).
  RatingFormKey get _key => (
        code: widget.ratingTypeId == null ? (widget.code ?? 'App') : null,
        ratingTypeId: widget.ratingTypeId,
        targetId: widget.targetId,
      );

  /// Seeds the stars, the per-question answers and the comment from any
  /// EXISTING submission, once.
  ///
  /// Awaits the provider's first future rather than listening, for the reason
  /// the old comment gave about pull-to-refresh: re-running the prefill over a
  /// part-scored form would reset the user's stars.
  Future<void> _prefillFromExisting() async {
    final RatingFormView form;
    try {
      form = await ref.read(ratingFormProvider(_key).future);
    } on Object {
      return; // The failed-load branch renders.
    }
    final existing = form.existing;
    if (!mounted || existing == null) {
      return;
    }
    setState(() {
      _overall = existing.overallStars ?? 0;
      _answers
        ..clear()
        ..addAll(existing.answers);
      if ((existing.comment ?? '').isNotEmpty) {
        _comment.text = existing.comment!;
      }
    });
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
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.rateThanks)));
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.rateFailed)));
    } finally {
      // A token refresh on the 401 path can throw a keystore PlatformException,
      // which is not an ApiFailure — clearing the flag here is what keeps the
      // submit button from stranding disabled.
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.rateTitle,
      onBack: () => backOrHome(context),
      body: ref.watch(ratingFormProvider(_key)).when(
            loading: () => const Center(
              child: CircularProgressIndicator(color: SimfTokens.accent),
            ),
            // Only the failed-load branch is pull-to-refreshable: the prefill
            // seeds _overall/_answers from any existing submission, so
            // re-running it over a part-scored form would reset the user's
            // stars.
            error: (_, __) => SimfRefreshableMessage(
              onRefresh: () =>
                  refreshAsync(ref, ratingFormProvider(_key).future),
              child: RateLoadError(
                message: l10n.rateLoadFailed,
                onRetry: () => ref.invalidate(ratingFormProvider(_key)),
              ),
            ),
            data: (form) => _buildForm(l10n, form),
          ),
    );
  }

  Widget _buildForm(AppL10n l10n, RatingFormView form) {
    final isArabic = l10n.isArabic;
    final children = <Widget>[];

    // D-713 (item 8) — the "watched at" context header on a per-session rating.
    final watchedSession = form.localizedTargetName(isArabic: isArabic);
    if (watchedSession != null) {
      children
        ..add(
          RateNavyNoteChip(
            icon: Icons.event_available_outlined,
            text: l10n.rateWatchedAt(
              watchedSession,
              _watchedWhen(isArabic, form.targetStart),
            ),
          ),
        )
        ..add(const SizedBox(height: SimfTokens.space5));
    }

    if (form.hasOverallStars) {
      children
        ..add(
          Column(
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
                size: SimfTokens.rateScreenSize,
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
          ),
        )
        ..add(const SizedBox(height: SimfTokens.space5));
    }

    for (final group in form.groups) {
      children
        ..add(
            SimfSectionHeader(title: group.localizedName(isArabic: isArabic)),)
        ..add(const SizedBox(height: SimfTokens.space3));
      for (final q in group.questions) {
        children
          ..add(_questionRow(isArabic, q))
          ..add(const SizedBox(height: SimfTokens.space4));
      }
      children.add(const SizedBox(height: SimfTokens.space3));
    }

    if (form.ungroupedQuestions.isNotEmpty) {
      children
        ..add(SimfSectionHeader(title: l10n.rateElementsTitle))
        ..add(const SizedBox(height: SimfTokens.space3));
      for (final q in form.ungroupedQuestions) {
        children
          ..add(_questionRow(isArabic, q))
          ..add(const SizedBox(height: SimfTokens.space4));
      }
    }

    if (form.allowComment) {
      final commentLabel = form.localizedCommentLabel(isArabic: isArabic) ??
          l10n.rateCommentLabel;
      children
        ..add(const SizedBox(height: SimfTokens.space5))
        ..add(SimfSectionHeader(title: commentLabel))
        ..add(const SizedBox(height: SimfTokens.space2))
        ..add(
          TextField(
            controller: _comment,
            maxLength: FieldLimits.feedbackComment,
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
          ),
        );
    }

    children.add(const SizedBox(height: SimfTokens.space5));
    // Owner 2026-07-19 — when the visitor did not attend what this rates, keep
    // the form visible but leave submit disabled (the server also 403s).
    if (!form.isEligible) {
      children
        ..add(
          RateNavyNoteChip(
            icon: Icons.info_outline,
            text: l10n.rateAttendRequired,
          ),
        )
        ..add(const SizedBox(height: SimfTokens.space3));
    }
    children.add(
      RateGoldButton(
        label: l10n.rateSubmit,
        loading: _submitting,
        onTap: form.isEligible ? () => unawaited(_submit(l10n, form)) : null,
      ),
    );

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
        label: q.localizedText(isArabic: isArabic),
        value: _answers[q.id] ?? 0,
        onChanged: (v) => setState(() => _answers[q.id] = v),
      );

  /// The "{day} {month} · {HH:MM}" watch time for the header, device-local and
  /// in the active locale (mirrors the session-header card). Empty when the
  /// session start is unknown (an older API), in which case the header shows
  /// the title alone.
  String _watchedWhen(bool isArabic, DateTime? start) {
    if (start == null) {
      return '';
    }
    final local = saudiOf(start);
    final hh = local.hour.toString().padLeft(2, '0');
    final mm = local.minute.toString().padLeft(2, '0');
    return '${local.day.toString().padLeft(2, '0')} '
        '${gregorianMonthName(local.month, isArabic: isArabic)} · $hh:$mm';
  }
}
