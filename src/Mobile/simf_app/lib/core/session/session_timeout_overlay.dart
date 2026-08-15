import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// D-726 (owner item 11) — the idle session-timeout warning the session guard
/// paints as an in-tree overlay (not a Navigator dialog, so it needs no router
/// navigator key): a dimmed barrier over the whole app with a centred card
/// showing the countdown and the "stay signed in" / "sign out" actions.
///
/// Stateless — the guard owns the countdown and passes [secondsLeft] down; the
/// barrier absorbs background taps so only the two actions resolve it.
class SessionTimeoutOverlay extends StatelessWidget {
  const SessionTimeoutOverlay({
    required this.secondsLeft,
    required this.onStaySignedIn,
    required this.onSignOut,
    super.key,
  });

  final int secondsLeft;
  final VoidCallback onStaySignedIn;
  final VoidCallback onSignOut;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // A full-screen modal barrier: BlockSemantics drops the background screen
    // from the a11y tree (a screen reader can't reach the controls behind the
    // countdown); the scrim + opaque GestureDetector eat pointer events so a
    // tap outside the card never dismisses the warning or counts as activity.
    return Positioned.fill(
      child: BlockSemantics(
        child: ColoredBox(
          color: SimfTokens.scrimBlack55,
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: () {},
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space4),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(
                      maxWidth: SimfTokens.sessionTimeoutOverlayMaxWidth,),
                  child: Material(
                    color: SimfTokens.navyDeep,
                    borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
                    child: Padding(
                      padding: const EdgeInsets.all(SimfTokens.space5),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: <Widget>[
                          Text(
                            l10n.sessionExpiryTitle,
                            style: SimfTokens.labelWhiteMedium,
                          ),
                          const SizedBox(height: SimfTokens.space3),
                          Text(
                            l10n.sessionExpiryCountdown(secondsLeft),
                            style: SimfTokens.bodyBeige,
                          ),
                          const SizedBox(height: SimfTokens.space5),
                          // Both actions on one row (owner 2026-07-11, D-747),
                          // matching the SimfConfirmDialog convention (D-565):
                          // the secondary "sign out" at the inline start, the
                          // primary "stay signed in" at the inline end, split
                          // 50/50 so the two never stack.
                          Row(
                            children: <Widget>[
                              Expanded(
                                child: TextButton(
                                  onPressed: onSignOut,
                                  style: TextButton.styleFrom(
                                    foregroundColor: SimfTokens.beigeBorder,
                                    minimumSize: const Size.fromHeight(
                                        SimfTokens.buttonHeight,),
                                  ),
                                  child: Text(l10n.signOutLink),
                                ),
                              ),
                              const SizedBox(width: SimfTokens.space3),
                              Expanded(
                                child: FilledButton(
                                  onPressed: onStaySignedIn,
                                  style: FilledButton.styleFrom(
                                    minimumSize: const Size.fromHeight(
                                        SimfTokens.buttonHeight,),
                                  ),
                                  child: Text(l10n.sessionStaySignedIn),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
