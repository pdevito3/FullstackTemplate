import type { OperatorType, ControlType, LogicalOperator } from '../types'

/**
 * Operator constants for fluent, type-safe operator references
 * Use these instead of magic strings throughout the codebase
 */
export const Operators = {
  // Equality operators
  EQUALS: '==' as OperatorType,
  NOT_EQUALS: '!=' as OperatorType,
  EQUALS_CASE_INSENSITIVE: '==*' as OperatorType,
  NOT_EQUALS_CASE_INSENSITIVE: '!=*' as OperatorType,

  // Comparison operators
  GREATER_THAN: '>' as OperatorType,
  LESS_THAN: '<' as OperatorType,
  GREATER_THAN_OR_EQUAL: '>=' as OperatorType,
  LESS_THAN_OR_EQUAL: '<=' as OperatorType,

  // String operators
  STARTS_WITH: '_=' as OperatorType,
  STARTS_WITH_CASE_INSENSITIVE: '_=*' as OperatorType,
  NOT_STARTS_WITH: '!_=' as OperatorType,
  NOT_STARTS_WITH_CASE_INSENSITIVE: '!_=*' as OperatorType,
  ENDS_WITH: '_-=' as OperatorType,
  ENDS_WITH_CASE_INSENSITIVE: '_-=*' as OperatorType,
  NOT_ENDS_WITH: '!_-=' as OperatorType,
  NOT_ENDS_WITH_CASE_INSENSITIVE: '!_-=*' as OperatorType,
  CONTAINS: '@=' as OperatorType,
  CONTAINS_CASE_INSENSITIVE: '@=*' as OperatorType,
  NOT_CONTAINS: '!@=' as OperatorType,
  NOT_CONTAINS_CASE_INSENSITIVE: '!@=*' as OperatorType,

  // Phonetic operators
  SOUNDS_LIKE: '~~' as OperatorType,
  NOT_SOUNDS_LIKE: '!~' as OperatorType,

  // Existence operators
  HAS: '^$' as OperatorType,
  HAS_CASE_INSENSITIVE: '^$*' as OperatorType,
  NOT_HAS: '!^$' as OperatorType,
  NOT_HAS_CASE_INSENSITIVE: '!^$*' as OperatorType,

  // Collection operators
  IN: '^^' as OperatorType,
  IN_CASE_INSENSITIVE: '^^*' as OperatorType,
  NOT_IN: '!^^' as OperatorType,
  NOT_IN_CASE_INSENSITIVE: '!^^*' as OperatorType,

  // Count operators
  COUNT_EQUALS: '#==' as OperatorType,
  COUNT_NOT_EQUALS: '#!=' as OperatorType,
  COUNT_GREATER_THAN: '#>' as OperatorType,
  COUNT_LESS_THAN: '#<' as OperatorType,
  COUNT_GREATER_THAN_OR_EQUAL: '#>=' as OperatorType,
  COUNT_LESS_THAN_OR_EQUAL: '#<=' as OperatorType,
} as const

/**
 * Logical operator constants for fluent, type-safe references
 */
export const LogicalOperators = {
  AND: 'AND' as LogicalOperator,
  OR: 'OR' as LogicalOperator,
} as const

export interface OperatorMetadata {
  symbol: OperatorType
  label: string
  appliesTo: ControlType[]
  requiresCaseSensitive?: boolean
  description: string
}

