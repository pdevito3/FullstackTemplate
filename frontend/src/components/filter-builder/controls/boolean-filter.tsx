import { Button } from '@/components/ui/button'
import type { Filter, FilterControlProps } from '../types'
import { Operators } from '../utils/operators'

// A click on a value submits, so the control does not read initialFilter.
type BooleanFilterProps = Pick<FilterControlProps, 'config' | 'onSubmit'>

export function BooleanFilter({ config, onSubmit }: BooleanFilterProps) {
  const { propertyKey, propertyLabel } = config
  const handleSubmit = (selectedValue: boolean) => {
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
          onPress={() => handleSubmit(true)}
          className="flex-1"
        >
          True
        </Button>
        <Button
          variant="outline"
          onPress={() => handleSubmit(false)}
          className="flex-1"
        >
          False
        </Button>
      </div>
    </div>
  )
}
