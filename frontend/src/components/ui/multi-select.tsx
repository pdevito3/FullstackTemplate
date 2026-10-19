"use client"

import * as React from "react"
import { Combobox as ComboboxPrimitive } from "@base-ui/react/combobox"
import { Cancel01Icon, Tick02Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { cn } from "@/lib/utils"

export interface MultiSelectOption {
  value: string
  label: string
}

interface MultiSelectProps {
  options: MultiSelectOption[]
  value?: MultiSelectOption[]
  defaultValue?: MultiSelectOption[]
  onValueChange?: (value: MultiSelectOption[]) => void
  placeholder?: string
  emptyMessage?: string
  className?: string
  disabled?: boolean
  /** Maximum number of chips to show before collapsing to text */
  maxChips?: number
  /** Custom text when collapsed. Receives count as parameter. Default: "{count} items selected" */
  collapsedText?: (count: number) => string
}

function MultiSelect({
  options,
  value,
  defaultValue,
  onValueChange,
  placeholder = "Select options...",
  emptyMessage = "No options found.",
  className,
  disabled = false,
  maxChips,
  collapsedText = (count) => `${count} items selected`,
}: MultiSelectProps) {
  const containerRef = React.useRef<HTMLDivElement | null>(null)
  const id = React.useId()

  return (
    <ComboboxPrimitive.Root
      items={options}
      multiple
      value={value}
      defaultValue={defaultValue}
      onValueChange={onValueChange}
      itemToStringValue={(item) => item.value}
      disabled={disabled}
    >
      <ComboboxPrimitive.Chips
        ref={containerRef}
        className={cn(
          "dark:bg-input/30 border-input focus-within:border-ring focus-within:ring-ring/50",
          "flex min-h-8 w-full flex-wrap items-center gap-1 rounded-lg border bg-transparent px-2 py-1",
          "transition-colors focus-within:ring-[3px]",
          "disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50",
          className
        )}
      >
        <ComboboxPrimitive.Value>
          {(selected: MultiSelectOption[]) => {
            const isCollapsed = maxChips !== undefined && selected.length > maxChips

            return (
              <React.Fragment>
                {isCollapsed ? (
                  <span className="px-1 text-sm">
                    {collapsedText(selected.length)}
                  </span>
                ) : (
                  selected.map((option) => (
                    <ComboboxPrimitive.Chip
                      key={option.value}
                      className={cn(
                        "bg-secondary text-secondary-foreground",
                        "flex items-center gap-1 rounded-md px-2 py-0.5 text-sm",
                        "cursor-default outline-none",
                        "data-highlighted:bg-primary data-highlighted:text-primary-foreground",
                        "focus-within:bg-primary focus-within:text-primary-foreground"
                      )}
                      aria-label={option.label}
                    >
                      {option.label}
                      <ComboboxPrimitive.ChipRemove
                        className={cn(
                          "rounded-sm p-0.5 text-inherit opacity-70",
                          "hover:opacity-100 focus:opacity-100",
                          "outline-none"
                        )}
                        aria-label="Remove"
                      >
                        <HugeiconsIcon icon={Cancel01Icon} className="size-3" />
                      </ComboboxPrimitive.ChipRemove>
                    </ComboboxPrimitive.Chip>
                  ))
                )}
                <ComboboxPrimitive.Input
                  id={id}
                  placeholder={selected.length > 0 ? "" : placeholder}
                  className={cn(
                    "min-w-12 flex-1 h-6 rounded-md border-0 bg-transparent pl-1 text-sm",
                    "outline-none placeholder:text-muted-foreground"
                  )}
                />
              </React.Fragment>
            )
          }}
        </ComboboxPrimitive.Value>
      </ComboboxPrimitive.Chips>

      <ComboboxPrimitive.Portal>
        <ComboboxPrimitive.Positioner
          className="isolate z-50 outline-none"
          sideOffset={4}
          anchor={containerRef}
        >
          <ComboboxPrimitive.Popup
            className={cn(
              "bg-popover text-popover-foreground ring-foreground/10",
              "group/combobox-popup origin-(--transform-origin) rounded-lg shadow-md ring-1",
              "max-h-[min(var(--available-height),20rem)] w-(--anchor-width) overflow-hidden",
              "data-open:animate-in data-closed:animate-out",
              "data-closed:fade-out-0 data-open:fade-in-0",
              "data-closed:zoom-out-95 data-open:zoom-in-95",
              "data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2",
              "data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
              "duration-100"
            )}
          >
            <ComboboxPrimitive.Empty className="py-4 text-center text-sm text-muted-foreground empty:hidden">
              {emptyMessage}
            </ComboboxPrimitive.Empty>
            <ComboboxPrimitive.List className="max-h-[inherit] overflow-y-auto overscroll-contain p-1 scroll-py-1">
              {(option: MultiSelectOption) => (
                <ComboboxPrimitive.Item
                  key={option.value}
                  value={option}
                  className={cn(
                    "relative flex w-full cursor-default select-none items-center gap-2",
                    "rounded-md py-1.5 pl-2 pr-8 text-sm outline-none",
                    "data-highlighted:bg-accent data-highlighted:text-accent-foreground",
                    "data-disabled:pointer-events-none data-disabled:opacity-50"
                  )}
                >
                  <ComboboxPrimitive.ItemIndicator className="absolute right-2 flex items-center justify-center">
                    <HugeiconsIcon icon={Tick02Icon} className="size-4" />
                  </ComboboxPrimitive.ItemIndicator>
                  <span className="flex-1">{option.label}</span>
                </ComboboxPrimitive.Item>
              )}
            </ComboboxPrimitive.List>
          </ComboboxPrimitive.Popup>
        </ComboboxPrimitive.Positioner>
      </ComboboxPrimitive.Portal>
    </ComboboxPrimitive.Root>
  )
}

export { MultiSelect }
