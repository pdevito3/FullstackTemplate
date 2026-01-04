import { useState } from 'react'
import { Button } from '@/components/ui/button'
import type { Filter } from '../types'
import { Operators } from '../utils/operators'

interface BooleanFilterProps {
  propertyKey: string
  propertyLabel: string
  onSubmit: (filter: Omit<Filter, 'id'>) => void
  initialFilter?: Filter
}

export function BooleanFilter({
  propertyKey,
  propertyLabel,
  onSubmit,
  initialFilter,
}: BooleanFilterProps) {
  const [_value, setValue] = useState<boolean>(
    (initialFilter?.value as boolean) ?? true
  )

  const handleSubmit = (selectedValue: boolean) => {
    setValue(selectedValue)
    onSubmit({
      propertyKey,
      propertyLabel,
      controlType: 'boolean',
      operator: Operators.EQUALS,
      value: selectedValue,
    } as Omit<Filter, 'id'>)
  }

  return (
    <div className="flex flex-col gap-2 w-48 p-3">
      <div className="text-sm font-medium mb-2">{propertyLabel}</div>
      <div className="flex gap-2">
        <Button
          variant="outline"
          onClick={() => handleSubmit(true)}
          className="flex-1"
        >
          True
        </Button>
        <Button
          variant="outline"
          onClick={() => handleSubmit(false)}
          className="flex-1"
        >
          False
        </Button>
      </div>
    </div>
  )
}
