import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/widgets/contact_card.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The resolved-card preview + save sheet. Holds the optional note and the save
/// call; pops `true` on a successful save, surfacing the self-save 400 inline.
class ContactPreviewSheet extends ConsumerStatefulWidget {
  const ContactPreviewSheet(
      {required this.token, required this.card, super.key,});

  final String token;
  final VisitorCard card;

  @override
  ConsumerState<ContactPreviewSheet> createState() =>
      _ContactPreviewSheetState();
}

class _ContactPreviewSheetState extends ConsumerState<ContactPreviewSheet> {
  final TextEditingController _noteController = TextEditingController();
  bool _saving = false;

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final l10n = AppL10n.of(context);
    setState(() => _saving = true);
    try {
      await ref
          .read(contactsRepositoryProvider)
          .save(widget.token, _noteController.text);
      if (!mounted) {
        return;
      }
      Navigator.of(context).pop(true);
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      setState(() => _saving = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            e.httpStatus == 400 ? l10n.saveContactSelf : l10n.saveContactError,
          ),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final card = widget.card;
    return Padding(
      padding: EdgeInsets.only(
        left: SimfTokens.space4,
        right: SimfTokens.space4,
        top: SimfTokens.space4,
        bottom: MediaQuery.of(context).viewInsets.bottom + SimfTokens.space4,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              l10n.contactPreviewTitle,
              style: SimfTokens.titleBold,
            ),
            const SizedBox(height: SimfTokens.space3),
            ContactCard(
              name: card.localizedName(isArabic: isArabic),
              available: card.available,
              jobTitle: card.localizedJobTitle(isArabic: isArabic),
              organisation: card.localizedOrganisation(isArabic: isArabic),
              country: card.localizedCountry(isArabic: isArabic),
              email: card.email,
              saudiMobile: card.saudiMobile,
              internationalMobile: card.internationalMobile,
            ),
            if (card.available) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              TextField(
                controller: _noteController,
                decoration: InputDecoration(
                  labelText: l10n.saveContactNoteHint,
                  border: const OutlineInputBorder(),
                ),
                maxLength: FieldLimits.contactNote,
              ),
              const SizedBox(height: SimfTokens.space2),
              FilledButton.icon(
                onPressed: _saving ? null : () => unawaited(_save()),
                icon: _saving
                    ? const SizedBox(
                        width: SimfTokens.space4,
                        height: SimfTokens.space4,
                        child: CircularProgressIndicator(
                            strokeWidth:
                                SimfTokens.scanContactScreenStrokeWidth,),
                      )
                    : const Icon(Icons.person_add_alt_1),
                label: Text(l10n.saveContactLabel),
              ),
            ],
            const SizedBox(height: SimfTokens.space2),
          ],
        ),
      ),
    );
  }
}
