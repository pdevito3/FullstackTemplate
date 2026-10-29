/**
 * QueryKit cannot hold a quote at the start or the end of a text value. Its raw literal takes each quote at the start
 * as part of the delimiter, and it trims the quotes at the ends of a text value. Such a filter would match other text,
 * so the filter builder refuses the value.
 */
export function hasEndQuote(value: string): boolean {
  return value.startsWith('"') || value.endsWith('"')
}

const END_QUOTE_RULE = 'A filter value cannot start or end with a quote.'

export const TEXT_END_QUOTE_MESSAGE = `${END_QUOTE_RULE} Remove the quote, or choose Contains and type the text inside the quotes.`

export function optionsEndQuoteMessage(optionLabels: string[]): string {
  return optionLabels.length === 1
    ? `${END_QUOTE_RULE} Clear the option ${optionLabels[0]}.`
    : `${END_QUOTE_RULE} Clear the options ${optionLabels.join(', ')}.`
}
