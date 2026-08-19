import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The console's top bar. Figma puts the back button on the LEFT (LTR header)
/// even in the RTL app, so the bar is forced LTR while the title itself stays
/// RTL — Figma 758:4655 for the circular navy back button.
class GateScanAppBar extends StatelessWidget implements PreferredSizeWidget {
  const GateScanAppBar({required this.title, required this.onBack, super.key});

  final String title;
  final VoidCallback onBack;

  @override
  Size get preferredSize => const Size.fromHeight(kToolbarHeight);

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.ltr,
      child: AppBar(
        backgroundColor: SimfTokens.navySurface,
        foregroundColor: SimfTokens.surface,
        elevation: 0,
        centerTitle: true,
        title: Text(
          title,
          textDirection: TextDirection.rtl,
          style: SimfTokens.labelWhiteSemiboldTitle,
        ),
        leading: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Material(
            color: SimfTokens.navyDeep,
            shape: const CircleBorder(),
            child: InkWell(
              customBorder: const CircleBorder(),
              onTap: onBack,
              child: const Padding(
                padding: EdgeInsets.all(SimfTokens.gap6),
                child: Icon(
                  Icons.chevron_left,
                  color: SimfTokens.surface,
                  size: SimfTokens.gateScanScreenSizeMd,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
