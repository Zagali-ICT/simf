# SIMF convention report

Generated 2026-08-08 by `dart run tool/conventions`.

## Summary

| Rule | Findings |
|------|----------|
| SIMF-C3 | 19 |
| **Total** | **19** |

## account feature


Issue file : src/Mobile/simf_app/lib/features/account/sign_in_screen.dart
Issue : _buildCard() returning Widget in a 418-line file (limit 400)  (line 319, SIMF-C3)
Fix : split the file; move this and its state into a widget

Issue file : src/Mobile/simf_app/lib/features/account/sign_up_interests_screen.dart
Issue : _buildBody() returning Widget in a 470-line file (limit 400)  (line 303, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildLoadError() returning Widget in a 470-line file (limit 400)  (line 411, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildChips() returning Widget in a 470-line file (limit 400)  (line 440, SIMF-C3)
Fix : split the file; move this and its state into a widget

Issue file : src/Mobile/simf_app/lib/features/account/sign_up_visitor_screen.dart
Issue : _buildBody() returning Widget in a 1393-line file (limit 400)  (line 738, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildLoadError() returning Widget in a 1393-line file (limit 400)  (line 921, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildProfileTypeField() returning Widget in a 1393-line file (limit 400)  (line 955, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildNationalityField() returning Widget in a 1393-line file (limit 400)  (line 994, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildDocumentFields() returning List<Widget> in a 1393-line file (limit 400)  (line 1129, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildPlaceOfBirthField() returning Widget in a 1393-line file (limit 400)  (line 1170, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildPlateField() returning Widget in a 1393-line file (limit 400)  (line 1191, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildIdImageField() returning Widget in a 1393-line file (limit 400)  (line 1282, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildFacePhotoField() returning Widget in a 1393-line file (limit 400)  (line 1302, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildOrganisationField() returning Widget in a 1393-line file (limit 400)  (line 1331, SIMF-C3)
Fix : split the file; move this and its state into a widget

## live feature


Issue file : src/Mobile/simf_app/lib/features/live/live_broadcast_screen.dart
Issue : _buildBody() returning Widget in a 508-line file (limit 400)  (line 275, SIMF-C3)
Fix : split the file; move this and its state into a widget

## sessions feature


Issue file : src/Mobile/simf_app/lib/features/sessions/session_detail_screen.dart
Issue : _buildBody() returning Widget in a 510-line file (limit 400)  (line 424, SIMF-C3)
Fix : split the file; move this and its state into a widget

## staff feature


Issue file : src/Mobile/simf_app/lib/features/staff/register_visitor_screen.dart
Issue : _buildBody() returning Widget in a 1264-line file (limit 400)  (line 644, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildLoadError() returning Widget in a 1264-line file (limit 400)  (line 679, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildForm() returning Widget in a 1264-line file (limit 400)  (line 709, SIMF-C3)
Fix : split the file; move this and its state into a widget