export const OPERATORS: Record<OperatorType, OperatorMetadata> = {
  // Equality operators
  '==': {
    symbol: '==',
    label: 'Equals',
    appliesTo: ['text', 'number', 'boolean'],
    description: 'Exact match (case-sensitive for text)',
  },
  '==*': {
    symbol: '==*',
    label: 'Equals',
    appliesTo: ['text'],
    requiresCaseSensitive: true,
    description: 'Exact match (case-insensitive)',
  },
  '!=': {
    symbol: '!=',
    label: 'Not Equals',
    appliesTo: ['text', 'number', 'boolean'],
    description: 'Does not match (case-sensitive for text)',
  },
  '!=*': {
    symbol: '!=*',
    label: 'Not Equals',
    appliesTo: ['text'],
    requiresCaseSensitive: true,
    description: 'Does not match (case-insensitive)',
  },

  // Comparison operators
  '>': {
    symbol: '>',
    label: 'Greater Than',
    appliesTo: ['number', 'date'],
    description: 'Value is greater than',
  },
  '<': {
    symbol: '<',
    label: 'Less Than',
    appliesTo: ['number', 'date'],
    description: 'Value is less than',
  },
  '>=': {
    symbol: '>=',
    label: 'Greater Than or Equal',
    appliesTo: ['number', 'date'],
    description: 'Value is greater than or equal to',
  },
  '<=': {
    symbol: '<=',
    label: 'Less Than or Equal',
    appliesTo: ['number', 'date'],
    description: 'Value is less than or equal to',
  },

  // String operators
  '_=': {
    symbol: '_=',
    label: 'Starts With',
    appliesTo: ['text'],
    description: 'Starts with (case-sensitive)',
  },
  '_=*': {
    symbol: '_=*',
    label: 'Starts With',
    appliesTo: ['text'],
    requiresCaseSensitive: true,
    description: 'Starts with (case-insensitive)',
  },
  '!_=': {
    symbol: '!_=',
    label: 'Does Not Start With',
    appliesTo: ['text'],
    description: 'Does not start with (case-sensitive)',
  },
  '!_=*': {
    symbol: '!_=*',
    label: 'Does Not Start With',
    appliesTo: ['text'],
    requiresCaseSensitive: true,
    description: 'Does not start with (case-insensitive)',
  },
  '_-=': {
    symbol: '_-=',
    label: 'Ends With',
    appliesTo: ['text'],
    description: 'Ends with (case-sensitive)',
  },
  '_-=*': {
    symbol: '_-=*',
    label: 'Ends With',
    appliesTo: ['text'],
    requiresCaseSensitive: true,
    description: 'Ends with (case-insensitive)',
  },
  '!_-=': {
    symbol: '!_-=',
    label: 'Does Not End With',
    appliesTo: ['text'],
    description: 'Does not end with (case-sensitive)',
  },
  '!_-=*': {
    symbol: '!_-=*',
    label: 'Does Not End With',
    appliesTo: ['text'],
    requiresCaseSensitive: true,
    description: 'Does not end with (case-insensitive)',
  },
  '@=': {
    symbol: '@=',
    label: 'Contains',
    appliesTo: ['text'],
    description: 'Contains (case-sensitive)',
  },
  '@=*': {
    symbol: '@=*',
    label: 'Contains',
    appliesTo: ['text'],
    requiresCaseSensitive: true,
    description: 'Contains (case-insensitive)',
  },
  '!@=': {
    symbol: '!@=',
    label: 'Does Not Contain',
    appliesTo: ['text'],
    description: 'Does not contain (case-sensitive)',
  },
  '!@=*': {
    symbol: '!@=*',
    label: 'Does Not Contain',
    appliesTo: ['text'],
    requiresCaseSensitive: true,
    description: 'Does not contain (case-insensitive)',
  },

  // Phonetic operators
  '~~': {
    symbol: '~~',
    label: 'Sounds Like',
    appliesTo: ['text'],
    description: 'Phonetically similar to',
  },
  '!~': {
    symbol: '!~',
    label: 'Does Not Sound Like',
    appliesTo: ['text'],
    description: 'Not phonetically similar to',
  },

  // Existence operators
  '^$': {
    symbol: '^$',
    label: 'Has',
    appliesTo: ['text', 'multiselect'],
    description: 'Has value (case-sensitive)',
  },
  '^$*': {
    symbol: '^$*',
    label: 'Has',
    appliesTo: ['text', 'multiselect'],
    requiresCaseSensitive: true,
    description: 'Has value (case-insensitive)',
  },
  '!^$': {
    symbol: '!^$',
    label: 'Does Not Have',
    appliesTo: ['text', 'multiselect'],
    description: 'Does not have value (case-sensitive)',
  },
  '!^$*': {
    symbol: '!^$*',
    label: 'Does Not Have',
    appliesTo: ['text', 'multiselect'],
    requiresCaseSensitive: true,
    description: 'Does not have value (case-insensitive)',
  },

  // Collection operators
  '^^': {
    symbol: '^^',
    label: 'In',
    appliesTo: ['multiselect'],
    description: 'In array (case-sensitive)',
  },
  '^^*': {
    symbol: '^^*',
    label: 'In',
    appliesTo: ['multiselect'],
    requiresCaseSensitive: true,
    description: 'In array (case-insensitive)',
  },
  '!^^': {
    symbol: '!^^',
    label: 'Not In',
    appliesTo: ['multiselect'],
    description: 'Not in array (case-sensitive)',
  },
  '!^^*': {
    symbol: '!^^*',
    label: 'Not In',
    appliesTo: ['multiselect'],
    requiresCaseSensitive: true,
    description: 'Not in array (case-insensitive)',
  },

  // Count operators
  '#==': {
    symbol: '#==',
    label: 'Count Equals',
    appliesTo: ['number'],
    description: 'Count equals',
  },
  '#!=': {
    symbol: '#!=',
    label: 'Count Not Equals',
    appliesTo: ['number'],
    description: 'Count not equals',
  },
  '#>': {
    symbol: '#>',
    label: 'Count Greater Than',
    appliesTo: ['number'],
    description: 'Count greater than',
  },
  '#<': {
    symbol: '#<',
    label: 'Count Less Than',
    appliesTo: ['number'],
    description: 'Count less than',
  },
  '#>=': {
    symbol: '#>=',
    label: 'Count Greater Than or Equal',
    appliesTo: ['number'],
    description: 'Count greater than or equal',
  },
  '#<=': {
    symbol: '#<=',
    label: 'Count Less Than or Equal',
    appliesTo: ['number'],
    description: 'Count less than or equal',
  },
}

/**
 * Get operators available for a specific control type
 */
export function getOperatorsForControlType(controlType: ControlType): OperatorMetadata[] {
  return Object.values(OPERATORS).filter((op) => op.appliesTo.includes(controlType))
}

/**
 * Get the default operator for a control type
 */
export function getDefaultOperator(controlType: ControlType): OperatorType {
  switch (controlType) {
    case 'text':
      return Operators.CONTAINS // Contains (case-sensitive by default)
    case 'multiselect':
      return Operators.IN
    case 'date':
      return Operators.EQUALS
    case 'number':
      return Operators.EQUALS
    case 'boolean':
      return Operators.EQUALS
  }
}

/**
 * Get the human-readable label for an operator
 */
export function getOperatorLabel(operator: OperatorType): string {
  return OPERATORS[operator]?.label || operator
}

/**
 * Check if an operator requires case-sensitivity handling
 */
export function operatorSupportsCaseSensitivity(operator: OperatorType): boolean {
  return OPERATORS[operator]?.requiresCaseSensitive === true
}
