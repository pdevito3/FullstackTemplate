import { useId } from 'react'
import { Button } from '@/components/ui/button'
import type { Filter, FilterControlProps } from '../types'
import { Operators } from '../utils/operators'

type BooleanFilterProps = Pick<FilterControlProps, 'config' | 'initialFilter' | 'onSubmit'>

/** A click on a value submits it. */
export function BooleanFilter({ config, initialFilter, onSubmit }: BooleanFilterProps) {
  const { propertyKey, propertyLabel } = config
  // Only the edit dialog has a current value. In the Add Filter menu the buttons are plain buttons, not toggles.
  // A "Not Equals True" filter matches the rows that are false, so False is its current value.
  const currentValue = initialFilter?.operator === Operators.NOT_EQUALS ? !initialFilter.value : initialFilter?.value
  const isPressed = (value: boolean) => (initialFilter ? currentValue === value : undefined)
  const labelId = useId()
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
    <div role="group" aria-labelledby={labelId} className="flex flex-col gap-2 w-full md:w-48 p-3">
      <div id={labelId} className="text-sm font-medium mb-2 in-data-[slot=responsive-dialog]:sr-only">{propertyLabel}</div>
      <div className="flex gap-2">
        <Button
          variant={isPressed(true) ? 'default' : 'outline'}
          aria-pressed={isPressed(true)}
          onPress={() => handleSubmit(true)}
          className="flex-1"
        >
          True
        </Button>
        <Button
          variant={isPressed(false) ? 'default' : 'outline'}
          aria-pressed={isPressed(false)}
          onPress={() => handleSubmit(false)}
          className="flex-1"
        >
          False
        </Button>
      </div>
    </div>
  )
}
