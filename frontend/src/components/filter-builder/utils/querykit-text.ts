import type { Filter } from '../types'

/**
 * A text value that QueryKit cannot hold. Its raw literal takes each quote at the start or the end as part of the
 * delimiter, so the filter matches other text.
 */
export type TextValueFault = 'endQuote'

const RULES: Record<TextValueFault, string> = {
  endQuote: 'A filter value cannot start or end with a quote.',
}

const TEXT_FIXES: Record<TextValueFault, string> = {
  endQuote: 'Remove the quote at the start or the end.',
}

export function hasEndQuote(value: string): boolean {
  return value.startsWith('"') || value.endsWith('"')
}

export function textValueFault(value: string): TextValueFault | undefined {
  return hasEndQuote(value) ? 'endQuote' : undefined
}

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
