import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 004 — إنشاء حساب — النوع · Sign up — type (Page_004 docs).
///
/// A **client-only** account-type gate — there is **no SIMF API** on this screen
/// (Page_004_API.md). App self-registration is Visitor-only; Exhibitor and
/// Sponsor are Control-Panel concepts (D-199), shown disabled with an
/// explanatory note. Selecting **Visitor** + **Continue** forwards
/// `type = visitor` (in the query) to the sign-up form on Page 005. The screen
/// behaves identically offline — it issues no request.
class SignUpTypeScreen extends StatefulWidget {
  const SignUpTypeScreen({super.key});

  @override
  State<SignUpTypeScreen> createState() => _SignUpTypeScreenState();
}

class _SignUpTypeScreenState extends State<SignUpTypeScreen> {
  /// The only type the App can self-register (Logic V2). Forwarded to Page 005.
  static const String _visitor = 'visitor';

  String? _selected;

  bool get _canContinue => _selected == _visitor;

  void _selectVisitor() => setState(() => _selected = _visitor);

  /// Exhibitor / Sponsor are not selectable; tapping them explains why (AC3).
  void _showCpOnlyNote() {
    final l10n = AppL10n.of(context);
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(l10n.signUpCpOnlyNote)));
  }

  void _continue() {
    if (!_canContinue) {
      return;
    }
    context.pushNamed(
      RouteNames.signUpForm,
      queryParameters: <String, String>{'type': _visitor},
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.signUpTypeTitle)),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              // Field label (mockup .mfield label — نوع التسجيل / on-navy hint).
              Text(
                l10n.signUpTypeLead,
                style: const TextStyle(
                  color: SimfTokens.txtSecondary,
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: SimfTokens.space3),
              _TypeChip(
                label: l10n.signUpTypeVisitor,
                helper: l10n.signUpTypeVisitorHelper,
                selected: _selected == _visitor,
                onTap: _selectVisitor,
              ),
              const SizedBox(height: SimfTokens.space2),
              _TypeChip(
                label: l10n.signUpTypeExhibitor,
                disabled: true,
                onTap: _showCpOnlyNote,
              ),
              const SizedBox(height: SimfTokens.space2),
              _TypeChip(
                label: l10n.signUpTypeSponsor,
                disabled: true,
                onTap: _showCpOnlyNote,
              ),
              const SizedBox(height: SimfTokens.space4),
              // CP-only explainer (on-navy secondary text + accent icon).
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  const Icon(
                    Icons.info_outline,
                    size: 18,
                    color: SimfTokens.accent,
                  ),
                  const SizedBox(width: SimfTokens.space2),
                  Expanded(
                    child: Text(
                      l10n.signUpCpOnlyNote,
                      style: const TextStyle(
                        color: SimfTokens.txtTertiary,
                        fontSize: SimfTokens.textSm,
                        height: 1.6,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: SimfTokens.space6),
              FilledButton(
                onPressed: _canContinue ? _continue : null,
                child: Text(l10n.continueLabel),
              ),
              const SizedBox(height: SimfTokens.space2),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Text(
                    l10n.haveAccountQuestion,
                    style: const TextStyle(color: SimfTokens.txtSecondary),
                  ),
                  TextButton(
                    onPressed: () => context.goNamed(RouteNames.signIn),
                    child: Text(l10n.signInTitle),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A single account-type chip (mockup `.type-chip`). Bordered, radius 6, with a
/// leading radio dot. `selected` paints the accent border + accent-8% fill +
/// white text + a gold dot; `disabled` renders muted and never selects — its
/// `onTap` surfaces the CP-only note.
class _TypeChip extends StatelessWidget {
  const _TypeChip({
    required this.label,
    required this.onTap,
    this.helper,
    this.selected = false,
    this.disabled = false,
  });

  final String label;
  final String? helper;
  final bool selected;
  final bool disabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Mockup: unselected = white-10% border; selected = accent border + 8% fill.
    final Color borderColor = selected ? SimfTokens.accent : SimfTokens.line;
    final Color fillColor = selected
        ? SimfTokens.accent.withValues(alpha: 0.08)
        : Colors.transparent;
    final Color labelColor = selected
        ? SimfTokens.surface
        : SimfTokens.txtSecondary;

    return Semantics(
      selected: selected,
      enabled: !disabled,
      button: true,
      child: Opacity(
        opacity: disabled ? 0.55 : 1,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          child: Container(
            padding: const EdgeInsets.symmetric(
              horizontal: SimfTokens.space4,
              vertical: SimfTokens.space3,
            ),
            decoration: BoxDecoration(
              color: fillColor,
              border: Border.all(
                color: borderColor,
                width: selected ? 1.5 : 1,
              ),
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
            child: Row(
              children: <Widget>[
                _RadioDot(selected: selected, disabled: disabled),
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        label,
                        style: TextStyle(
                          color: labelColor,
                          fontSize: SimfTokens.textMd,
                          fontWeight: selected
                              ? FontWeight.w600
                              : FontWeight.w500,
                        ),
                      ),
                      if (helper != null) ...<Widget>[
                        const SizedBox(height: SimfTokens.space1),
                        Text(
                          helper!,
                          style: const TextStyle(
                            color: SimfTokens.txtTertiary,
                            fontSize: SimfTokens.textSm,
                            height: 1.5,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// The mockup `.type-chip::before` radio dot: a 14px ring that fills with the
/// accent when the chip is selected.
class _RadioDot extends StatelessWidget {
  const _RadioDot({required this.selected, required this.disabled});

  final bool selected;
  final bool disabled;

  @override
  Widget build(BuildContext context) {
    final Color ringColor = selected
        ? SimfTokens.accent
        : SimfTokens.txtTertiary;
    return Container(
      width: 16,
      height: 16,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(color: ringColor, width: 1.5),
      ),
      alignment: Alignment.center,
      child: selected
          ? Container(
              width: 8,
              height: 8,
              decoration: const BoxDecoration(
                shape: BoxShape.circle,
                color: SimfTokens.accent,
              ),
            )
          : null,
    );
  }
}
