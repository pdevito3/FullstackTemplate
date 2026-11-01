/**
 * Puts text in a QueryKit literal. QueryKit reads an unquoted value as a name, a number, or `null`, and it has no
 * escape in `"..."`. So text with a quote goes in a raw literal: the text between runs of quotes that are longer than
 * each run in the text, and at least three long. One space pads each side, and QueryKit removes it, so a quote at the
 * start or the end of the text stays part of the text.
 */
export function quoteText(value: string): string {
  if (!value.includes('"')) {
    return `"${value}"`
  }
  const longestRun = Math.max(...(value.match(/"+/g) ?? []).map((run) => run.length))
  const delimiter = '"'.repeat(Math.max(3, longestRun + 1))
  return `${delimiter} ${value} ${delimiter}`
}

/**
 * Turns the text of a search box into a QueryKit filter that finds the rows whose field contains the text, with no
 * regard to case. Blank text adds no filter. Give the DTO as `T`, so that a field that the DTO does not have fails the
 * build.
 */
export function containsText<T>(field: Extract<keyof T, string>, text: string): string | undefined {
  const trimmed = text.trim()
  return trimmed ? `${field} @=* ${quoteText(trimmed)}` : undefined
}
