import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../app/localization/app_l10n.dart';
import '../../../../app/theme/tokens.dart';
import '../../../../app/widgets/simf_page_shell.dart';
import '../../../../core/responsive/max_width_body.dart';
import '../../../app/route_names.dart';
import '../data/profile_models.dart';
import '../widgets/auth_chrome.dart';
import '../widgets/interest_chip.dart';

/// The interests step's body: the chip grid, the load-error retry and the
/// submit control.
///
/// Extracted from the screen's State. None of the three methods touched
/// setState or ref - they only rendered - so they compose into one widget and
/// the screen keeps the state, the loading and the two async actions.
class SignUpInterestsBody extends StatelessWidget {
  const SignUpInterestsBody({
    required this.l10n,
    required this.interests,
    required this.selected,
    required this.loading,
    required this.loadError,
    required this.submitting,
    required this.submitError,
    required this.editMode,
    required this.draft,
    required this.onToggleInterest,
    required this.onSave,
    required this.onRetry,
    required this.onRetryEdit,
    super.key,
  });

  final AppL10n l10n;
  final List<InterestItem> interests;
  final Set<String> selected;
  final bool loading;
  final String? loadError;
  final bool submitting;
  final String? submitError;

  /// True when re-entered to EDIT saved interests: it changes the submit
  /// label and which reload the retry runs.
  final bool editMode;

  /// Null on the edit path, where there is no draft to carry forward.
  final SignUpProfileDraft? draft;

  final void Function(String id, AppL10n l10n) onToggleInterest;
  /// Saves and advances; async, so the submit control awaits it.
  final Future<void> Function() onSave;
  /// Both reload paths are async: the retry control awaits them.
  final Future<void> Function() onRetry;
  final Future<void> Function() onRetryEdit;

  @override
  Widget build(BuildContext context) {
    final l10n = this.l10n;
    if (!editMode && draft == null) {
      // Create mode, direct open with no preceding data screen — recover by
      // sending the user to the profile-data step. (Edit mode self-loads.)
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                l10n.profileLoadError,
                textAlign: TextAlign.center,
                style: const TextStyle(color: SimfTokens.txtSecondary),
              ),
              const SizedBox(height: SimfTokens.space4),
              FilledButton(
                onPressed: () => context.goNamed(RouteNames.signUpVisitor),
                child: Text(l10n.signUpVisitorTitle),
              ),
            ],
          ),
        ),
      );
    }
    if (loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (loadError != null) {
      return loadErrorView(l10n);
    }
    return Column(
      children: <Widget>[
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            child: MaxWidthBody(
              maxWidth: SimfTokens.signUpInterestsScreenMaxWidth,
              child: Column(
                children: <Widget>[
                  const SizedBox(height: SimfTokens.space6),
                  Text(
                    l10n.interestsChooseTitle,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: SimfTokens.surface,
                      fontSize: SimfTokens.text24,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space6),
                    child: Text(
                      l10n.interestsHelper,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textLg,
                        fontWeight: FontWeight.w500,
                        height: 21 / 16,
                      ),
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space8),
                  _chips(l10n),
                  const SizedBox(height: SimfTokens.space6),
                  Text(
                    l10n.interestsCounter(selected.length),
                    textAlign: TextAlign.center,
                    style: SimfTokens.labelBeigeMedium,
                  ),
                  if (submitError != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space3),
                    Text(
                      submitError!,
                      textAlign: TextAlign.center,
                      style: SimfTokens.labelDangerSm,
                    ),
                  ],
                  const SizedBox(height: SimfTokens.space6),
                ],
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(SimfTokens.space4, 0, SimfTokens.space4, SimfTokens.space6),
          child: MaxWidthBody(
            maxWidth: SimfTokens.signUpInterestsScreenMaxWidth,
            child: SizedBox(
              width: double.infinity,
              child: AuthSubmitButton(
                label: editMode ? l10n.saveLabel : l10n.continueLabel,
                busy: submitting,
                onPressed: (submitting || selected.isEmpty)
                    ? null
                    : () => unawaited(onSave()),
              ),
            ),
          ),
        ),
      ],
    );
  }

/// Only this branch is pull-to-refreshable — re-running the load on the
  /// populated grid would discard the chips the user has already picked.
  Widget loadErrorView(AppL10n l10n) {
    return SimfRefreshableMessage(
      onRefresh: () => editMode ? onRetryEdit() : onRetry(),
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                loadError!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: SimfTokens.txtSecondary),
              ),
              const SizedBox(height: SimfTokens.space4),
              FilledButton(
                onPressed: () =>
                    unawaited(editMode ? onRetryEdit() : onRetry()),
                child: Text(l10n.retryLabel),
              ),
            ],
          ),
        ),
      ),
    );
  }

/// The design's two-column pill grid (Figma 505:1222): gold when selected,
  /// `navyDeep` with a muted border otherwise.
  Widget _chips(AppL10n l10n) {
    if (interests.isEmpty) {
      return Text(
        l10n.interestsEmpty,
        style: const TextStyle(color: SimfTokens.txtSecondary),
      );
    }
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        crossAxisSpacing: 10,
        mainAxisSpacing: 12,
        mainAxisExtent: SimfTokens.signUpInterestsScreenExtent,
      ),
      itemCount: interests.length,
      itemBuilder: (context, index) {
        final interest = interests[index];
        return InterestChip(
          label: l10n.isArabic ? interest.nameArabic : interest.name,
          selected: selected.contains(interest.id),
          onTap: submitting ? null : () => onToggleInterest(interest.id, l10n),
        );
      },
    );
  }
}
