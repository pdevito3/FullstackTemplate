import { useId, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { ResponsiveDialogFooter } from '@/components/ui/responsive-dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { isTextValue, type FilterControlProps, type OperatorType } from '../types'
import {
  baseOperator,
  caseInsensitiveOperator,
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
    initialOperator ? baseOperator(initialOperator) : getDefaultOperator('text')
  )

  const [value, setValue] = useState(isTextValue(initialFilter?.value) ? initialFilter.value : '')
  const [operator, setOperator] = useState<OperatorType>(initialBaseOperator)

  const [caseSensitive, setCaseSensitive] = useState(
    initialFilter ? !initialIsCaseInsensitive : false
  )

  const supportsCaseSensitivity = operatorSupportsCaseSensitivity(operator)
  const valueId = useId()

  const text = value.trim()

  const handleSubmit = () => {
    if (text === '') {
      return
    }

    // Convert operator based on case sensitivity
    // If case-insensitive, append * to operator (e.g., @= becomes @=*)
    const finalOperator = caseSensitive ? operator : (caseInsensitiveOperator(operator) ?? operator)

    onSubmit({
      propertyKey,
      propertyLabel,
      controlType: 'text',
      operator: finalOperator,
      value: text,
      caseSensitive,
    })
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
            value={value}
            onChange={(e) => setValue(e.target.value)}
            placeholder={`Enter ${propertyLabel.toLowerCase()}...`}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                handleSubmit()
              }
            }}
          />
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
