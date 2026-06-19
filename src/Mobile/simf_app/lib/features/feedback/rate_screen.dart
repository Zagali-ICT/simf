import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';

/// Data layer for the rating (Page_040). `POST /app/feedback/rate`
/// (`RequireApprovedAccount`) upserts the caller's overall star rating, the four
/// optional per-element scores (D-463, Figma "قيّم العناصر") and the comment.
class FeedbackRepository {
  FeedbackRepository(this._client);

  final SimfApiClient _client;

  Future<void> submitRating({
    required int stars,
    String? comment,
    int? organizationStars,
    int? contentStars,
    int? appStars,
    int? venueStars,
  }) {
    return _client.post<bool>(
      '/app/feedback/rate',
      body: <String, dynamic>{
        'stars': stars,
        'comment': comment,
        // Appended D-463 fields — omitted-as-null when the element is unscored.
        'organizationStars': organizationStars,
        'contentStars': contentStars,
        'appStars': appStars,
        'venueStars': venueStars,
      },
      decodeData: (_) => true,
    );
  }
}

final feedbackRepositoryProvider = Provider<FeedbackRepository>((ref) {
  return FeedbackRepository(ref.watch(simfApiClientProvider));
});

/// Page 040 — تقييم الملتقى · Rate (#40, `/rate`, Visitor login-only).
///
/// Pixel-parity to KSA Figma frame `1116:16894`: on the navy [KsaPage] shell,
/// a centred lead ("شارك تجربتك" → the question → the overall star row → the
/// "{n} من 5 · {word}" summary), the "قيّم العناصر" block of four per-element
/// star rows (organisation / content / app / venue), the "ملاحظاتك" notes box
/// and the gold "إرسال التقييم" button. The overall stars are required; the four
/// element scores and the comment are optional. `POST /app/feedback/rate`; a
/// 401/403 maps to a toast.
class RateScreen extends ConsumerStatefulWidget {
  const RateScreen({super.key});

  @override
  ConsumerState<RateScreen> createState() => _RateScreenState();
}

class _RateScreenState extends ConsumerState<RateScreen> {
  final TextEditingController _comment = TextEditingController();
  int _stars = 0;
  int _organization = 0;
  int _content = 0;
  int _app = 0;
  int _venue = 0;
  bool _submitting = false;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  /// 0 (unscored) maps to null on the wire; 1–5 passes through.
  int? _orNull(int value) => value < 1 ? null : value;

  Future<void> _submit(AppL10n l10n) async {
    if (_stars < 1) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.rateStarsRequired)));
      return;
    }
    setState(() => _submitting = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(feedbackRepositoryProvider).submitRating(
            stars: _stars,
            comment: _comment.text.trim().isEmpty ? null : _comment.text.trim(),
            organizationStars: _orNull(_organization),
            contentStars: _orNull(_content),
            appStars: _orNull(_app),
            venueStars: _orNull(_venue),
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
    return KsaPage(
      title: l10n.rateTitle,
      onBack: () => ksaBackOrHome(context),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space5,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        children: <Widget>[
          // Centred lead: kicker, question, the overall star row and the
          // dynamic "{n} من 5 · {word}" summary (frame 1116:17144).
          Column(
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
                value: _stars,
                size: 30,
                gap: SimfTokens.space3,
                onChanged: (v) => setState(() => _stars = v),
              ),
              const SizedBox(height: SimfTokens.space5),
              // Summary shows only once an overall score is picked; a same-height
              // spacer holds the slot otherwise. Sizes naturally so it never
              // clips under the accessibility text scaler.
              if (_stars < 1)
                const SizedBox(height: 20)
              else
                Text(
                  l10n.rateScoreSummary(_stars),
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textMd,
                  ),
                ),
            ],
          ),
          const SizedBox(height: SimfTokens.space5),
          // "قيّم العناصر" — the four per-element rows (frames 1116:17145…17211).
          Text(
            l10n.rateElementsTitle,
            textAlign: TextAlign.start,
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w500,
              fontSize: SimfTokens.textLg,
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          _CategoryRow(
            label: l10n.rateCatOrganization,
            value: _organization,
            onChanged: (v) => setState(() => _organization = v),
          ),
          const SizedBox(height: SimfTokens.space4),
          _CategoryRow(
            label: l10n.rateCatContent,
            value: _content,
            onChanged: (v) => setState(() => _content = v),
          ),
          const SizedBox(height: SimfTokens.space4),
          _CategoryRow(
            label: l10n.rateCatApp,
            value: _app,
            onChanged: (v) => setState(() => _app = v),
          ),
          const SizedBox(height: SimfTokens.space4),
          _CategoryRow(
            label: l10n.rateCatVenue,
            value: _venue,
            onChanged: (v) => setState(() => _venue = v),
          ),
          const SizedBox(height: SimfTokens.space6),
          // "ملاحظاتك" notes box (frame 1116:17216).
          Text(
            l10n.rateCommentLabel,
            textAlign: TextAlign.start,
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w500,
              fontSize: SimfTokens.textLg,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          TextField(
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
          ),
          const SizedBox(height: SimfTokens.space5),
          // Gold "إرسال التقييم" button (frame 1116:17220).
          _GoldButton(
            label: _submitting ? l10n.loadingLabel : l10n.rateSubmit,
            onTap: _submitting ? null : () => unawaited(_submit(l10n)),
          ),
        ],
      ),
    );
  }
}

/// One per-element row (frames 1116:17145…): the beige-hairline 48-high box with
/// the element name at the inline start and the small star bar at the inline end.
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

/// The full-width gold action button (frame 1116:17220): radius-4 gold fill with
/// the centred white label. [onTap] null renders the disabled (submitting) state.
class _GoldButton extends StatelessWidget {
  const _GoldButton({required this.label, required this.onTap});

  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;
    return Material(
      color: enabled ? SimfTokens.accent : SimfTokens.navyDisabled,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: SizedBox(
          height: 48,
          child: Center(
            child: Text(
              label,
              style: TextStyle(
                color: enabled ? Colors.white : SimfTokens.navyDisabledText,
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
