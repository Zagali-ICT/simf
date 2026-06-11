// SimfTextField Numeric mode (B3 — D-221 hardening). Strips every non-digit
// from a numeric field in place as the user types or pastes, so an ID / Iqama
// number field can never visually hold letters. This is the cosmetic layer:
// the C# @oninput handler keeps the *bound model* digit-only even if this
// script never loads, and the server-side FluentValidation rule is the final
// guarantee. The caret is kept roughly in place across the strip.
export function digitsOnly(elementId) {
  const el = document.getElementById(elementId);
  if (!el || el.dataset.simfDigits === '1') {
    return;
  }
  el.dataset.simfDigits = '1';
  el.addEventListener('input', () => {
    const cleaned = el.value.replace(/\D+/g, '');
    if (cleaned === el.value) {
      return;
    }
    const caretBefore = el.selectionStart ?? el.value.length;
    const removed = countNonDigits(el.value, caretBefore);
    el.value = cleaned;
    const caret = Math.max(0, caretBefore - removed);
    try {
      el.setSelectionRange(caret, caret);
    } catch {
      // setSelectionRange is unsupported on some input types — ignore.
    }
  });
}

function countNonDigits(value, upTo) {
  let count = 0;
  const limit = Math.min(upTo, value.length);
  for (let i = 0; i < limit; i++) {
    const c = value[i];
    if (c < '0' || c > '9') {
      count++;
    }
  }
  return count;
}
