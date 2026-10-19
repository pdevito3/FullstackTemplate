import { useState, useMemo } from 'react'
import { ArrowDown01Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { OperatorType, Filter, FilterOption } from '../types'
import { getOperatorLabel, Operators } from '../utils/operators'

interface MultiSelectFilterProps {
  propertyKey: string
  propertyLabel: string
  options: FilterOption[]
  onSubmit: (filter: Omit<Filter, 'id'>) => void
  initialFilter?: Filter // For editing existing filter
  allowedOperators?: OperatorType[]
}

export function MultiSelectFilter({
  propertyKey,
  propertyLabel,
  options,
  onSubmit,
  initialFilter,
  allowedOperators = [Operators.IN, Operators.NOT_IN],
}: MultiSelectFilterProps) {
  const [searchTerm, setSearchTerm] = useState('')
  const [selected, setSelected] = useState<string[]>(
    (initialFilter?.value as string[]) || []
  )
  const [operator, setOperator] = useState<OperatorType>(
    initialFilter?.operator || Operators.IN
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
    <div className="flex flex-col gap-2 w-64 p-2">
      {/* Operator selector */}
      <div className="flex flex-col gap-1">
        <label className="text-xs font-medium">Operator</label>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={<Button variant="outline" size="sm" className="w-full justify-between" />}
          >
            {getOperatorLabel(operator)}
            <HugeiconsIcon icon={ArrowDown01Icon} className="size-4 opacity-50" />
          </DropdownMenuTrigger>
          <DropdownMenuContent>
            {allowedOperators.map((op) => (
              <DropdownMenuItem key={op} onClick={() => setOperator(op)}>
                {getOperatorLabel(op)}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      {/* Match all toggle */}
      <Checkbox
        isSelected={matchAll}
        onChange={setMatchAll}
      >
        <span className="text-sm">All must match</span>
      </Checkbox>

      {/* Search input */}
      <Input
        placeholder="Filter options..."
        value={searchTerm}
        onChange={(e) => setSearchTerm(e.target.value)}
        autoFocus
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
              <Button
                variant="ghost"
                size="xs"
                className="h-6 px-2 text-xs opacity-0 group-hover:opacity-100 transition-opacity"
                onClick={() => selectOnly(option.value)}
              >
                Only
              </Button>
            </div>
          ))
        )}
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between pt-2 border-t">
        <Button variant="outline" size="sm" onClick={toggleAll}>
          {allRootSelected ? 'Deselect All' : 'Select All'}
        </Button>
        <Button size="sm" onClick={handleSubmit} disabled={selected.length === 0}>
          {initialFilter ? 'Update' : 'Add'} ({selected.length})
        </Button>
      </div>
    </div>
  )
}
