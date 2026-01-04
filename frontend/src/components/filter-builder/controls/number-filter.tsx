import { useState } from 'react'
import { ArrowDown01Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { OperatorType, Filter } from '../types'
import { getOperatorsForControlType, getOperatorLabel, Operators } from '../utils/operators'

interface NumberFilterProps {
  propertyKey: string
  propertyLabel: string
  onSubmit: (filter: Omit<Filter, 'id'>) => void
  allowedOperators?: OperatorType[]
  initialFilter?: Filter
}

export function NumberFilter({
  propertyKey,
  propertyLabel,
  onSubmit,
  allowedOperators,
  initialFilter,
}: NumberFilterProps) {
  const numberOperators = getOperatorsForControlType('number')
  const availableOperators = allowedOperators
    ? numberOperators.filter((op) => allowedOperators.includes(op.symbol))
    : numberOperators

  const [value, setValue] = useState(initialFilter?.value?.toString() || '')
  const [operator, setOperator] = useState<OperatorType>(
    initialFilter?.operator || availableOperators[0]?.symbol || Operators.COUNT_EQUALS
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

  const selectedOperatorLabel = getOperatorLabel(operator)
  const isValid = value.trim() !== '' && !isNaN(parseFloat(value))

  return (
    <div className="flex flex-col gap-3 w-64 p-3">
      <div className="flex flex-col gap-2">
        <Label>Operator</Label>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={<Button variant="outline" className="w-full justify-between" size="sm" />}
          >
            {selectedOperatorLabel}
            <HugeiconsIcon icon={ArrowDown01Icon} className="size-4 opacity-50" />
          </DropdownMenuTrigger>
          <DropdownMenuContent className="w-full">
            {availableOperators.map((op) => (
              <DropdownMenuItem
                key={op.symbol}
                onClick={() => setOperator(op.symbol)}
              >
                {op.label}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <div className="flex flex-col gap-2">
        <Label htmlFor="value">Value</Label>
        <Input
          id="value"
          type="number"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder={`Enter ${propertyLabel.toLowerCase()}...`}
          autoFocus
          onKeyDown={(e) => {
            if (e.key === 'Enter' && isValid) {
              handleSubmit()
            }
          }}
        />
      </div>

      <Button onClick={handleSubmit} disabled={!isValid} className="w-full">
        {initialFilter ? 'Update' : 'Add'} Filter
      </Button>
    </div>
  )
}
