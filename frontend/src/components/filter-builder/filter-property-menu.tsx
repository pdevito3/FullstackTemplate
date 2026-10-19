import { useState, useMemo } from 'react'
import { Add01Icon, TextIcon, ArrowRight01Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import { cn } from '@/lib/utils'
import type { FilterConfig, Filter } from './types'
import { TextFilterModal } from './controls/text-filter-modal'
import { MultiSelectFilter } from './controls/multiselect-filter'
import { DateFilter } from './controls/date-filter'
import { NumberFilter } from './controls/number-filter'
import { BooleanFilter } from './controls/boolean-filter'

interface FilterPropertyMenuProps {
  options: FilterConfig[]
  onAddFilter: (filter: Omit<Filter, 'id'>) => void
}

export function FilterPropertyMenu({ options, onAddFilter }: FilterPropertyMenuProps) {
  const [searchTerm, setSearchTerm] = useState('')
  const [popoverOpen, setPopoverOpen] = useState(false)
  const [textFilterModalOpen, setTextFilterModalOpen] = useState(false)
  const [selectedTextProperty, setSelectedTextProperty] = useState<FilterConfig | null>(null)
  // For submenu popover
  const [selectedProperty, setSelectedProperty] = useState<FilterConfig | null>(null)
  const [submenuOpen, setSubmenuOpen] = useState(false)

  const filteredOptions = useMemo(() => {
    if (!searchTerm) return options
    const lower = searchTerm.toLowerCase()
    return options.filter(
      (opt) =>
        opt.propertyLabel.toLowerCase().includes(lower) ||
        opt.propertyKey.toLowerCase().includes(lower)
    )
  }, [options, searchTerm])

  const handleTextPropertyClick = (config: FilterConfig) => {
    setSelectedTextProperty(config)
    setTextFilterModalOpen(true)
    setPopoverOpen(false)
    setSearchTerm('')
    setSelectedProperty(null)
    setSubmenuOpen(false)
  }

  const handlePropertyClick = (config: FilterConfig) => {
    // Text filters use a modal
    if (config.controlType === 'text') {
      handleTextPropertyClick(config)
      return
    }
    // Other types open a submenu popover
    if (selectedProperty?.propertyKey === config.propertyKey && submenuOpen) {
      // Clicking same item closes submenu
      setSubmenuOpen(false)
      setSelectedProperty(null)
    } else {
      setSelectedProperty(config)
      setSubmenuOpen(true)
    }
  }

  const handleTextFilterSubmit = (filter: Omit<Filter, 'id'>) => {
    onAddFilter(filter)
    setTextFilterModalOpen(false)
    setSelectedTextProperty(null)
  }

  const handleFilterSubmit = (filter: Omit<Filter, 'id'>) => {
    onAddFilter(filter)
    setPopoverOpen(false)
    setSubmenuOpen(false)
    setSearchTerm('')
    setSelectedProperty(null)
  }

  const handlePopoverOpenChange = (open: boolean) => {
    setPopoverOpen(open)
    if (!open) {
      setSearchTerm('')
      setSelectedProperty(null)
      setSubmenuOpen(false)
    }
  }

  const handleSubmenuOpenChange = (open: boolean) => {
    setSubmenuOpen(open)
    if (!open) {
      setSelectedProperty(null)
    }
  }

  return (
    <>
      <Popover open={popoverOpen} onOpenChange={handlePopoverOpenChange}>
        <PopoverTrigger
          render={<Button variant="outline" size="sm" />}
        >
          <HugeiconsIcon icon={Add01Icon} className="size-4 mr-2" />
          Add Filter
        </PopoverTrigger>
        <PopoverContent className="w-56 p-0" align="start">
          {/* Property list view */}
          <div className="p-2">
            <Input
              placeholder="Search properties..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="h-8"
            />
          </div>
          <div className="border-t" />

          <div className="max-h-72 overflow-y-auto p-1">
            {filteredOptions.length === 0 ? (
              <div className="p-4 text-sm text-muted-foreground text-center">
                No properties found
              </div>
            ) : (
              filteredOptions.map((config) => (
                <PropertyMenuItem
                  key={config.propertyKey}
                  config={config}
                  isSelected={selectedProperty?.propertyKey === config.propertyKey}
                  submenuOpen={submenuOpen && selectedProperty?.propertyKey === config.propertyKey}
                  onSubmenuOpenChange={handleSubmenuOpenChange}
                  onClick={() => handlePropertyClick(config)}
                  onFilterSubmit={handleFilterSubmit}
                />
              ))
            )}
          </div>
        </PopoverContent>
      </Popover>

      {/* Text filter modal */}
      {selectedTextProperty && (
        <TextFilterModal
          isOpen={textFilterModalOpen}
          onClose={() => {
            setTextFilterModalOpen(false)
            setSelectedTextProperty(null)
          }}
          onSubmit={handleTextFilterSubmit}
          propertyKey={selectedTextProperty.propertyKey}
          propertyLabel={selectedTextProperty.propertyLabel}
          allowedOperators={selectedTextProperty.operators}
        />
      )}
    </>
  )
}

interface PropertyMenuItemProps {
  config: FilterConfig
  isSelected: boolean
  submenuOpen: boolean
  onSubmenuOpenChange: (open: boolean) => void
  onClick: () => void
  onFilterSubmit: (filter: Omit<Filter, 'id'>) => void
}

function PropertyMenuItem({
  config,
  isSelected,
  submenuOpen,
  onSubmenuOpenChange,
  onClick,
  onFilterSubmit,
}: PropertyMenuItemProps) {
  // Text filters don't have a submenu
  if (config.controlType === 'text') {
    return (
      <button
        onClick={onClick}
        className={cn(
          "w-full flex items-center gap-1.5 rounded-md px-2 py-1.5 text-sm text-left",
          "hover:bg-accent hover:text-accent-foreground",
          "focus:bg-accent focus:text-accent-foreground focus:outline-none"
        )}
      >
        {config.propertyLabel}
        <HugeiconsIcon icon={TextIcon} className="ml-auto size-4 text-muted-foreground" />
      </button>
    )
  }

  return (
    <Popover open={submenuOpen && isSelected} onOpenChange={onSubmenuOpenChange}>
      <PopoverTrigger
        onClick={onClick}
        className={cn(
          "w-full flex items-center gap-1.5 rounded-md px-2 py-1.5 text-sm text-left",
          "hover:bg-accent hover:text-accent-foreground",
          "focus:bg-accent focus:text-accent-foreground focus:outline-none",
          isSelected && submenuOpen && "bg-accent text-accent-foreground"
        )}
      >
        {config.propertyLabel}
        <HugeiconsIcon icon={ArrowRight01Icon} className="ml-auto size-4 text-muted-foreground" />
      </PopoverTrigger>
      <PopoverContent
        className="w-auto p-0"
        side="right"
        align="start"
        sideOffset={4}
      >
        {config.controlType === 'multiselect' && (
          <MultiSelectFilter
            propertyKey={config.propertyKey}
            propertyLabel={config.propertyLabel}
            options={config.options || []}
            onSubmit={onFilterSubmit}
            allowedOperators={config.operators}
          />
        )}
        {config.controlType === 'date' && (
          <DateFilter
            propertyKey={config.propertyKey}
            propertyLabel={config.propertyLabel}
            onSubmit={onFilterSubmit}
            dateType={config.dateType}
          />
        )}
        {config.controlType === 'number' && (
          <NumberFilter
            propertyKey={config.propertyKey}
            propertyLabel={config.propertyLabel}
            onSubmit={onFilterSubmit}
            allowedOperators={config.operators}
          />
        )}
        {config.controlType === 'boolean' && (
          <BooleanFilter
            propertyKey={config.propertyKey}
            propertyLabel={config.propertyLabel}
            onSubmit={onFilterSubmit}
          />
        )}
      </PopoverContent>
    </Popover>
  )
}
