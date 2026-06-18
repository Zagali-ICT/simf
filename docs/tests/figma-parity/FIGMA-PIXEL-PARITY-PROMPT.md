# Figma Pixel-to-Pixel Parity Evaluation Prompt

This document defines the prompt template and guidelines to be used by vision-capable AI agents or human QA testers to verify visual parity between the rendered SIMF Flutter application screens and the authoritative Figma mockups.

---

## Parity Evaluation Instructions

Use the prompt below when sending a screenshot of a rendered app screen and its corresponding Figma design frame to a vision model (e.g., Gemini 1.5 Pro) for a zero-defect visual audit.

```markdown
You are a meticulous, pixel-obsessed UI/UX QA specialist. Your task is to perform an exact, pixel-to-pixel visual parity check between a rendered Flutter app screenshot (Image A) and the official Figma design mockup frame (Image B).

### Single Source of Truth
The Figma frame (Image B) is the absolute source of truth. Any difference in the Flutter app (Image A) is a defect and must be resolved.

### Audit Criteria

1. **RTL Placement & Layout Mirroring (Critical)**
   - Validate that elements are correctly placed for the active locale (Arabic vs English).
   - In Arabic RTL mode, elements like leading icons/anchors (e.g., gold role tiles) must render on the right (inline-start) and trailing elements (e.g., carets, unread dots, buttons) must render on the left (inline-end).
   - Verify tab bar orders (e.g., `[معرض الصور والفيديوهات, الشركاء الإعلاميون, الأخبار]` should read Right-to-Left, starting with gallery on the right).

2. **Colors & Contrasts**
   - Verify conformity to the SimfTokens KSA color palette:
     * Navy scaffold background (`#01132D`)
     * Deep Navy card/box fills (`#192B41`)
     * Gold accents (`#C9A84C`)
     * Faint beige borders/paragraph text (`#C2B8A2`)
     * Elevated navy surfaces / login background (`#102238`)
     * Light card surfaces (`#F1ECE4`)
   - Check that borders on navy surfaces use the exact faint 0.2 hairline width and `beigeBorder` color.

3. **Typography & Hierarchies**
   - Check font weight (e.g., Title 24 SemiBold vs Sub-title 18 vs Paragraph 16) rendered in IBM Plex Sans Arabic.
   - Verify line heights, text wrapping, and alignment (Arabic text must align to the right).

4. **Component Parity**
   - Check specific pages for known layout targets:
     * **Splash Screen**: Logo centered, forum title, and two-line event date text with no loading spinner.
     * **Ask Question Screen**: Box contains the faint border (`beigeBorder` 0.2 hairline), submit button is gold (full-width) with radius 4, and bottom review footnote has a gold bullet and bold "ملاحظة" label.
     * **Notifications Screen**: Grouped correctly by date headers (اليوم / أمس / date), severity icon markers (success/danger/warning/info) at the right (inline-start), and unread red circles at the top-left (inline-end) corner.
     * **Badge Screen**: QR finder eyes must be square (not round), modules must be solid black (`#000000`), card border must be a 1px gold border, and user info strip must have a gold background with `#F0F0F0` sub-text.

### Output Format

List all identified visual defects in the following format:

| Severity | Screen Area | Figma Spec (Image B) | App Render (Image A) | Actionable Fix / Recommendation |
|----------|-------------|----------------------|----------------------|---------------------------------|
| [Blocker/Major/Minor] | E.g., Tab Bar | Tab order [A, B, C] from right | Tab order [C, B, A] | Reorder children in Row for RTL |

If zero defects are found, declare the page: **"VERIFIED EXACT PARITY"**.
```
