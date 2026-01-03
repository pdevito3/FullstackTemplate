"use client"

import * as React from "react"
import { Combobox as ComboboxPrimitive } from "@base-ui/react/combobox"
import { Tick02Icon, Cancel01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { cn } from "@/lib/utils"

// Re-export the Root component for full access to props
const Combobox = ComboboxPrimitive.Root

function ComboboxInput({
  className,
  ...props
}: ComboboxPrimitive.Input.Props) {
  return (
    <ComboboxPrimitive.Input
      data-slot="combobox-input"
      className={cn(
        "dark:bg-input/30 border-input focus-visible:border-ring focus-visible:ring-ring/50 aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive dark:aria-invalid:border-destructive/50 disabled:bg-input/50 dark:disabled:bg-input/80",
        "h-8 w-full rounded-lg border bg-transparent px-2.5 py-1 text-sm",
        "transition-colors focus-visible:ring-[3px] aria-invalid:ring-[3px]",
        "outline-none placeholder:text-muted-foreground",
        "disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50",
        className
      )}
      {...props}
    />
  )
}

function ComboboxPortal({ ...props }: ComboboxPrimitive.Portal.Props) {
  return <ComboboxPrimitive.Portal data-slot="combobox-portal" {...props} />
}

function ComboboxPositioner({
  className,
  sideOffset = 4,
  ...props
}: ComboboxPrimitive.Positioner.Props) {
  return (
    <ComboboxPrimitive.Positioner
      data-slot="combobox-positioner"
      sideOffset={sideOffset}
      className={cn("isolate z-50 outline-none", className)}
      {...props}
    />
  )
}

function ComboboxPopup({
  className,
  ...props
}: ComboboxPrimitive.Popup.Props) {
  return (
    <ComboboxPrimitive.Popup
      data-slot="combobox-popup"
      className={cn(
        "bg-popover text-popover-foreground ring-foreground/10",
        "group/combobox-popup origin-[var(--transform-origin)] rounded-lg shadow-md ring-1",
        "max-h-[min(var(--available-height),20rem)] w-[var(--anchor-width)] overflow-hidden",
        "data-open:animate-in data-closed:animate-out",
        "data-closed:fade-out-0 data-open:fade-in-0",
        "data-closed:zoom-out-95 data-open:zoom-in-95",
        "data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2",
        "data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
        "duration-100",
        className
      )}
      {...props}
    />
  )
}

function ComboboxList({
  className,
  ...props
}: ComboboxPrimitive.List.Props) {
  return (
    <ComboboxPrimitive.List
      data-slot="combobox-list"
      className={cn(
        "max-h-[inherit] overflow-y-auto overscroll-contain p-1 scroll-py-1",
        className
      )}
      {...props}
    />
  )
}

function ComboboxItem({
  className,
  children,
  ...props
}: ComboboxPrimitive.Item.Props) {
  return (
    <ComboboxPrimitive.Item
      data-slot="combobox-item"
      className={cn(
        "relative flex w-full cursor-default select-none items-center gap-2",
        "rounded-md py-1.5 pl-2 pr-8 text-sm outline-none",
        "data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground",
        "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
        className
      )}
      {...props}
    >
      {children}
    </ComboboxPrimitive.Item>
  )
}

function ComboboxItemIndicator({
  className,
  ...props
}: ComboboxPrimitive.ItemIndicator.Props) {
  return (
    <ComboboxPrimitive.ItemIndicator
      data-slot="combobox-item-indicator"
      className={cn(
        "absolute right-2 flex items-center justify-center",
        className
      )}
      {...props}
    >
      <HugeiconsIcon icon={Tick02Icon} className="size-4" />
    </ComboboxPrimitive.ItemIndicator>
  )
}

// Note: Combobox.Item children serve as the item text
// There is no separate ItemText component in Base UI Combobox

function ComboboxGroup({
  className,
  ...props
}: ComboboxPrimitive.Group.Props) {
  return (
    <ComboboxPrimitive.Group
      data-slot="combobox-group"
      className={cn("overflow-hidden", className)}
      {...props}
    />
  )
}

function ComboboxGroupLabel({
  className,
  ...props
}: ComboboxPrimitive.GroupLabel.Props) {
  return (
    <ComboboxPrimitive.GroupLabel
      data-slot="combobox-group-label"
      className={cn(
        "text-muted-foreground px-2 py-1.5 text-xs font-medium",
        className
      )}
      {...props}
    />
  )
}

function ComboboxEmpty({
  className,
  ...props
}: ComboboxPrimitive.Empty.Props) {
  return (
    <ComboboxPrimitive.Empty
      data-slot="combobox-empty"
      className={cn(
        "py-4 text-center text-sm text-muted-foreground empty:hidden",
        className
      )}
      {...props}
    />
  )
}

function ComboboxChips({
  className,
  ...props
}: ComboboxPrimitive.Chips.Props) {
  return (
    <ComboboxPrimitive.Chips
      data-slot="combobox-chips"
      className={cn(
        "dark:bg-input/30 border-input focus-within:border-ring focus-within:ring-ring/50",
        "flex min-h-8 w-full flex-wrap items-center gap-1 rounded-lg border bg-transparent px-2 py-1",
        "transition-colors focus-within:ring-[3px]",
        className
      )}
      {...props}
    />
  )
}

function ComboboxChip({
  className,
  ...props
}: ComboboxPrimitive.Chip.Props) {
  return (
    <ComboboxPrimitive.Chip
      data-slot="combobox-chip"
      className={cn(
        "bg-secondary text-secondary-foreground",
        "flex items-center gap-1 rounded-md px-2 py-0.5 text-sm",
        "cursor-default outline-none",
        "data-[highlighted]:bg-primary data-[highlighted]:text-primary-foreground",
        "focus-within:bg-primary focus-within:text-primary-foreground",
        className
      )}
      {...props}
    />
  )
}

function ComboboxChipRemove({
  className,
  ...props
}: ComboboxPrimitive.ChipRemove.Props) {
  return (
    <ComboboxPrimitive.ChipRemove
      data-slot="combobox-chip-remove"
      className={cn(
        "rounded-sm p-0.5 text-inherit opacity-70",
        "hover:opacity-100 focus:opacity-100",
        "outline-none",
        className
      )}
      {...props}
    >
      <HugeiconsIcon icon={Cancel01Icon} className="size-3" />
    </ComboboxPrimitive.ChipRemove>
  )
}

function ComboboxValue({
  ...props
}: ComboboxPrimitive.Value.Props) {
  return (
    <ComboboxPrimitive.Value
      {...props}
    />
  )
}

function ComboboxClearButton({
  className,
  ...props
}: React.ComponentProps<"button">) {
  return (
    <button
      type="button"
      data-slot="combobox-clear-button"
      className={cn(
        "rounded-sm p-0.5 text-muted-foreground opacity-70",
        "hover:opacity-100 focus:opacity-100",
        "outline-none focus-visible:ring-1 focus-visible:ring-ring",
        className
      )}
      {...props}
    >
      <HugeiconsIcon icon={Cancel01Icon} className="size-4" />
    </button>
  )
}

// Hook for filtering - re-export from Base UI
const useComboboxFilter = ComboboxPrimitive.useFilter

// Convenience component that combines common parts
function ComboboxContent({
  className,
  children,
  sideOffset = 4,
  emptyMessage = "No results found.",
  ...props
}: ComboboxPrimitive.Popup.Props &
  Pick<ComboboxPrimitive.Positioner.Props, "sideOffset"> & {
    emptyMessage?: string
  }) {
  return (
    <ComboboxPortal>
      <ComboboxPositioner sideOffset={sideOffset}>
        <ComboboxPopup className={className} {...props}>
          <ComboboxEmpty>{emptyMessage}</ComboboxEmpty>
          <ComboboxList>{children}</ComboboxList>
        </ComboboxPopup>
      </ComboboxPositioner>
    </ComboboxPortal>
  )
}

export {
  Combobox,
  ComboboxInput,
  ComboboxPortal,
  ComboboxPositioner,
  ComboboxPopup,
  ComboboxList,
  ComboboxItem,
  ComboboxItemIndicator,
  ComboboxGroup,
  ComboboxGroupLabel,
  ComboboxEmpty,
  ComboboxChips,
  ComboboxChip,
  ComboboxChipRemove,
  ComboboxValue,
  ComboboxClearButton,
  ComboboxContent,
  useComboboxFilter,
}
