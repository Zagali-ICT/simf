import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/feedback_repository.dart';
import 'data/rating_models.dart';

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
    final missingRequired =
        required.any((q) => (_answers[q.id] ?? 0) < 1);
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
              ? _LoadError(message: l10n.rateLoadFailed, onRetry: _loadForm)
              : _buildForm(l10n, form),
    );
  }

  Widget _buildForm(AppL10n l10n, RatingFormView form) {
    final isArabic = l10n.isArabic;
    final children = <Widget>[];

    if (form.hasOverallStars) {
      children.add(Column(
        children: <Widget>[
          Text(
            l10n.rateKicker,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textMd,
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          Text(
            l10n.rateLead,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textTitle,
              height: 1.4,
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          _StarRow(
            value: _overall,
            size: 30,
            gap: SimfTokens.space3,
            onChanged: (v) => setState(() => _overall = v),
          ),
          const SizedBox(height: SimfTokens.space5),
          if (_overall < 1)
            const SizedBox(height: 20)
          else
            Text(
              l10n.rateScoreSummary(_overall),
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textMd,
              ),
            ),
        ],
      ),);
      children.add(const SizedBox(height: SimfTokens.space5));
    }

    // Grouped questions — a section per group.
    for (final group in form.groups) {
      children.add(_SectionTitle(group.localizedName(isArabic)));
      children.add(const SizedBox(height: SimfTokens.space3));
      for (final q in group.questions) {
        children.add(_questionRow(isArabic, q));
        children.add(const SizedBox(height: SimfTokens.space4));
      }
      children.add(const SizedBox(height: SimfTokens.space3));
    }

    // Flat (ungrouped) questions — under the generic "Rate the elements" title.
    if (form.ungroupedQuestions.isNotEmpty) {
      children.add(_SectionTitle(l10n.rateElementsTitle));
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
      children.add(_SectionTitle(commentLabel));
      children.add(const SizedBox(height: SimfTokens.space2));
      children.add(TextField(
        controller: _comment,
        maxLength: 2000,
        maxLines: 4,
        minLines: 4,
        style: const TextStyle(color: Colors.white, fontSize: SimfTokens.textMd),
        decoration: InputDecoration(
          filled: true,
          fillColor: SimfTokens.navyDeep,
          hintText: l10n.rateCommentHint,
          hintStyle: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
          ),
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
    children.add(_GoldButton(
      label: l10n.rateSubmit,
      loading: _submitting,
      onTap: () => unawaited(_submit(l10n, form)),
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

  Widget _questionRow(bool isArabic, RatingFormQuestion q) => _CategoryRow(
        label: q.localizedText(isArabic),
        value: _answers[q.id] ?? 0,
        onChanged: (v) => setState(() => _answers[q.id] = v),
      );
}

/// A left-aligned white section heading (group name / "Rate the elements" /
/// comment label).
class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      textAlign: TextAlign.start,
      style: const TextStyle(
        color: Colors.white,
        fontWeight: FontWeight.w500,
        fontSize: SimfTokens.textLg,
      ),
    );
  }
}

/// Form-load failure with a retry button.
class _LoadError extends StatelessWidget {
  const _LoadError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space5),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textMd,
              ),
            ),
            const SizedBox(height: SimfTokens.space4),
            OutlinedButton(
              onPressed: onRetry,
              child: Text(AppL10n.of(context).retryLabel),
            ),
          ],
        ),
      ),
    );
  }
}

/// One per-element row: the beige-hairline 48-high box with the element name at
/// the inline start and the small star bar at the inline end.
class _CategoryRow extends StatelessWidget {
  const _CategoryRow({
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final int value;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      decoration: BoxDecoration(
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: <Widget>[
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textMd,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          _StarRow(
            value: value,
            size: 18,
            gap: SimfTokens.space1,
            onChanged: onChanged,
          ),
        ],
      ),
    );
  }
}

/// A tappable 1–5 star bar. Renders in the ambient direction so the fill grows
/// from the inline start (right under RTL — matching the Figma — left under
/// LTR). [value] 0 means unscored (all outlines). Tapping star N sets [value] N.
class _StarRow extends StatelessWidget {
  const _StarRow({
    required this.value,
    required this.onChanged,
    this.size = 24,
    this.gap = SimfTokens.space2,
  });

  final int value;
  final ValueChanged<int> onChanged;
  final double size;
  final double gap;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        for (var star = 1; star <= 5; star++) ...<Widget>[
          if (star > 1) SizedBox(width: gap),
          GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: () => onChanged(star),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: SimfTokens.space1),
              child: Icon(
                star <= value ? Icons.star_rounded : Icons.star_outline_rounded,
                size: size,
                color: star <= value
                    ? SimfTokens.accent
                    : SimfTokens.beigeBorder,
              ),
            ),
          ),
        ],
      ],
    );
  }
}

/// The full-width gold action button: radius-4 gold fill with the centred white
/// label. Stays gold while [loading] (taps disabled, a white spinner replaces the
/// label) so the button never turns into an unreadable dark box on the navy.
class _GoldButton extends StatelessWidget {
  const _GoldButton({
    required this.label,
    required this.onTap,
    this.loading = false,
  });

  final String label;
  final bool loading;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.accent,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        onTap: loading ? null : onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: SizedBox(
          height: 48,
          child: Center(
            child: loading
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : Text(
                    label,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: SimfTokens.textLg,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
          ),
        ),
      ),
    );
  }
}
