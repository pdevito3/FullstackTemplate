import { useId, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { ResponsiveDialogFooter } from '@/components/ui/responsive-dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { Filter, FilterControlProps, OperatorType } from '../types'
import { textValueFault, textValueMessage } from '../utils/querykit-text'
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
  const fault = textValueFault(text)
  // A quote or a bracket at the end can start one inside the text, so the message waits until the user asks to add the
  // filter. On a blur it would move the buttons under a press, and the click would miss them.
  const [checked, setChecked] = useState(fault !== undefined)
  const showFault = fault !== undefined && (checked || text.startsWith('"'))

  const handleSubmit = () => {
    setChecked(true)
    if (text === '' || fault) {
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
            aria-invalid={showFault || undefined}
            aria-describedby={showFault ? errorId : undefined}
            value={value}
            onChange={(e) => {
              setValue(e.target.value)
              setChecked(false)
            }}
            placeholder={`Enter ${propertyLabel.toLowerCase()}...`}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                handleSubmit()
              }
            }}
          />
          {showFault && (
            <p id={errorId} role="alert" className="text-destructive text-sm">
              {textValueMessage(fault)}
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
        <Button onPress={handleSubmit} isDisabled={text === ''}>
          {initialFilter ? 'Update' : 'Add'} Filter
        </Button>
      </ResponsiveDialogFooter>
    </div>
  )
}
