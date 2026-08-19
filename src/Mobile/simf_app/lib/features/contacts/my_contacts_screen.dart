import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/contacts/data/contacts_providers.dart';
import 'package:simf_app/features/contacts/widgets/my_contacts_body.dart';

/// My Contacts — route: RouteNames.myContacts
/// Contract: SIMF-FDS-014 §5.6, D-286 — `GET /app/contacts` resolves the cards
///   on read (no PII snapshot). A row exports (`GET /app/contacts/{id}/vcard`)
///   or removes (`DELETE /app/contacts/{id}`, soft-delete). Final visuals from
///   SIMF-VID-001.
class MyContactsScreen extends ConsumerWidget {
  const MyContactsScreen({super.key});

  Future<void> _openScanner(BuildContext context, WidgetRef ref) async {
    await context.pushNamed(RouteNames.scanContact);
    // A save on the scanner closes it and returns here — reload to show it.
    ref.invalidate(savedContactsProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.myContactsTitle),
        actions: <Widget>[
          IconButton(
            tooltip: l10n.contactScanAdd,
            onPressed: () => unawaited(_openScanner(context, ref)),
            icon: const Icon(Icons.qr_code_scanner),
          ),
        ],
      ),
      body: SafeArea(
        child: MyContactsBody(
          onScan: () => unawaited(_openScanner(context, ref)),
        ),
      ),
    );
  }
}
