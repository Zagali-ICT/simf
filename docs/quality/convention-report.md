# SIMF convention report

Generated 2026-08-08 by `dart run tool/conventions`.

## Summary

| Rule | Findings |
|------|----------|
| SIMF-C1 | 599 |
| SIMF-C3 | 192 |
| SIMF-N1 | 17 |
| SIMF-N2 | 67 |
| **Total** | **875** |

## ControlPanel feature


Issue file : src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BulkBadgeGenerator.razor
Issue : style="@(row.Color is null ? null : $"  (line 57, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PrintBag.razor
Issue : style="@($"  (line 39, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ThemesList.razor
Issue : style="@($"  (line 87, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/WalkInRegistrationForm.razor
Issue : style="@($"  (line 35, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/WalkInSuccessModal.razor
Issue : style="@($"  (line 11, SIMF-N1)
Fix : a BEM class in the stylesheet

## Shared components feature


Issue file : src/Shared/SIMF.Components/Charts/SimfBarGauge.razor
Issue : style="--simf-gauge-fill:@Percent"  (line 17, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/Shared/SIMF.Components/Charts/SimfGroupedBarChart.razor
Issue : style="--simf-bar-x:@Percent(bar.X + (bar.Width / 2), PlotWidth);--simf-bar-y:@Percent(bar.Height, PlotHeight)"  (line 84, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/Shared/SIMF.Components/Forms/SimfContextMenu.razor
Issue : style="@($"  (line 9, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/Shared/SIMF.Components/Forms/SimfDataGrid.razor
Issue : style="@(column.Width is null ? null : $"  (line 144, SIMF-N1)
Fix : a BEM class in the stylesheet

## Website feature


Issue file : src/Website/SIMF.Web/Components/Layout/LandingHeader.razor
Issue : style="height:40px"  (line 116, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/Website/SIMF.Web/Components/Pages/Archive.razor
Issue : style="background-image:url('@s.Image')"  (line 55, SIMF-N1)
Fix : a BEM class in the stylesheet
Issue : style="background-image:url('@m.Image')"  (line 97, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/Website/SIMF.Web/Components/Pages/Landing.razor
Issue : style="background-image:url('@m.Image')"  (line 150, SIMF-N1)
Fix : a BEM class in the stylesheet
Issue : style="background-image:url('@s.Image')"  (line 217, SIMF-N1)
Fix : a BEM class in the stylesheet
Issue : style="background-image:url('@n.Image')"  (line 291, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/Website/SIMF.Web/Components/Pages/Organizer.razor
Issue : style="--logo:url('/@o.Logo')"  (line 34, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/Website/SIMF.Web/Components/Pages/Plenary.razor
Issue : style="background-image:url('@s.Image')"  (line 28, SIMF-N1)
Fix : a BEM class in the stylesheet

Issue file : src/Website/SIMF.Web/wwwroot/css/landing.css
Issue : raw hex #001640  (line 27, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #244a77  (line 28, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #e9edf1  (line 29, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #498fbd  (line 30, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #376b8e  (line 31, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #edf4f8  (line 32, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #005da2  (line 34, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #007cd8  (line 35, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #1b3859  (line 37, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #e8c060  (line 38, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #ba9a4d  (line 39, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #ae9048  (line 40, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fdf9ef  (line 41, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #514322  (line 42, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #161616  (line 44, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #1f2a37  (line 45, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #d2d6db  (line 46, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #f5f8fa  (line 47, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #ffffff  (line 48, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #545555  (line 49, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #c7dceb  (line 51, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #f8ebce  (line 52, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #244a77  (line 65, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #006923  (line 128, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #9da4ae  (line 198, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #6c737f  (line 201, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #384250  (line 202, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #384250  (line 203, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #1d3c60  (line 208, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #f3f4f6  (line 212, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #e5e7eb  (line 214, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #bbc7d5  (line 282, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 286, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 286, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 287, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 287, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 314, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 315, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 340, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fafafa  (line 360, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #e0e0e0  (line 360, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #001c71  (line 363, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #bbc7d5  (line 364, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fafafa  (line 366, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #757575  (line 373, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #757575  (line 375, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #244a77  (line 394, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #498fbd  (line 394, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fff  (line 572, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #e0e0e0  (line 572, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #e7e9f1  (line 575, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fff  (line 576, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fafafa  (line 578, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #e0e0e0  (line 578, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #212121  (line 578, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 592, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 592, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 593, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #000  (line 593, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fff  (line 598, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #f8ebce  (line 601, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fff  (line 646, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #f8ebce  (line 695, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #f8ebce  (line 702, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #fff  (line 725, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #f1f2f2  (line 803, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)
Issue : raw hex #e9edf1  (line 1235, SIMF-N2)
Fix : theme.tokens.css (the CSS token SSOT)

## about feature


Issue file : src/Mobile/simf_app/lib/features/about/widgets/about_cards.dart
Issue : private widget _Card extends StatelessWidget  (line 137, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _CardHeading extends StatelessWidget  (line 157, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/about/widgets/about_header.dart
Issue : size: 22  (line 31, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/about/widgets/check_for_updates_row.dart
Issue : Duration(seconds: 10)  (line 48, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const

## accessibility feature


Issue file : src/Mobile/simf_app/lib/features/accessibility/widgets/accessibility_font_size_card.dart
Issue : private widget _SizeChip extends StatelessWidget  (line 64, SIMF-C3)
Fix : its own file under widgets/
Issue : height: 36  (line 86, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## account feature


Issue file : src/Mobile/simf_app/lib/features/account/badge_activation_screen.dart
Issue : widget-building method _buildPasswordErrors() returning Widget  (line 184, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildBody() returning Widget  (line 242, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 246, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 252, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 50  (line 285, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 6  (line 307, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 128  (line 321, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 128  (line 337, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : widget-building method _buildBottomActions() returning Widget  (line 351, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 355, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/badge_password_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 147, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 153, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 159, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 128  (line 182, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : widget-building method _buildSubmit() returning Widget  (line 222, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 226, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/biometric_auth.dart
Issue : Duration(seconds: 2)  (line 152, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(seconds: 8)  (line 203, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const

Issue file : src/Mobile/simf_app/lib/features/account/biometric_step_up_screen.dart
Issue : widget-building method _buildContent() returning Widget  (line 223, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 227, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 230, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 253, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildSubmitButton() returning Widget  (line 296, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 300, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildResendRow() returning Widget  (line 313, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/account/email_otp_verify_screen.dart
Issue : widget-building method _buildContent() returning Widget  (line 215, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 219, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 222, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 64  (line 238, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildSubmitButton() returning Widget  (line 271, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 275, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildResendRow() returning Widget  (line 288, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/account/forgot_password_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 135, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 139, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 145, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 50  (line 159, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : size: 18  (line 166, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildBottomActions() returning Widget  (line 191, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 197, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 6  (line 220, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/reset_password_screen.dart
Issue : widget-building method _buildPasswordErrors() returning Widget  (line 74, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildBody() returning Widget  (line 195, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 199, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 205, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 6  (line 220, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 128  (line 237, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 128  (line 253, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : widget-building method _buildBottomActions() returning Widget  (line 279, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 283, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/sign_in_screen.dart
Issue : maxWidth: 560  (line 293, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildCard() returning Widget  (line 319, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/account/sign_up_email_verify_screen.dart
Issue : widget-building method _buildContent() returning Widget  (line 223, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 227, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 230, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 243, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildCooldownRow() returning Widget  (line 272, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 6  (line 280, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildBottomActions() returning Widget  (line 290, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 295, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 6  (line 316, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/sign_up_form_screen.dart
Issue : widget-building method _buildPasswordErrors() returning Widget  (line 93, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 255, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildCard() returning Widget  (line 280, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/account/sign_up_interests_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 303, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 342, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxWidth: 560  (line 394, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildLoadError() returning Widget  (line 411, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildChips() returning Widget  (line 440, SIMF-C3)
Fix : its own file under widgets/
Issue : crossAxisCount: 2  (line 453, SIMF-C1)
Fix : computed from core/responsive/breakpoints.dart
Issue : mainAxisExtent: 43  (line 456, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/sign_up_visitor_screen.dart
Issue : Duration(milliseconds: 350)  (line 351, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : widget-building method _buildBody() returning Widget  (line 736, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 758, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 50  (line 804, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 50  (line 816, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 100  (line 838, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 100  (line 857, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : fontSize: 13  (line 903, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildLoadError() returning Widget  (line 919, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildProfileTypeField() returning Widget  (line 953, SIMF-C3)
Fix : its own file under widgets/
Issue : Radius.circular(12)  (line 982, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildNationalityField() returning Widget  (line 992, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildDocumentFields() returning List<Widget>  (line 1127, SIMF-C3)
Fix : its own file under widgets/
Issue : maxLength: 10  (line 1134, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : widget-building method _buildPlaceOfBirthField() returning Widget  (line 1168, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildPlateField() returning Widget  (line 1189, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildIdImageField() returning Widget  (line 1280, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildFacePhotoField() returning Widget  (line 1300, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildOrganisationField() returning Widget  (line 1329, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/account/widgets/account_auth_prompt.dart
Issue : width: 6  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/account_form_field.dart
Issue : size: 16  (line 140, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/account_header.dart
Issue : size: 44  (line 19, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/account_remember_forgot.dart
Issue : width: 19  (line 37, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 19  (line 38, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.5  (line 50, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 5  (line 58, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/account_sub_header.dart
Issue : size: 20  (line 41, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 56  (line 46, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/account_terms_checkbox.dart
Issue : width: 19  (line 45, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 19  (line 46, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.5  (line 53, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 13  (line 71, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 13  (line 83, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 6  (line 100, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/account_top_controls.dart
Issue : size: 24  (line 44, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/attachment_field.dart
Issue : width: 40  (line 74, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 75, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 40  (line 84, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 85, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _AttachBox extends StatelessWidget  (line 112, SIMF-C3)
Fix : its own file under widgets/
Issue : height: 56  (line 130, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 143, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/auth_chrome.dart
Issue : Size.fromHeight(48)  (line 31, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 10  (line 49, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Size.fromHeight(48)  (line 60, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 20  (line 99, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 20  (line 100, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 102, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/beige_tabs.dart
Issue : height: 34  (line 38, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/date_of_birth_field.dart
Issue : size: 18  (line 40, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/interest_chip.dart
Issue : BorderRadius.circular(999)  (line 26, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(999)  (line 32, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.2  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/mobile_field.dart
Issue : maxLength: 17  (line 71, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/navy_password_toggle.dart
Issue : size: 18  (line 27, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/organisation_typeahead_field.dart
Issue : size: 18  (line 82, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 14  (line 95, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 14  (line 96, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 98, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 10  (line 102, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/otp_code_boxes.dart
Issue : height: 52  (line 40, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 6  (line 48, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : height: 52  (line 81, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.5  (line 86, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 96  (line 119, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 96  (line 120, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.2  (line 124, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 34  (line 127, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/place_of_birth_field.dart
Issue : maxLength: 128  (line 61, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/plate_number_field.dart
Issue : width: 92  (line 66, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 4  (line 74, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/profile_type_field.dart
Issue : width: 16  (line 52, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 16  (line 53, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 55, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/sign_in_alt_actions.dart
Issue : size: 20  (line 64, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 75, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 83, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/sign_up_visitor_header_avatar.dart
Issue : width: 40  (line 22, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 23, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 31, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 37, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 40  (line 37, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/account/widgets/terms_and_next_buttons.dart
Issue : height: 20  (line 62, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 20  (line 63, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 65, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## ai_summary feature


Issue file : src/Mobile/simf_app/lib/features/ai_summary/session_summary_list_screen.dart
Issue : widget-building method _buildList() returning Widget  (line 102, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/ai_summary/widgets/session_summary_list_card.dart
Issue : private widget _CategoryPill extends StatelessWidget  (line 146, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/ai_summary/widgets/summary_content_card.dart
Issue : width: 6  (line 84, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 6  (line 85, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/ai_summary/widgets/summary_generate_card.dart
Issue : size: 18  (line 56, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 75, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## app (shared shell) feature


Issue file : src/Mobile/simf_app/lib/app/theme/app_theme.dart
Issue : Size.fromHeight(48)  (line 45, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Size.fromHeight(48)  (line 124, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Size.fromHeight(48)  (line 232, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Size.fromHeight(48)  (line 278, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/media_coverage_tabs.dart
Issue : private widget _MediaTab extends StatelessWidget  (line 56, SIMF-C3)
Fix : its own file under widgets/
Issue : height: 48  (line 83, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/more_drawer.dart
Issue : private widget _DrawerTile extends StatelessWidget  (line 240, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/app/widgets/qr_scan_view.dart
Issue : private widget _ScannerHeader extends StatelessWidget  (line 106, SIMF-C3)
Fix : its own file under widgets/
Issue : height: 56  (line 118, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 142, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 24  (line 153, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_bottom_nav.dart
Issue : Offset(-1)  (line 52, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : blurRadius: 6  (line 53, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 64  (line 71, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _Item extends StatelessWidget  (line 120, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 24  (line 153, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _CentreAction extends StatelessWidget  (line 174, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 56  (line 200, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 56  (line 201, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_cards.dart
Issue : size: 16  (line 123, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 152, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 178, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 72  (line 228, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 64  (line 229, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 273, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_filter_search_field.dart
Issue : height: 48  (line 32, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 16  (line 45, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 40  (line 73, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 16  (line 90, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_form_scaffold.dart
Issue : size: 20  (line 79, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 44  (line 97, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 16  (line 98, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 24  (line 103, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 24  (line 118, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxWidth: 560  (line 134, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 140, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : EdgeInsets.all(24)  (line 145, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_identity_cell.dart
Issue : maxLines: 2  (line 97, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : size: 20  (line 111, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _LogoOrInitials extends StatelessWidget  (line 137, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _InitialsAvatar extends StatelessWidget  (line 171, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_language_toggle.dart
Issue : width: 16  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 16  (line 35, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 10  (line 47, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(12)  (line 59, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 48  (line 61, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 24  (line 62, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : EdgeInsets.all(4)  (line 63, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(12)  (line 66, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_page_shell.dart
Issue : width: 42  (line 205, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 42  (line 206, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 42  (line 240, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 42  (line 240, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _ScreenAnnouncer extends ConsumerStatefulWidget  (line 247, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 24  (line 335, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 359, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 313  (line 501, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 323  (line 502, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Radius.circular(40)  (line 505, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _AvatarFallback extends StatelessWidget  (line 580, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_scanner_body.dart
Issue : Duration(seconds: 8)  (line 131, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : widget-building method _buildCameraSection() returning Widget  (line 216, SIMF-C3)
Fix : its own file under widgets/
Issue : Size.fromHeight(48)  (line 232, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildManual() returning Widget  (line 303, SIMF-C3)
Fix : its own file under widgets/
Issue : Size.fromHeight(48)  (line 325, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _OrDivider extends StatelessWidget  (line 333, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _CameraErrorCard extends StatelessWidget  (line 357, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 40  (line 386, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_scanner_frame.dart
Issue : width: 2.36  (line 9, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Duration(milliseconds: 2200)  (line 79, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : BorderRadius.circular(24)  (line 128, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : blurRadius: 60  (line 132, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(24)  (line 133, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : EdgeInsets.fromLTRB(20)  (line 145, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : EdgeInsets.fromLTRB(16)  (line 145, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : EdgeInsets.fromLTRB(4)  (line 145, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : EdgeInsets.fromLTRB(16)  (line 145, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildWindow() returning Widget  (line 153, SIMF-C3)
Fix : its own file under widgets/
Issue : BorderRadius.circular(16)  (line 155, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 64  (line 171, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildScanLine() returning Widget  (line 181, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildStatusRow() returning Widget  (line 208, SIMF-C3)
Fix : its own file under widgets/
Issue : fontSize: 12  (line 221, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 8  (line 224, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(100)  (line 226, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 6  (line 228, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _ScanLine extends StatelessWidget  (line 238, SIMF-C3)
Fix : its own file under widgets/
Issue : height: 2  (line 245, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : blurRadius: 8  (line 255, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _Bracket extends StatelessWidget  (line 262, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 28  (line 273, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 28  (line 274, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_search_field.dart
Issue : size: 14  (line 53, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : minHeight: 44  (line 57, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : minWidth: 44  (line 57, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 61, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : minHeight: 44  (line 66, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : minWidth: 44  (line 66, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_states.dart
Issue : size: 56  (line 72, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/app/widgets/simf_tiles.dart
Issue : size: 24  (line 80, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 81, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _TileBody extends StatelessWidget  (line 139, SIMF-C3)
Fix : its own file under widgets/

## archive feature


Issue file : src/Mobile/simf_app/lib/features/archive/widgets/archive_bullet.dart
Issue : width: 5  (line 31, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 5  (line 32, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/archive/widgets/archive_edition_pills.dart
Issue : private widget _EditionPill extends StatelessWidget  (line 44, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/archive/widgets/archive_gallery_row.dart
Issue : height: 104  (line 24, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/archive/widgets/archive_gallery_tile.dart
Issue : width: 104  (line 25, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 104  (line 26, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 28  (line 75, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/archive/widgets/archive_past_speaker_card.dart
Issue : width: 72  (line 23, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 72  (line 28, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 72  (line 29, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 72  (line 38, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 72  (line 39, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLines: 2  (line 51, SIMF-C1)
Fix : a named layout const (never a value-named token)

Issue file : src/Mobile/simf_app/lib/features/archive/widgets/archive_past_speakers_row.dart
Issue : private widget _PastSpeakerOverflow extends StatelessWidget  (line 49, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 72  (line 60, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 72  (line 65, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 72  (line 66, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/archive/widgets/archive_place_time_row.dart
Issue : private widget _LabelledBullet extends StatelessWidget  (line 54, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/archive/widgets/archive_stat_row.dart
Issue : private widget _StatTile extends StatelessWidget  (line 42, SIMF-C3)
Fix : its own file under widgets/

## badge feature


Issue file : src/Mobile/simf_app/lib/features/badge/badge_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 117, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/badge/widgets/badge_actions.dart
Issue : size: 24  (line 39, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 55, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 73, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/badge/widgets/badge_qr_card.dart
Issue : size: 64  (line 122, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## booths feature


Issue file : src/Mobile/simf_app/lib/features/booths/booths_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 126, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/booths/exhibitor_detail_screen.dart
Issue : widget-building method _build() returning Widget  (line 67, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/booths/widgets/booth_company_header.dart
Issue : private widget _CountryFlagTile extends StatelessWidget  (line 87, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 40  (line 98, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 99, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 28  (line 109, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _LogoTile extends StatelessWidget  (line 115, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/booths/widgets/booth_contact_box.dart
Issue : size: 16  (line 38, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/booths/widgets/booth_guide_button.dart
Issue : size: 18  (line 51, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/booths/widgets/booth_hall_row.dart
Issue : private widget _CodePill extends StatelessWidget  (line 56, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/booths/widgets/booth_officer_row.dart
Issue : width: 40  (line 36, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 37, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## chatbot feature


Issue file : src/Mobile/simf_app/lib/features/chatbot/widgets/chat_bubble.dart
Issue : height: 1.5  (line 27, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _AiBadge extends StatelessWidget  (line 67, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/chatbot/widgets/chat_composer.dart
Issue : maxLines: 4  (line 51, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : width: 12  (line 85, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 12  (line 86, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 88, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 14  (line 94, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/chatbot/widgets/quick_replies.dart
Issue : private widget _QuickReplyChip extends StatelessWidget  (line 32, SIMF-C3)
Fix : its own file under widgets/

## contact_us feature


Issue file : src/Mobile/simf_app/lib/features/contact_us/widgets/contact_info_card.dart
Issue : private widget _InfoRow extends StatelessWidget  (line 53, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 40  (line 85, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 86, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 92, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/contact_us/widgets/contact_send_message_card.dart
Issue : maxLines: 5  (line 70, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : height: 20  (line 82, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 20  (line 83, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 84, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/contact_us/widgets/contact_social_card.dart
Issue : private widget _SocialButton extends StatelessWidget  (line 70, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 48  (line 90, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 91, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 101, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## contacts feature


Issue file : src/Mobile/simf_app/lib/features/contacts/my_contacts_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 108, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _ErrorState extends StatelessWidget  (line 151, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/contacts/scan_contact_screen.dart
Issue : private widget _ContactPreviewSheet extends ConsumerStatefulWidget  (line 146, SIMF-C3)
Fix : its own file under widgets/
Issue : maxLength: 280  (line 237, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : width: 16  (line 244, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 16  (line 245, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 246, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/contacts/share_my_contact_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 157, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 240  (line 189, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 16  (line 230, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 16  (line 231, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 232, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/contacts/widgets/contact_card.dart
Issue : radius: 26  (line 51, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _ChannelRow extends StatelessWidget  (line 120, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 18  (line 138, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/contacts/widgets/contacts_empty_state.dart
Issue : size: 56  (line 31, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## content feature


Issue file : src/Mobile/simf_app/lib/features/content/terms_screen.dart
Issue : width: 313  (line 116, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 323  (line 117, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(40)  (line 120, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 56  (line 130, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 145, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildBody() returning Widget  (line 176, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildContent() returning Widget  (line 206, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/content/widgets/terms_bullet_card.dart
Issue : width: 0.2  (line 19, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## core feature


Issue file : src/Mobile/simf_app/lib/core/session/session_guard.dart
Issue : Duration(seconds: 15)  (line 32, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(seconds: 60)  (line 33, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(minutes: 5)  (line 34, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(seconds: 30)  (line 35, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const

Issue file : src/Mobile/simf_app/lib/core/session/session_timeout_overlay.dart
Issue : maxWidth: 360  (line 43, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Size.fromHeight(48)  (line 75, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Size.fromHeight(48)  (line 85, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/core/startup/server_app_update_checker.dart
Issue : Duration(days: 3)  (line 24, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const

Issue file : src/Mobile/simf_app/lib/core/utils/saudi_time.dart
Issue : Duration(hours: 3)  (line 24, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const

Issue file : src/Mobile/simf_app/lib/core/utils/scan_gate.dart
Issue : Duration(seconds: 2)  (line 27, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const

Issue file : src/Mobile/simf_app/lib/core/widgets/coming_soon_screen.dart
Issue : width: 80  (line 38, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 80  (line 39, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/core/widgets/simf_auth_sweep.dart
Issue : width: 313  (line 35, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 323  (line 36, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(40)  (line 39, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/core/widgets/simf_checkbox_tile.dart
Issue : width: 19  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 19  (line 35, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.5  (line 40, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 8  (line 47, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 14  (line 55, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/core/widgets/simf_field_style.dart
Issue : fontSize: 14  (line 12, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 14  (line 21, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/core/widgets/simf_image_source_sheet.dart
Issue : private widget _SourceTile extends StatelessWidget  (line 73, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/core/widgets/simf_labeled_text_field.dart
Issue : height: 8  (line 36, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/core/widgets/simf_radio_pill.dart
Issue : height: 48  (line 33, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 18  (line 45, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 18  (line 46, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.2  (line 49, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 10  (line 54, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 10  (line 55, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 12  (line 63, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 14  (line 67, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## delegations feature


Issue file : src/Mobile/simf_app/lib/features/delegations/widgets/delegation_card.dart
Issue : private widget _FlagBox extends StatelessWidget  (line 85, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 48  (line 93, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 94, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 28  (line 101, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/delegations/widgets/delegation_meeting_request_sheet.dart
Issue : width: 80  (line 286, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 5  (line 287, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 20  (line 340, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 20  (line 341, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 343, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 1000  (line 448, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : width: 20  (line 484, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 20  (line 485, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 487, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxHeight: 264  (line 508, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 541, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _DelegationOptionTile extends StatelessWidget  (line 635, SIMF-C3)
Fix : its own file under widgets/
Issue : fontSize: 22  (line 675, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/delegations/widgets/delegations_body.dart
Issue : private widget _ActiveFilterChip extends StatelessWidget  (line 135, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 14  (line 186, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/delegations/widgets/delegations_stats_strip.dart
Issue : Offset(0.60)  (line 47, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.30)  (line 47, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.16)  (line 48, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.25)  (line 48, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.42)  (line 49, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.10)  (line 49, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.46)  (line 50, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.16)  (line 50, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.09)  (line 51, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.48)  (line 51, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.22)  (line 52, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.76)  (line 52, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.30)  (line 53, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.63)  (line 53, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.54)  (line 54, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Offset(0.18)  (line 54, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _FlagSpot extends StatelessWidget  (line 104, SIMF-C3)
Fix : its own file under widgets/
Issue : fontSize: 14  (line 133, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _Stat extends StatelessWidget  (line 161, SIMF-C3)
Fix : its own file under widgets/

## exhibition feature


Issue file : src/Mobile/simf_app/lib/features/exhibition/entity_identity_card.dart
Issue : width: 108  (line 43, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 108  (line 43, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _LocationLine extends StatelessWidget  (line 85, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _TierPill extends StatelessWidget  (line 119, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 16  (line 148, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/exhibition/entity_link_row.dart
Issue : size: 18  (line 113, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _IconBox extends StatelessWidget  (line 124, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 44  (line 136, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 44  (line 137, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 148, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 149, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## exhibitor feature


Issue file : src/Mobile/simf_app/lib/features/exhibitor/my_visitors_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 99, SIMF-C3)
Fix : its own file under widgets/

## faq feature


Issue file : src/Mobile/simf_app/lib/features/faq/widgets/faq_tile.dart
Issue : size: 20  (line 49, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## feedback feature


Issue file : src/Mobile/simf_app/lib/features/feedback/rate_screen.dart
Issue : widget-building method _buildForm() returning Widget  (line 203, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 30  (line 237, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 2000  (line 284, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLines: 4  (line 285, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : minLines: 4  (line 286, SIMF-C1)
Fix : a named layout const (never a value-named token)

Issue file : src/Mobile/simf_app/lib/features/feedback/widgets/rate_category_row.dart
Issue : size: 18  (line 46, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/feedback/widgets/rate_gold_button.dart
Issue : width: 20  (line 33, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 20  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 36, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/feedback/widgets/rate_navy_note_chip.dart
Issue : size: 16  (line 29, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## forum_guide feature


Issue file : src/Mobile/simf_app/lib/features/forum_guide/widgets/forum_guide_cards.dart
Issue : size: 14  (line 41, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 32  (line 81, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 32  (line 82, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 117, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## gallery feature


Issue file : src/Mobile/simf_app/lib/features/gallery/widgets/coverage_tabs.dart
Issue : private widget _CoverageTab extends StatelessWidget  (line 54, SIMF-C3)
Fix : its own file under widgets/
Issue : maxLines: 2  (line 81, SIMF-C1)
Fix : a named layout const (never a value-named token)

Issue file : src/Mobile/simf_app/lib/features/gallery/widgets/gallery_media_tile.dart
Issue : private widget _PlayGlyph extends StatelessWidget  (line 73, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 52  (line 82, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 52  (line 83, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 30  (line 91, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _Thumbnail extends StatelessWidget  (line 99, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 22  (line 128, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 22  (line 129, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 130, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/gallery/widgets/gallery_placeholder_box.dart
Issue : size: 32  (line 18, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/gallery/widgets/media_grid.dart
Issue : crossAxisCount: 2  (line 27, SIMF-C1)
Fix : computed from core/responsive/breakpoints.dart

## gates feature


Issue file : src/Mobile/simf_app/lib/features/gates/gate_scan_screen.dart
Issue : size: 26  (line 323, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 451, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/gates/widgets/gate_direction_button.dart
Issue : size: 18  (line 50, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/gates/widgets/gate_result_view.dart
Issue : size: 84  (line 55, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/gates/widgets/gate_setup_view.dart
Issue : size: 60  (line 60, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 84, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 22  (line 149, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## guest feature


Issue file : src/Mobile/simf_app/lib/features/guest/guest_mode_screen.dart
Issue : width: 64  (line 43, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 64  (line 44, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.5  (line 48, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 30  (line 53, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 1.7  (line 83, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 1.7  (line 92, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## home feature


Issue file : src/Mobile/simf_app/lib/features/home/widgets/carousel_dots.dart
Issue : Duration(milliseconds: 250)  (line 21, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : height: 6  (line 24, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(3)  (line 29, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/home/widgets/follow_us_section.dart
Issue : private widget _SocialButton extends StatelessWidget  (line 106, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 20  (line 144, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _WebsiteLink extends StatelessWidget  (line 155, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 16  (line 180, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/home/widgets/guest_home.dart
Issue : size: 32  (line 104, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _GuestBanner extends StatelessWidget  (line 131, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/home/widgets/highlights_carousel.dart
Issue : Duration(seconds: 4)  (line 37, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(milliseconds: 450)  (line 63, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : private widget _HighlightSlide extends StatelessWidget  (line 109, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 18  (line 148, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 18  (line 149, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 151, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 28  (line 157, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLines: 2  (line 179, SIMF-C1)
Fix : a named layout const (never a value-named token)

Issue file : src/Mobile/simf_app/lib/features/home/widgets/home_banners.dart
Issue : size: 24  (line 78, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/home/widgets/home_hero_banner.dart
Issue : Duration(seconds: 4)  (line 45, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(milliseconds: 450)  (line 89, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : private widget _HeroImage extends StatelessWidget  (line 163, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _HeroOverlay extends StatelessWidget  (line 199, SIMF-C3)
Fix : its own file under widgets/
Issue : maxLines: 2  (line 241, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : maxLines: 2  (line 249, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : private widget _MetaLine extends StatelessWidget  (line 267, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 14  (line 279, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/home/widgets/operational_homes.dart
Issue : size: 32  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 32  (line 45, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 32  (line 113, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/home/widgets/pending_approval_card.dart
Issue : size: 24  (line 68, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## live feature


Issue file : src/Mobile/simf_app/lib/features/live/live_broadcast_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 275, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/live/widgets/live_badges.dart
Issue : width: 7  (line 42, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 7  (line 43, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 14  (line 92, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/live/widgets/live_content.dart
Issue : size: 56  (line 33, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _TogglePill extends StatelessWidget  (line 106, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 16  (line 133, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 1.5  (line 180, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 5  (line 188, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 5  (line 189, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 211, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 280, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLines: 2  (line 324, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : private widget _TimeChip extends StatelessWidget  (line 339, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/live/widgets/live_message_surfaces.dart
Issue : private widget _MessageSurface extends StatelessWidget  (line 36, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 40  (line 67, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/live/widgets/live_player_surface.dart
Issue : private widget _CaptionStrip extends ConsumerWidget  (line 73, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/live/widgets/live_video_player.dart
Issue : private widget _YoutubeView extends StatelessWidget  (line 203, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _Player extends StatelessWidget  (line 225, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _PlayerLoading extends StatelessWidget  (line 267, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 52  (line 283, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 52  (line 284, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 22  (line 292, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _PlayerError extends StatelessWidget  (line 302, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 36  (line 334, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## media_partners feature


Issue file : src/Mobile/simf_app/lib/features/media_partners/media_partners_screen.dart
Issue : crossAxisCount: 2  (line 102, SIMF-C1)
Fix : computed from core/responsive/breakpoints.dart

Issue file : src/Mobile/simf_app/lib/features/media_partners/widgets/partner_card.dart
Issue : maxLines: 2  (line 40, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : private widget _PartnerLogo extends StatelessWidget  (line 62, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 18  (line 105, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 18  (line 106, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 107, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _InitialsTile extends StatelessWidget  (line 121, SIMF-C3)
Fix : its own file under widgets/

## meetings feature


Issue file : src/Mobile/simf_app/lib/features/meetings/meeting_confirm_screen.dart
Issue : size: 64  (line 126, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 64  (line 163, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/meetings/meetings_screen.dart
Issue : widget-building method _buildList() returning Widget  (line 149, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/meetings/widgets/meeting_card.dart
Issue : size: 38  (line 155, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 172, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 12  (line 197, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _FlagBadge extends StatelessWidget  (line 247, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 48  (line 257, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 258, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## moderation feature


Issue file : src/Mobile/simf_app/lib/features/moderation/widgets/moderated_session_tile.dart
Issue : size: 32  (line 39, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/moderation/widgets/moderator_action_button.dart
Issue : size: 30  (line 65, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/moderation/widgets/moderator_filter_bar.dart
Issue : private widget _Chip extends StatelessWidget  (line 68, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/moderation/widgets/moderator_header.dart
Issue : size: 26  (line 47, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _RolePill extends StatelessWidget  (line 68, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/moderation/widgets/moderator_question_card.dart
Issue : width: 80  (line 132, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 80  (line 133, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## more feature


Issue file : src/Mobile/simf_app/lib/features/more/widgets/more_list.dart
Issue : height: 48  (line 59, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 22  (line 86, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/more/widgets/more_profile_card.dart
Issue : size: 42  (line 61, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 66, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## myarea feature


Issue file : src/Mobile/simf_app/lib/features/myarea/identity_verification_screen.dart
Issue : fontSize: 30  (line 281, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 32  (line 283, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 32  (line 285, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/myarea/my_area_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 161, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildErrorState() returning Widget  (line 187, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildLimited() returning Widget  (line 202, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildDashboard() returning Widget  (line 225, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/myarea/my_mobile_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 168, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 560  (line 185, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 15  (line 207, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxWidth: 560  (line 246, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildLoadError() returning Widget  (line 262, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/myarea/my_sessions_screen.dart
Issue : private widget _TabbedList extends StatelessWidget  (line 123, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _MySessionCard extends StatelessWidget  (line 178, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/myarea/widgets/identity_capture_view.dart
Issue : fontSize: 15  (line 71, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 10  (line 80, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : fontSize: 26  (line 87, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 32  (line 111, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 6  (line 112, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(3)  (line 115, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1080  (line 121, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 1440  (line 121, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/myarea/widgets/identity_fallback_view.dart
Issue : size: 56  (line 30, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : Size.fromHeight(48)  (line 45, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/myarea/widgets/my_area_identity_card.dart
Issue : width: 0.2  (line 35, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 48  (line 77, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 78, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 0.5  (line 82, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 93, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _TappableAvatar extends StatelessWidget  (line 118, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 64  (line 134, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 1.5  (line 157, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 12  (line 161, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/myarea/widgets/my_area_rows.dart
Issue : size: 20  (line 74, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 134, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 48  (line 163, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 169, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## news feature


Issue file : src/Mobile/simf_app/lib/features/news/news_article_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 77, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/news/widgets/news_card.dart
Issue : maxLines: 2  (line 71, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : private widget _NewsThumbnail extends StatelessWidget  (line 101, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 18  (line 137, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 18  (line 138, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 139, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _NewsImageFallback extends StatelessWidget  (line 173, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 28  (line 183, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _CategoryChip extends StatelessWidget  (line 190, SIMF-C3)
Fix : its own file under widgets/

## notifications feature


Issue file : src/Mobile/simf_app/lib/features/notifications/notifications_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 278, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/notifications/widgets/notification_card.dart
Issue : private widget _UnreadDot extends StatelessWidget  (line 105, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 14  (line 113, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 14  (line 114, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/notifications/widgets/notification_category_icon.dart
Issue : width: 40  (line 27, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 28, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 30, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## onboarding feature


Issue file : src/Mobile/simf_app/lib/features/onboarding/onboarding_screen.dart
Issue : Duration(milliseconds: 250)  (line 122, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(milliseconds: 250)  (line 134, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : size: 136  (line 180, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/onboarding/widgets/onboarding_dots.dart
Issue : Duration(milliseconds: 200)  (line 26, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : height: 8  (line 29, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(999)  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/onboarding/widgets/onboarding_top_bar.dart
Issue : size: 20  (line 43, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## questions feature


Issue file : src/Mobile/simf_app/lib/features/questions/widgets/send_question_content.dart
Issue : maxLength: 500  (line 145, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : private widget _NumberedLine extends StatelessWidget  (line 239, SIMF-C3)
Fix : its own file under widgets/

## registration feature


Issue file : src/Mobile/simf_app/lib/features/registration/registration_status_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 117, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildStatusView() returning Widget  (line 129, SIMF-C3)
Fix : its own file under widgets/
Issue : maxWidth: 480  (line 173, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildError() returning Widget  (line 216, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/registration/registration_success_screen.dart
Issue : width: 313  (line 62, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 323  (line 63, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : BorderRadius.circular(40)  (line 66, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/contact_us_section.dart
Issue : private widget _ContactTile extends StatelessWidget  (line 86, SIMF-C3)
Fix : its own file under widgets/
Issue : height: 52  (line 100, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 0.8  (line 103, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 106, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_primary_button.dart
Issue : height: 48  (line 20, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_secondary_button.dart
Issue : height: 48  (line 23, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_sign_out_link.dart
Issue : Size.fromHeight(36)  (line 22, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_status_header.dart
Issue : size: 20  (line 36, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : splashRadius: 22  (line 38, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 48  (line 49, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_status_hero.dart
Issue : width: 104  (line 28, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 104  (line 29, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 2.36  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 40  (line 36, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 1.5  (line 55, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_success_actions.dart
Issue : Size.fromHeight(48)  (line 39, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_success_body.dart
Issue : maxWidth: 400  (line 41, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_success_header.dart
Issue : height: 56  (line 22, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 36, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/registration/widgets/registration_success_mark.dart
Issue : width: 104  (line 14, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 104  (line 15, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 2.4  (line 20, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 40  (line 24, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## requests feature


Issue file : src/Mobile/simf_app/lib/features/requests/requests_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 132, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/requests/widgets/request_action_row.dart
Issue : size: 14  (line 119, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/requests/widgets/request_card.dart
Issue : maxLines: 2  (line 76, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : size: 20  (line 103, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : widget-building method _buildDetail() returning Widget  (line 127, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 8  (line 142, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 8  (line 143, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 16  (line 176, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _IconBox extends StatelessWidget  (line 226, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 16  (line 243, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## sessions feature


Issue file : src/Mobile/simf_app/lib/features/sessions/join_session_hub_screen.dart
Issue : private widget _HubList extends StatelessWidget  (line 71, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _HubRow extends StatelessWidget  (line 121, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 20  (line 166, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/my_seat_screen.dart
Issue : private widget _SeatMapView extends StatelessWidget  (line 95, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _SessionCard extends StatelessWidget  (line 147, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _SeatChip extends StatelessWidget  (line 214, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _ChangeSeatButton extends StatelessWidget  (line 264, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 18  (line 290, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _Actions extends StatelessWidget  (line 296, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 18  (line 328, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 352, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/seat_picker_screen.dart
Issue : size: 20  (line 299, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 321, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _SelectedSeatChip extends StatelessWidget  (line 334, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/session_detail_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 424, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/session_presentations_screen.dart
Issue : private widget _Body extends StatelessWidget  (line 101, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _PresentationCard extends StatelessWidget  (line 196, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _FileIcon extends StatelessWidget  (line 299, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 20  (line 315, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _SessionSummryButton extends StatelessWidget  (line 322, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/sessions_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 127, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/ask_host_card.dart
Issue : size: 24  (line 45, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/favourite_heart_button.dart
Issue : size: 16  (line 68, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 16  (line 72, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/hall_seat_map.dart
Issue : private widget _StageBar extends StatelessWidget  (line 224, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _SeatGridRow extends StatelessWidget  (line 254, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _SeatBox extends StatelessWidget  (line 407, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _Legend extends StatelessWidget  (line 519, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _TierLegend extends StatelessWidget  (line 582, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _LegendItem extends StatelessWidget  (line 623, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/programme_day_banner.dart
Issue : size: 16  (line 65, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _DayBannerFallback extends StatelessWidget  (line 77, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 28  (line 88, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/programme_day_strip.dart
Issue : widget-building method _buildBand() returning Widget  (line 58, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _DayCell extends StatelessWidget  (line 145, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_arrival_action.dart
Issue : size: 20  (line 124, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_booking_actions.dart
Issue : size: 24  (line 118, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 24  (line 154, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_card_meta.dart
Issue : size: 12  (line 24, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 12  (line 25, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 12  (line 61, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 12  (line 62, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_detail_header.dart
Issue : size: 22  (line 66, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_filter_tabs.dart
Issue : private widget _Pill extends StatelessWidget  (line 84, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 14  (line 134, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_header_card.dart
Issue : private widget _CategoryPill extends StatelessWidget  (line 140, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _HeaderActionButton extends StatelessWidget  (line 175, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _IndexBadge extends StatelessWidget  (line 238, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _MetaRow extends StatelessWidget  (line 276, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _MetaItem extends StatelessWidget  (line 321, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 14  (line 333, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_reservation_card.dart
Issue : size: 20  (line 76, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _SeatMarker extends StatelessWidget  (line 87, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_speaker_card.dart
Issue : size: 14  (line 109, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _SpeakerAvatar extends StatelessWidget  (line 128, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 20  (line 142, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 40  (line 146, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 40  (line 147, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_state_chip.dart
Issue : width: 8  (line 127, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 8  (line 128, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_timeline_row.dart
Issue : maxLines: 2  (line 48, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : maxLines: 2  (line 95, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : size: 14  (line 105, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _TimeRail extends StatelessWidget  (line 134, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_type_tabs.dart
Issue : private widget _TypeTab extends StatelessWidget  (line 63, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/sessions_search_field.dart
Issue : size: 18  (line 53, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## speakers feature


Issue file : src/Mobile/simf_app/lib/features/speakers/speaker_profile_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 142, SIMF-C3)
Fix : its own file under widgets/
Issue : size: 18  (line 255, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/speakers/speakers_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 93, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/speakers/widgets/meeting_request_sheet.dart
Issue : width: 80  (line 302, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 5  (line 303, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 20  (line 359, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 20  (line 360, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 362, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxLength: 1000  (line 443, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : width: 20  (line 483, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 20  (line 484, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : strokeWidth: 2  (line 486, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : maxHeight: 264  (line 506, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 565, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/speakers/widgets/speaker_avatar.dart
Issue : size: 64  (line 39, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 125  (line 48, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 125  (line 49, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : EdgeInsets.all(2.77)  (line 50, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/speakers/widgets/speaker_cv.dart
Issue : private widget _CvTab extends StatelessWidget  (line 50, SIMF-C3)
Fix : its own file under widgets/
Issue : maxLines: 2  (line 85, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : height: 1.2  (line 91, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/speakers/widgets/speaker_list_card.dart
Issue : size: 20  (line 112, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/speakers/widgets/speaker_option_tile.dart
Issue : size: 40  (line 58, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 107, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/speakers/widgets/speaker_profile_header.dart
Issue : width: 42  (line 34, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 42  (line 35, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 42  (line 57, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 42  (line 57, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _NameLine extends StatelessWidget  (line 64, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/speakers/widgets/speaker_sessions.dart
Issue : size: 18  (line 77, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/speakers/widgets/speaker_sort_control.dart
Issue : size: 18  (line 45, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 58, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## splash feature


Issue file : src/Mobile/simf_app/lib/features/splash/splash_controller.dart
Issue : Duration(milliseconds: 1200)  (line 15, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(seconds: 5)  (line 78, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : Duration(seconds: 8)  (line 112, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const

Issue file : src/Mobile/simf_app/lib/features/splash/splash_screen.dart
Issue : size: 136  (line 51, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

## sponsors feature


Issue file : src/Mobile/simf_app/lib/features/sponsors/sponsor_detail_screen.dart
Issue : widget-building method _build() returning Widget  (line 70, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sponsors/widgets/sponsor_card.dart
Issue : maxLines: 2  (line 88, SIMF-C1)
Fix : a named layout const (never a value-named token)
Issue : size: 20  (line 120, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _BadgeBox extends StatelessWidget  (line 132, SIMF-C3)
Fix : its own file under widgets/
Issue : width: 53  (line 145, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 53  (line 146, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/sponsors/widgets/sponsor_grid.dart
Issue : crossAxisCount: 3  (line 33, SIMF-C1)
Fix : computed from core/responsive/breakpoints.dart
Issue : private widget _SponsorGridTile extends StatelessWidget  (line 54, SIMF-C3)
Fix : its own file under widgets/

## staff feature


Issue file : src/Mobile/simf_app/lib/features/staff/register_visitor_screen.dart
Issue : Duration(milliseconds: 250)  (line 439, SIMF-C1)
Fix : core/net/timeouts.dart or a feature policy const
Issue : widget-building method _buildBody() returning Widget  (line 642, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildLoadError() returning Widget  (line 677, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildForm() returning Widget  (line 707, SIMF-C3)
Fix : its own file under widgets/
Issue : maxLength: 100  (line 812, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 100  (line 827, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 50  (line 843, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)
Issue : maxLength: 10  (line 1083, SIMF-C1)
Fix : features/<f>/data/*_field_limits.dart (mirror backend MaxLength)

Issue file : src/Mobile/simf_app/lib/features/staff/staff_seating_screen.dart
Issue : private widget _DeskCard extends StatelessWidget  (line 307, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _DeskRow extends StatelessWidget  (line 327, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _OccupantHeader extends StatelessWidget  (line 351, SIMF-C3)
Fix : its own file under widgets/

## venuemap feature


Issue file : src/Mobile/simf_app/lib/features/venuemap/venue_map_screen.dart
Issue : widget-building method _buildBody() returning Widget  (line 234, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildError() returning Widget  (line 247, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildEmpty() returning Widget  (line 261, SIMF-C3)
Fix : its own file under widgets/
Issue : widget-building method _buildMap() returning Widget  (line 271, SIMF-C3)
Fix : its own file under widgets/
Issue : EdgeInsets.all(200)  (line 287, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : width: 80  (line 297, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/venuemap/widgets/venue_map_booth_sheet.dart
Issue : private widget _SubLine extends StatelessWidget  (line 95, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/venuemap/widgets/venue_map_controls.dart
Issue : size: 20  (line 37, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

Issue file : src/Mobile/simf_app/lib/features/venuemap/widgets/venue_map_info_card.dart
Issue : blurRadius: 8  (line 74, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 20  (line 144, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 159, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : private widget _LogoBadge extends StatelessWidget  (line 171, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/venuemap/widgets/venue_map_marker.dart
Issue : width: 34  (line 29, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : height: 34  (line 30, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)
Issue : size: 18  (line 42, SIMF-C1)
Fix : SimfTokens (semantic name, not a value-name)

