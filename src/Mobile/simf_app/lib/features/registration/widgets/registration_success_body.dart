import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/registration/widgets/contact_us_section.dart';
import 'package:simf_app/features/registration/widgets/reference_number_card.dart';
import 'package:simf_app/features/registration/widgets/registration_success_actions.dart';
import 'package:simf_app/features/registration/widgets/registration_success_mark.dart';

/// The fallback mask when no real reference is available (offline arrival or a
/// pre-D-373 save). D-373 superseded the D-366 always-masked rule: the save
/// response now carries the DB-issued `SIMF-YYYY-NNNNNNNN` reference and the
/// interests screen passes it here via the route extra.
const String _maskedReference = 'SIMF-2026-xxxx';

/// The scrollable confirmation body (Figma 505:1451): the green success mark,
/// the headline + welcome copy, the reference-number card, the status / home
/// actions, and the contact-us block — capped at 400px and centred.
class RegistrationSuccessBody extends StatelessWidget {
  const RegistrationSuccessBody({
    required this.welcomeMessage,
    this.referenceNumber,
    super.key,
  });

  final String welcomeMessage;

  /// The `SIMF-YYYY-NNNNNNNN` reference issued by the save (D-373); null on an
  /// offline / out-of-flow arrival → the mask renders.
  final String? referenceNumber;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: SimfTokens.registrationSuccessBodyMaxWidth),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: SimfTokens.space6),
              const RegistrationSuccessMark(),
              const SizedBox(height: SimfTokens.space4),
              Text(
                l10n.registrationSuccessTitle,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: SimfTokens.text24,
                  fontWeight: FontWeight.w700,
                  color: SimfTokens.surface,
                ),
              ),
              const SizedBox(height: SimfTokens.space4),
              Text(
                welcomeMessage,
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeige,
              ),
              const SizedBox(height: SimfTokens.space6),
              ReferenceNumberCard(
                label: l10n.referenceNumberLabel,
                reference: referenceNumber ?? _maskedReference,
              ),
              const SizedBox(height: SimfTokens.space8),
              RegistrationSuccessActions(
                statusLabel: l10n.registrationStatusButton,
                homeLabel: l10n.goHomeButton,
                onStatus: () =>
                    context.goNamed(RouteNames.registrationStatus),
                onHome: () => context.go('/'),
              ),
              const SizedBox(height: SimfTokens.space8),
              ContactUsSection(
                title: l10n.contactUsTitle,
                socialFooter: l10n.simfSocialFooter,
              ),
              const SizedBox(height: SimfTokens.space6),
            ],
          ),
        ),
      ),
    );
  }
}
