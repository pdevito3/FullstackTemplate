import type { Filter } from '../types'

/**
 * A text value that QueryKit cannot hold:
 * - `endQuote`: its raw literal takes each quote at the start as part of the delimiter, and it trims the quotes at the
 *   ends of a text value. So the filter matches other text.
 * - `brackets`: it reads a value that starts with `[` and ends with `]` as a list, also in quotes. So it refuses the
 *   filter.
 */
export type TextValueFault = 'endQuote' | 'brackets'

const RULES: Record<TextValueFault, string> = {
  endQuote: 'A filter value cannot start or end with a quote.',
  brackets: 'A filter value cannot start with [ and end with ].',
}

const TEXT_FIXES: Record<TextValueFault, string> = {
  endQuote: 'Remove the quote at the start or the end.',
  brackets: 'Remove the [ at the start or the ] at the end.',
}

export function hasEndQuote(value: string): boolean {
  return value.startsWith('"') || value.endsWith('"')
}

export function textValueFault(value: string): TextValueFault | undefined {
  if (hasEndQuote(value)) return 'endQuote'
  if (value.startsWith('[') && value.endsWith(']')) return 'brackets'
  return undefined
}

/**
 * A list item can start with `[` and end with `]`, because QueryKit splits the list before it reads an item. So only an
 * end quote is a fault there.
 */
export function filterValueFault(filter: Filter): TextValueFault | undefined {
  switch (filter.controlType) {
    case 'text':
      return textValueFault(filter.value as string)
    case 'multiselect':
      return (filter.value as string[]).some(hasEndQuote) ? 'endQuote' : undefined
    default:
      return undefined
  }
}

export function textValueMessage(fault: TextValueFault): string {
  return `${RULES[fault]} ${TEXT_FIXES[fault]}`
}

export function optionsEndQuoteMessage(optionLabels: string[]): string {
  return optionLabels.length === 1
    ? `${RULES.endQuote} Clear the option ${optionLabels[0]}.`
    : `${RULES.endQuote} Clear the options ${optionLabels.join(', ')}.`
}

export function unusedFilterMessage(fault: TextValueFault): string {
  return `This filter is not used. ${RULES[fault]} Edit the filter, then change the value.`
}
