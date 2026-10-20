import { useState } from 'react'
import {
  Autocomplete,
  Dialog,
  Menu,
  SearchField,
  useFilter,
  type Key,
} from 'react-aria-components'
import { Add01Icon, TextIcon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover } from '@/components/ui/popover'
import {
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
} from '@/components/ui/dropdown-menu'
import type { FilterConfig, Filter } from './types'
import { FilterControl, FilterControlDialog } from './controls/filter-control'
import { FILTER_CONTROLS } from './controls/filter-controls'

interface FilterPropertyMenuProps {
  options: FilterConfig[]
  onAddFilter: (filter: Omit<Filter, 'id'>) => void
}

/**
 * The "Add Filter" menu. The user types to search the properties. A property
 * opens its filter control in a submenu or in a dialog. `FILTER_CONTROLS`
 * gives the surface of each control type.
 */
export function FilterPropertyMenu({ options, onAddFilter }: FilterPropertyMenuProps) {
  const [popoverOpen, setPopoverOpen] = useState(false)
  const [dialogProperty, setDialogProperty] = useState<FilterConfig | null>(null)
  const { contains } = useFilter({ sensitivity: 'base' })

  // The menu closes by itself after an action.
  const handleAction = (key: Key) => {
    const config = options.find((opt) => opt.propertyKey === key)
    if (config && FILTER_CONTROLS[config.controlType].surface === 'dialog') {
      setDialogProperty(config)
    }
  }

  const handleDialogSubmit = (filter: Omit<Filter, 'id'>) => {
    onAddFilter(filter)
    setDialogProperty(null)
  }

  const handleFilterSubmit = (filter: Omit<Filter, 'id'>) => {
    onAddFilter(filter)
    setPopoverOpen(false)
  }

  return (
    <>
      {/* A MenuTrigger root is necessary for the submenus to open. */}
      <DropdownMenuTrigger isOpen={popoverOpen} onOpenChange={setPopoverOpen}>
        <Button variant="outline" size="sm">
          <HugeiconsIcon icon={Add01Icon} className="size-4" />
          Add Filter
        </Button>
        <Popover placement="bottom start" className="w-56 gap-0 p-0">
          <Autocomplete
            filter={(textValue, inputValue, node) =>
              contains(textValue, inputValue) || contains(propertyKeyOf(node), inputValue)
            }
          >
            <SearchField aria-label="Search properties" autoFocus className="p-2">
              <Input placeholder="Search properties..." className="h-8" />
            </SearchField>
            <div className="border-t" />
            <Menu
              items={options}
              // React Aria caches each item per option. Without this, a submenu keeps the
              // handleFilterSubmit of its first render.
              dependencies={[handleFilterSubmit]}
              onAction={handleAction}
              renderEmptyState={() => (
                <div className="p-4 text-sm text-muted-foreground text-center">
                  No properties found
                </div>
              )}
              className="max-h-72 overflow-y-auto p-1 outline-none"
            >
              {(config) => (
                <PropertyMenuItem config={config} onFilterSubmit={handleFilterSubmit} />
              )}
            </Menu>
          </Autocomplete>
        </Popover>
      </DropdownMenuTrigger>

      {dialogProperty && (
        <FilterControlDialog
          config={dialogProperty}
          onSubmit={handleDialogSubmit}
          onCancel={() => setDialogProperty(null)}
        />
      )}
    </>
  )
}

/**
 * The property key of a node of the menu, so that the search also matches the
 * key (`address` finds Author City). The id of each menu item is the key. A
 * submenu item is a `submenutrigger` node, and its first child is that item.
 */
function propertyKeyOf(node: { type: string; key: Key; firstChildKey?: Key | null }): string {
  return String(node.type === 'submenutrigger' ? node.firstChildKey : node.key)
}

interface PropertyMenuItemProps {
  config: FilterConfig
  onFilterSubmit: (filter: Omit<Filter, 'id'>) => void
}

function PropertyMenuItem({ config, onFilterSubmit }: PropertyMenuItemProps) {
  // A dialog control opens after the menu closes, so its item has no submenu.
  if (FILTER_CONTROLS[config.controlType].surface === 'dialog') {
    return (
      <DropdownMenuItem id={config.propertyKey} textValue={config.propertyLabel}>
        {config.propertyLabel}
        <HugeiconsIcon icon={TextIcon} className="ml-auto size-4 text-muted-foreground" />
      </DropdownMenuItem>
    )
  }

  return (
    <DropdownMenuSub>
      <DropdownMenuSubTrigger id={config.propertyKey} textValue={config.propertyLabel}>
        {config.propertyLabel}
      </DropdownMenuSubTrigger>
      <Popover placement="end top" className="w-auto p-0">
        <Dialog aria-label={`${config.propertyLabel} filter`} className="outline-none">
          <FilterControl config={config} onSubmit={onFilterSubmit} />
        </Dialog>
      </Popover>
    </DropdownMenuSub>
  )
}

