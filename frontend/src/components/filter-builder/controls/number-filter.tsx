import { useId, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { OperatorType, Filter, FilterControlProps } from '../types'
import { getDefaultOperator, getOperatorsForControlType, pickOperator } from '../utils/operators'
import { OperatorSelect } from './operator-select'

export function NumberFilter({
  config,
  initialFilter,
  onSubmit,
  autoFocus = false,
}: FilterControlProps) {
  const { propertyKey, propertyLabel, operators: allowedOperators } = config
  const numberOperators = getOperatorsForControlType('number')
  const availableOperators = (
    allowedOperators
      ? numberOperators.filter((op) => allowedOperators.includes(op.symbol))
      : numberOperators
  ).map((op) => op.symbol)

  const [value, setValue] = useState(initialFilter?.value?.toString() || '')
  const valueId = useId()
  const [operator, setOperator] = useState<OperatorType>(
    pickOperator(availableOperators, initialFilter?.operator ?? getDefaultOperator('number'))
  )

  const handleSubmit = () => {
    const numValue = parseFloat(value)
    if (isNaN(numValue)) {
      return
    }

    onSubmit({
      propertyKey,
      propertyLabel,
      controlType: 'number',
      operator,
      value: numValue,
    } as Omit<Filter, 'id'>)
  }

  const isValid = value.trim() !== '' && !isNaN(parseFloat(value))

  return (
    <div className="flex flex-col gap-3 w-full md:w-64 p-3">
      <OperatorSelect
        operators={availableOperators}
        value={operator}
        onChange={setOperator}
        size="sm"
      />

      <div className="flex flex-col gap-2">
        <Label htmlFor={valueId}>Value</Label>
        <Input
          id={valueId}
          type="number"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder={`Enter ${propertyLabel.toLowerCase()}...`}
          autoFocus={autoFocus}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && isValid) {
              handleSubmit()
            }
          }}
        />
      </div>

      <Button onPress={handleSubmit} isDisabled={!isValid} className="w-full">
        {initialFilter ? 'Update' : 'Add'} Filter
      </Button>
    </div>
  )
}
