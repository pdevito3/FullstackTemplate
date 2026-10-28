import { useState, useMemo } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import type { OperatorType, Filter, FilterControlProps, FilterOption } from '../types'
import { getDefaultOperator, Operators, pickOperator } from '../utils/operators'
import { FilterControlFooter } from './filter-control-footer'
import { OperatorSelect } from './operator-select'

// A stable empty list, so that the memos below do not run again on each render.
const NO_OPTIONS: FilterOption[] = []
const DEFAULT_OPERATORS: OperatorType[] = [Operators.IN, Operators.NOT_IN]

export function MultiSelectFilter({
  config,
  surface,
  initialFilter,
  onSubmit,
  autoFocus = false,
}: FilterControlProps) {
  const {
    propertyKey,
    propertyLabel,
    options = NO_OPTIONS,
    operators: allowedOperators = DEFAULT_OPERATORS,
  } = config
  const [searchTerm, setSearchTerm] = useState('')
  const [selected, setSelected] = useState<string[]>(
    (initialFilter?.value as string[]) || []
  )
  const [operator, setOperator] = useState<OperatorType>(
    pickOperator(allowedOperators, initialFilter?.operator ?? getDefaultOperator('multiselect'))
  )
  const [matchAll, setMatchAll] = useState(initialFilter?.matchAll || false)

  const filteredOptions = useMemo(() => {
    if (!searchTerm) return options
    const lower = searchTerm.toLowerCase()
    return options.filter(
      (opt) =>
        opt.label.toLowerCase().includes(lower) ||
        opt.value.toLowerCase().includes(lower)
    )
  }, [options, searchTerm])

  // Only consider root-level (non-nested) options for "Select All"
  const rootOptions = useMemo(() => options.filter(opt => !opt.isNested), [options])
  const allRootSelected = rootOptions.length > 0 && rootOptions.every(opt => selected.includes(opt.value))

  const toggleOption = (value: string) => {
    setSelected((prev) =>
      prev.includes(value) ? prev.filter((v) => v !== value) : [...prev, value]
    )
  }

  const selectOnly = (value: string) => {
    setSelected([value])
  }

  const toggleAll = () => {
    if (allRootSelected) {
      // Deselect all root options, but keep any nested options that were selected
      const nestedValues = options.filter(opt => opt.isNested).map(opt => opt.value)
      setSelected(selected.filter(v => nestedValues.includes(v)))
    } else {
      // Select all root options, keeping any already-selected nested options
      const rootValues = rootOptions.map(opt => opt.value)
      const nestedSelected = selected.filter(v => !rootValues.includes(v))
      setSelected([...rootValues, ...nestedSelected])
    }
  }

  const handleSubmit = () => {
    if (selected.length === 0) {
      return
    }

    // Get labels for the selected values
    const selectedLabels = selected.map((value) => {
      const option = options.find((opt) => opt.value === value)
      return option?.label ?? value
    })

    onSubmit({
      propertyKey,
      propertyLabel,
      controlType: 'multiselect',
      operator,
      value: selected,
      selectedLabels,
      matchAll: matchAll || undefined,
    } as Omit<Filter, 'id'>)
  }

  return (
    <div className="flex flex-col w-full md:w-64 p-2">
      <div className="flex flex-col gap-2">
        {/* Operator selector */}
        <OperatorSelect
          operators={allowedOperators}
          value={operator}
          onChange={setOperator}
          size="sm"
        />

        {/* Match all toggle, and Select All for the list below */}
        <div className="flex items-center justify-between gap-2">
          <Checkbox
            isSelected={matchAll}
            onChange={setMatchAll}
          >
            <span className="text-sm">All must match</span>
          </Checkbox>
          <Button variant="outline" size="sm" onPress={toggleAll}>
            {allRootSelected ? 'Deselect All' : 'Select All'}
          </Button>
        </div>

        {/* Search input. Not autoFocus by default: in the Add Filter submenu it takes focus from the menu and breaks keyboard navigation. */}
        <Input
          autoFocus={autoFocus}
          aria-label="Filter options"
          placeholder="Filter options..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="h-8"
        />

        {/* Options list */}
        <div className="max-h-48 overflow-y-auto border rounded-md">
          {filteredOptions.length === 0 ? (
            <div className="p-4 text-sm text-muted-foreground text-center">
              No options found
            </div>
          ) : (
            filteredOptions.map((option) => (
              <div
                key={option.value}
                className="flex items-center justify-between px-2 py-1.5 hover:bg-accent group"
              >
                <Checkbox
                  isSelected={selected.includes(option.value)}
                  onChange={() => toggleOption(option.value)}
                  className="flex-1"
                >
                  <span className="text-sm">{option.label}</span>
                </Checkbox>
                {/* A touch screen has no hover, and a hidden button there still takes a tap, so it always shows. */}
                <Button
                  variant="ghost"
                  size="xs"
                  className="h-6 px-2 text-xs opacity-0 group-hover:opacity-100 focus-visible:opacity-100 any-pointer-coarse:opacity-100 transition-opacity"
                  aria-label={`Only ${option.label}`}
                  onPress={() => selectOnly(option.value)}
                >
                  Only
                </Button>
              </div>
            ))
          )}
        </div>
      </div>

      <FilterControlFooter surface={surface}>
        <Button onPress={handleSubmit} isDisabled={selected.length === 0} className="w-full">
          {initialFilter ? 'Update' : 'Add'} ({selected.length})
        </Button>
      </FilterControlFooter>
    </div>
  )
}
