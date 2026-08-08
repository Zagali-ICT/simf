import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../app/theme/tokens.dart';
import 'simf_field_label.dart';
import 'simf_field_style.dart';

/// A labeled SIMF form text field: a [SimfFieldLabel] above a [TextFormField]
/// using the shared `simfInputStyle` + `simfFieldDecoration` (maxLength counter
/// hidden). A field with a [validator] auto-validates on user interaction.
class SimfLabeledTextField extends StatelessWidget {
  const SimfLabeledTextField({
    required this.label,
    required this.controller,
    this.maxLength,
    this.keyboardType,
    this.textDirection,
    this.inputFormatters,
    this.validator,
    super.key,
  });

  final String label;
  final TextEditingController controller;
  final int? maxLength;
  final TextInputType? keyboardType;
  final TextDirection? textDirection;
  final List<TextInputFormatter>? inputFormatters;
  final FormFieldValidator<String>? validator;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(label),
        const SizedBox(height: SimfTokens.space2),
        // The visible [SimfFieldLabel] is a sibling Text, not the field's own
        // name, so a screen reader announced a bare "edit box" (BUG-012).
        // Attaching the label here names the field without changing the paint.
        Semantics(
          label: label,
          textField: true,
          child: TextFormField(
            controller: controller,
            maxLength: maxLength,
            keyboardType: keyboardType,
            textDirection: textDirection,
            style: simfInputStyle,
            autovalidateMode:
                validator != null ? AutovalidateMode.onUserInteraction : null,
            inputFormatters: inputFormatters,
            validator: validator,
            decoration: simfFieldDecoration(counterText: ''),
          ),
        ),
      ],
    );
  }
}
