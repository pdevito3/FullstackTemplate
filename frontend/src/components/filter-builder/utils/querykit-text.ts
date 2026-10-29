/** QueryKit cannot keep a quote at the start or the end of a value, so a filter uses the text between them. */
export function withoutEndQuotes(value: string): string {
  return value.replace(/^"+|"+$/g, '')
}
