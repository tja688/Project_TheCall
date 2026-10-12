// Presentation-only spans. All adjusted values come from PageExports.Preview.
export function spanParts(spans) {
  return spans.map(span => ({
    text: Object.prototype.hasOwnProperty.call(span, "energy") ? String(span.energy) : span.text,
    live: false,
  }));
}
