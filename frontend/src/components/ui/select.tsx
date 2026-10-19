"use client"

import * as React from "react"
import { Select as SelectPrimitive } from "@base-ui/react/select"
import { Tick02Icon, ArrowUpDownIcon, ArrowUp01Icon, ArrowDown01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { cn } from "@/lib/utils"

// Re-export the Root component for full access to props
const Select = SelectPrimitive.Root

function SelectTrigger({
  className,
  children,
  ...props
}: SelectPrimitive.Trigger.Props) {
  return (
    <SelectPrimitive.Trigger
      data-slot="select-trigger"
      className={cn(
        "dark:bg-input/30 border-input focus-visible:border-ring focus-visible:ring-ring/50 aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive dark:aria-invalid:border-destructive/50 disabled:bg-input/50 dark:disabled:bg-input/80",
        "flex h-8 w-full items-center justify-between gap-2 rounded-lg border bg-transparent px-2.5 py-1 text-sm",
        "transition-colors focus-visible:ring-[3px] aria-invalid:ring-[3px]",
        "cursor-default select-none outline-none",
        "data-[popup-open]:border-ring data-[popup-open]:ring-ring/50 data-[popup-open]:ring-[3px]",
        "disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50",
        className
      )}
      {...props}
    >
      {children}
    </SelectPrimitive.Trigger>
  )
}

function SelectValue({
  className,
  placeholder,
  children,
  ...props
}: Omit<SelectPrimitive.Value.Props, 'children'> & {
  placeholder?: string
  children?: SelectPrimitive.Value.Props['children']
}) {
  return (
    <SelectPrimitive.Value
      data-slot="select-value"
      className={cn(
        "flex-1 truncate text-left",
        className
      )}
      {...props}
    >
      {children ?? ((value: unknown) => {
        if (value === null || value === undefined) {
          return <span className="text-muted-foreground">{placeholder}</span>
        }
        // Handle object values with label property
        if (typeof value === 'object' && value !== null && 'label' in value) {
          return (value as { label: string }).label
        }
        return String(value)
      })}
    </SelectPrimitive.Value>
  )
}

function SelectIcon({
  className,
  ...props
}: React.ComponentProps<"span">) {
  return (
    <span
      data-slot="select-icon"
      className={cn("flex shrink-0 text-muted-foreground", className)}
      {...props}
    >
      <HugeiconsIcon icon={ArrowUpDownIcon} className="size-4" />
    </span>
  )
}

function SelectPortal({ ...props }: SelectPrimitive.Portal.Props) {
  return <SelectPrimitive.Portal data-slot="select-portal" {...props} />
}

function SelectBackdrop({
  className,
  ...props
}: SelectPrimitive.Backdrop.Props) {
  return (
    <SelectPrimitive.Backdrop
      data-slot="select-backdrop"
      className={cn("fixed inset-0 z-40", className)}
      {...props}
    />
  )
}

function SelectPositioner({
  className,
  sideOffset = 4,
  ...props
}: SelectPrimitive.Positioner.Props) {
  return (
    <SelectPrimitive.Positioner
      data-slot="select-positioner"
      sideOffset={sideOffset}
      className={cn("isolate z-50 outline-none", className)}
      {...props}
    />
  )
}

function SelectPopup({
  className,
  ...props
}: SelectPrimitive.Popup.Props) {
  return (
    <SelectPrimitive.Popup
      data-slot="select-popup"
      className={cn(
        "bg-popover text-popover-foreground ring-foreground/10",
        "group/select-popup origin-[var(--transform-origin)] rounded-lg shadow-md ring-1",
        "max-h-[var(--available-height)] overflow-hidden",
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

function SelectScrollUpArrow({
  className,
  ...props
}: SelectPrimitive.ScrollUpArrow.Props) {
  return (
    <SelectPrimitive.ScrollUpArrow
      data-slot="select-scroll-up-arrow"
      className={cn(
        "sticky top-0 z-[1] flex h-6 w-full cursor-default items-center justify-center",
        "rounded-t-lg bg-popover text-muted-foreground",
        className
      )}
      {...props}
    >
      <HugeiconsIcon icon={ArrowUp01Icon} className="size-4" />
    </SelectPrimitive.ScrollUpArrow>
  )
}

function SelectScrollDownArrow({
  className,
  ...props
}: SelectPrimitive.ScrollDownArrow.Props) {
  return (
    <SelectPrimitive.ScrollDownArrow
      data-slot="select-scroll-down-arrow"
      className={cn(
        "sticky bottom-0 z-[1] flex h-6 w-full cursor-default items-center justify-center",
        "rounded-b-lg bg-popover text-muted-foreground",
        className
      )}
      {...props}
    >
      <HugeiconsIcon icon={ArrowDown01Icon} className="size-4" />
    </SelectPrimitive.ScrollDownArrow>
  )
}

function SelectList({
  className,
  ...props
}: SelectPrimitive.List.Props) {
  return (
    <SelectPrimitive.List
      data-slot="select-list"
      className={cn(
        "relative max-h-[var(--available-height)] overflow-y-auto overscroll-contain p-1 scroll-py-1",
        className
      )}
      {...props}
    />
  )
}

function SelectItem({
  className,
  children,
  ...props
}: SelectPrimitive.Item.Props) {
  return (
    <SelectPrimitive.Item
      data-slot="select-item"
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
    </SelectPrimitive.Item>
  )
}

function SelectItemIndicator({
  className,
  ...props
}: SelectPrimitive.ItemIndicator.Props) {
  return (
    <SelectPrimitive.ItemIndicator
      data-slot="select-item-indicator"
      className={cn(
        "absolute right-2 flex items-center justify-center",
        className
      )}
      {...props}
    >
      <HugeiconsIcon icon={Tick02Icon} className="size-4" />
    </SelectPrimitive.ItemIndicator>
  )
}

function SelectItemText({
  className,
  ...props
}: SelectPrimitive.ItemText.Props) {
  return (
    <SelectPrimitive.ItemText
      data-slot="select-item-text"
      className={cn("flex-1", className)}
      {...props}
    />
  )
}

function SelectGroup({
  className,
  ...props
}: SelectPrimitive.Group.Props) {
  return (
    <SelectPrimitive.Group
      data-slot="select-group"
      className={cn("overflow-hidden", className)}
      {...props}
    />
  )
}

function SelectGroupLabel({
  className,
  ...props
}: SelectPrimitive.GroupLabel.Props) {
  return (
    <SelectPrimitive.GroupLabel
      data-slot="select-group-label"
      className={cn(
        "text-muted-foreground px-2 py-1.5 text-xs font-medium",
        className
      )}
      {...props}
    />
  )
}

function SelectSeparator({
  className,
  ...props
}: SelectPrimitive.Separator.Props) {
  return (
    <SelectPrimitive.Separator
      data-slot="select-separator"
      className={cn("bg-border -mx-1 my-1 h-px", className)}
      {...props}
    />
  )
}

function SelectArrow({
  className,
  ...props
}: SelectPrimitive.Arrow.Props) {
  return (
    <SelectPrimitive.Arrow
      data-slot="select-arrow"
      className={cn(
        "fill-popover stroke-foreground/10",
        className
      )}
      {...props}
    />
  )
}

// Convenience component that combines common parts
function SelectContent({
  className,
  children,
  sideOffset = 4,
  position = "popper",
  ...props
}: SelectPrimitive.Popup.Props &
  Pick<SelectPrimitive.Positioner.Props, "sideOffset"> & {
    position?: "popper" | "item-aligned"
  }) {
  return (
    <SelectPortal>
      <SelectPositioner sideOffset={sideOffset} {...(position === "item-aligned" ? { alignItemWithTrigger: true } : {})}>
        <SelectPopup className={className} {...props}>
          <SelectScrollUpArrow />
          <SelectList>{children}</SelectList>
          <SelectScrollDownArrow />
        </SelectPopup>
      </SelectPositioner>
    </SelectPortal>
  )
}

export {
  Select,
  SelectTrigger,
  SelectValue,
  SelectIcon,
  SelectPortal,
  SelectBackdrop,
  SelectPositioner,
  SelectPopup,
  SelectScrollUpArrow,
  SelectScrollDownArrow,
  SelectList,
  SelectItem,
  SelectItemIndicator,
  SelectItemText,
  SelectGroup,
  SelectGroupLabel,
  SelectSeparator,
  SelectArrow,
  SelectContent,
}
