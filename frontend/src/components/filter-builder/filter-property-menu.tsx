import { useState } from 'react'
import {
  Autocomplete,
  Dialog,
  Menu,
  SearchField,
  useFilter,
  type Key,
} from 'react-aria-components'
import { Add01Icon, ArrowRight01Icon, TextIcon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover } from '@/components/ui/popover'
import { useMediaQuery } from '@/hooks/use-media-query'
import {
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
} from '@/components/ui/dropdown-menu'
import type { ControlType, FilterConfig, Filter } from './types'
import { FilterControl, FilterControlDialog } from './controls/filter-control'
import { FILTER_CONTROLS, type FilterControlSurface } from './controls/filter-controls'

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
  // The date control is 378 px wide and 450 px tall. Beside the sidebar, its
  // submenu needs about 906 px of width and 474 px of height.
  const hasRoomForSubmenus = useMediaQuery('(width >= 64rem) and (height >= 32rem)')

  // The menu closes by itself after an action.
  const handleAction = (key: Key) => {
    const config = options.find((opt) => opt.propertyKey === key)
    if (config && surfaceOf(config.controlType, hasRoomForSubmenus) === 'dialog') {
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
              // handleFilterSubmit and the surface of its first render.
              dependencies={[handleFilterSubmit, hasRoomForSubmenus]}
              onAction={handleAction}
              renderEmptyState={() => (
                <div className="p-4 text-sm text-muted-foreground text-center">
                  No properties found
                </div>
              )}
              className="max-h-72 overflow-y-auto p-1 outline-none"
            >
              {(config) => (
                <PropertyMenuItem
                  config={config}
                  surface={surfaceOf(config.controlType, hasRoomForSubmenus)}
                  onFilterSubmit={handleFilterSubmit}
                />
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

/**
 * A phone or a tablet has no room for a submenu beside the menu. React Aria
 * flips a side popover to the other side, but it never moves the popover along
 * that axis. So without the room, each control opens in the dialog.
 */
function surfaceOf(controlType: ControlType, hasRoomForSubmenus: boolean): FilterControlSurface {
  return hasRoomForSubmenus ? FILTER_CONTROLS[controlType].surface : 'dialog'
}

interface PropertyMenuItemProps {
  config: FilterConfig
  surface: FilterControlSurface
  onFilterSubmit: (filter: Omit<Filter, 'id'>) => void
}

function PropertyMenuItem({ config, surface, onFilterSubmit }: PropertyMenuItemProps) {
  // A dialog control opens after the menu closes, so its item has no submenu.
  if (surface === 'dialog') {
    const isSubmenuOnDesktop = FILTER_CONTROLS[config.controlType].surface === 'submenu'
    return (
      <DropdownMenuItem id={config.propertyKey} textValue={config.propertyLabel}>
        {config.propertyLabel}
        {isSubmenuOnDesktop ? (
          <HugeiconsIcon icon={ArrowRight01Icon} strokeWidth={2} className="ml-auto" />
        ) : (
          <HugeiconsIcon icon={TextIcon} className="ml-auto size-4 text-muted-foreground" />
        )}
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

