"use client"

import * as React from "react"
import { Autocomplete as AutocompletePrimitive } from "@base-ui/react/autocomplete"
import { Cancel01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { cn } from "@/lib/utils"

// Re-export the Root component for full access to props
const Autocomplete = AutocompletePrimitive.Root

function AutocompleteInput({
  className,
  ...props
}: AutocompletePrimitive.Input.Props) {
  return (
    <AutocompletePrimitive.Input
      data-slot="autocomplete-input"
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

function AutocompletePortal({ ...props }: AutocompletePrimitive.Portal.Props) {
  return <AutocompletePrimitive.Portal data-slot="autocomplete-portal" {...props} />
}

function AutocompletePositioner({
  className,
  sideOffset = 4,
  ...props
}: AutocompletePrimitive.Positioner.Props) {
  return (
    <AutocompletePrimitive.Positioner
      data-slot="autocomplete-positioner"
      sideOffset={sideOffset}
      className={cn("isolate z-50 outline-none", className)}
      {...props}
    />
  )
}

function AutocompletePopup({
  className,
  ...props
}: AutocompletePrimitive.Popup.Props) {
  return (
    <AutocompletePrimitive.Popup
      data-slot="autocomplete-popup"
      className={cn(
        "bg-popover text-popover-foreground ring-foreground/10",
        "group/autocomplete-popup origin-[var(--transform-origin)] rounded-lg shadow-md ring-1",
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

function AutocompleteList({
  className,
  ...props
}: AutocompletePrimitive.List.Props) {
  return (
    <AutocompletePrimitive.List
      data-slot="autocomplete-list"
      className={cn(
        "max-h-[inherit] overflow-y-auto overscroll-contain p-1 scroll-py-1",
        className
      )}
      {...props}
    />
  )
}

function AutocompleteItem({
  className,
  children,
  ...props
}: AutocompletePrimitive.Item.Props) {
  return (
    <AutocompletePrimitive.Item
      data-slot="autocomplete-item"
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
    </AutocompletePrimitive.Item>
  )
}

// Note: Autocomplete.Item children serve as the item text
// Autocomplete shares components with Combobox and doesn't have ItemIndicator or ItemText

function AutocompleteGroup({
  className,
  ...props
}: AutocompletePrimitive.Group.Props) {
  return (
    <AutocompletePrimitive.Group
      data-slot="autocomplete-group"
      className={cn("overflow-hidden", className)}
      {...props}
    />
  )
}

function AutocompleteGroupLabel({
  className,
  ...props
}: AutocompletePrimitive.GroupLabel.Props) {
  return (
    <AutocompletePrimitive.GroupLabel
      data-slot="autocomplete-group-label"
      className={cn(
        "text-muted-foreground px-2 py-1.5 text-xs font-medium",
        className
      )}
      {...props}
    />
  )
}

function AutocompleteEmpty({
  className,
  ...props
}: AutocompletePrimitive.Empty.Props) {
  return (
    <AutocompletePrimitive.Empty
      data-slot="autocomplete-empty"
      className={cn(
        "py-4 text-center text-sm text-muted-foreground empty:hidden",
        className
      )}
      {...props}
    />
  )
}

function AutocompleteStatus({
  className,
  ...props
}: AutocompletePrimitive.Status.Props) {
  return (
    <AutocompletePrimitive.Status
      data-slot="autocomplete-status"
      className={cn(
        "flex items-center gap-2 px-2 py-1.5 text-sm text-muted-foreground",
        className
      )}
      {...props}
    />
  )
}

function AutocompleteClearButton({
  className,
  ...props
}: React.ComponentProps<"button">) {
  return (
    <button
      type="button"
      data-slot="autocomplete-clear-button"
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
const useAutocompleteFilter = AutocompletePrimitive.useFilter

// Convenience component that combines common parts
function AutocompleteContent({
  className,
  children,
  sideOffset = 4,
  emptyMessage = "No results found.",
  ...props
}: AutocompletePrimitive.Popup.Props &
  Pick<AutocompletePrimitive.Positioner.Props, "sideOffset"> & {
    emptyMessage?: string
  }) {
  return (
    <AutocompletePortal>
      <AutocompletePositioner sideOffset={sideOffset}>
        <AutocompletePopup className={className} {...props}>
          <AutocompleteEmpty>{emptyMessage}</AutocompleteEmpty>
          <AutocompleteList>{children}</AutocompleteList>
        </AutocompletePopup>
      </AutocompletePositioner>
    </AutocompletePortal>
  )
}

export {
  Autocomplete,
  AutocompleteInput,
  AutocompletePortal,
  AutocompletePositioner,
  AutocompletePopup,
  AutocompleteList,
  AutocompleteItem,
  AutocompleteGroup,
  AutocompleteGroupLabel,
  AutocompleteEmpty,
  AutocompleteStatus,
  AutocompleteClearButton,
  AutocompleteContent,
  useAutocompleteFilter,
}
