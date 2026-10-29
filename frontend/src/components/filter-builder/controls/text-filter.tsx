import { useId, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { ResponsiveDialogFooter } from '@/components/ui/responsive-dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { Filter, FilterControlProps, OperatorType } from '../types'
import { hasEndQuote, TEXT_END_QUOTE_MESSAGE } from '../utils/querykit-text'
import {
  getDefaultOperator,
  getOperatorsForControlType,
  operatorSupportsCaseSensitivity,
  pickOperator,
} from '../utils/operators'
import { OperatorSelect } from './operator-select'

/** The text filter form. It shows in a dialog, so it has a Cancel button. */
export function TextFilter({
  config,
  initialFilter,
  onSubmit,
  onCancel,
  autoFocus = false,
}: FilterControlProps) {
  const { propertyKey, propertyLabel, operators: allowedOperators } = config
  const textOperators = getOperatorsForControlType('text')
  // Filter to only show base operators (without * suffix) - the checkbox controls case sensitivity
  const baseOperators = textOperators.filter((op) => !op.symbol.endsWith('*'))
  const availableOperators = (
    allowedOperators
      ? baseOperators.filter((op) => allowedOperators.includes(op.symbol))
      : baseOperators
  ).map((op) => op.symbol)

  // Extract base operator and case sensitivity from initial filter
  const initialOperator = initialFilter?.operator
  const initialIsCaseInsensitive = initialOperator?.endsWith('*') ?? false
  const initialBaseOperator = pickOperator(
    availableOperators,
    initialOperator
      ? ((initialIsCaseInsensitive ? initialOperator.slice(0, -1) : initialOperator) as OperatorType)
      : getDefaultOperator('text')
  )

  const [value, setValue] = useState((initialFilter?.value as string) || '')
  const [operator, setOperator] = useState<OperatorType>(initialBaseOperator)
  const [caseSensitive, setCaseSensitive] = useState(
    initialFilter ? !initialIsCaseInsensitive : false
  )
  const supportsCaseSensitivity = operatorSupportsCaseSensitivity(operator)
  const valueId = useId()
  const errorId = useId()

  const text = value.trim()
  const endQuote = hasEndQuote(text)
  const canSubmit = text !== '' && !endQuote

  const handleSubmit = () => {
    if (!canSubmit) {
      return
    }

    // Convert operator based on case sensitivity
    // If case-insensitive, append * to operator (e.g., @= becomes @=*)
    const finalOperator = caseSensitive || !supportsCaseSensitivity
      ? operator
      : (`${operator}*` as OperatorType)

    onSubmit({
      propertyKey,
      propertyLabel,
      controlType: 'text',
      operator: finalOperator,
      value: text,
      caseSensitive,
    } as Omit<Filter, 'id'>)
  }

  return (
    <div>
      <div className="flex flex-col gap-4">
        <OperatorSelect
          operators={availableOperators}
          value={operator}
          onChange={setOperator}
        />

        <div className="flex flex-col gap-2">
          <Label htmlFor={valueId}>Value</Label>
          <Input
            autoFocus={autoFocus}
            id={valueId}
            aria-invalid={endQuote || undefined}
            aria-describedby={endQuote ? errorId : undefined}
            value={value}
            onChange={(e) => setValue(e.target.value)}
            placeholder={`Enter ${propertyLabel.toLowerCase()}...`}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                handleSubmit()
              }
            }}
          />
          {endQuote && (
            <p id={errorId} role="alert" className="text-destructive text-sm">
              {TEXT_END_QUOTE_MESSAGE}
            </p>
          )}
        </div>

        {supportsCaseSensitivity && (
          <Checkbox
            isSelected={caseSensitive}
            onChange={setCaseSensitive}
          >
            Case sensitive
          </Checkbox>
        )}
      </div>

      <ResponsiveDialogFooter>
        <Button variant="outline" onPress={onCancel}>
          Cancel
        </Button>
        <Button onPress={handleSubmit} isDisabled={!canSubmit}>
          {initialFilter ? 'Update' : 'Add'} Filter
        </Button>
      </ResponsiveDialogFooter>
    </div>
  )
}
