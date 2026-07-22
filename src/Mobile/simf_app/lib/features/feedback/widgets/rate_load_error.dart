import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// Form-load failure with a retry button.
class RateLoadError extends StatelessWidget {
  const RateLoadError({required this.message, required this.onRetry, super.key});

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
              style: SimfTokens.bodyWhiteMd,
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
