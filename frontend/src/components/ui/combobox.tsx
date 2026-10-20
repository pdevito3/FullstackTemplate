import * as React from "react"
import {
  ComboBox as ComboBoxPrimitive,
  ComboBoxStateContext,
  ComboBoxValue as ComboBoxValuePrimitive,
  composeRenderProps,
  Group,
  GroupContext,
  Input as InputPrimitive,
  ListBoxItem as ListBoxItemPrimitive,
  ListBox as ListBoxPrimitive,
  ListBoxSection as ListBoxSectionPrimitive,
  OverlayTriggerStateContext,
  Popover as PopoverPrimitive,
  TagGroup as TagGroupPrimitive,
  TagList as TagListPrimitive,
  Tag as TagPrimitive,
  useSlottedContext,
  type ComboBoxProps,
  type ComboBoxValueProps,
  type GroupProps,
  type InputProps,
  type ListBoxItemProps,
  type ListBoxProps,
  type ListBoxSectionProps,
  type TagListProps,
  type TagProps,
} from "@animate-ui/react/primitives/react-aria"
import { HugeiconsIcon } from "@hugeicons/react"
import { ArrowDown01Icon, Cancel01Icon, Tick02Icon } from "@hugeicons/core-free-icons"
import { cn } from "@/lib/utils"

import { Button } from "@/components/ui/button"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
} from "@/components/ui/input-group"

function Combobox<T extends object, M extends "single" | "multiple" = "single">(
  props: ComboBoxProps<T, M>
) {
  return (
    // The same guard as in Select: in an overlay, the hidden copy that the
    // ComboBox builds its collection from inherits the open state of the
    // overlay, and React Aria throws "Duplicate key" when the overlay closes.
    <OverlayTriggerStateContext.Provider value={null}>
      <ComboBoxPrimitive {...props} />
    </OverlayTriggerStateContext.Provider>
  )
}

function ComboboxValue<T>({ ...props }: ComboBoxValueProps<T>) {
  return <ComboBoxValuePrimitive data-slot="combobox-value" {...props} />
}

function ComboboxClear({
  className,
  onPress,
  ...props
}: React.ComponentProps<typeof InputGroupButton>) {
  const state = React.useContext(ComboBoxStateContext)
  // The button opts out of the ComboBox button slot, so it reads the disabled
  // state from the group context that the ComboBox gives.
  const isDisabled = useSlottedContext(GroupContext)?.isDisabled
  if (state?.inputValue === "") {
    return null
  }

  return (
    <InputGroupButton
      data-slot="combobox-clear"
      variant="ghost"
      size="icon-xs"
      aria-label="Clear"
      className={cn(className)}
      onPress={(e) => {
        state?.setValue(null)
        // setValue(null) does not clear typed text when no item is selected.
        state?.setInputValue("")
        // The button unmounts when the text is empty. Give the focus back to the input.
        e.target.closest("[data-slot=input-group]")?.querySelector("input")?.focus()
        onPress?.(e)
      }}
      slot={null}
      isDisabled={isDisabled}
      {...props}
    >
      <HugeiconsIcon icon={Cancel01Icon} strokeWidth={2} className="pointer-events-none" />
    </InputGroupButton>
  )
}

function ComboboxInput({
  className,
  children,
  showTrigger = true,
  showClear = false,
  ...props
}: Omit<React.ComponentProps<"input">, "disabled"> & {
  showTrigger?: boolean
  showClear?: boolean
}) {
  return (
    <InputGroup className={cn("w-auto", className)}>
      <InputGroupInput {...props} />
      <InputGroupAddon align="inline-end">
        {showTrigger && (
          <InputGroupButton
            size="icon-xs"
            variant="ghost"
            data-slot="combobox-trigger"
            className="group-has-data-[slot=combobox-clear]/input-group:hidden data-pressed:bg-transparent [&_svg:not([class*='size-'])]:size-4"
          >
            <HugeiconsIcon icon={ArrowDown01Icon} strokeWidth={2} className="pointer-events-none size-4 text-muted-foreground" />
          </InputGroupButton>
        )}
        {showClear && <ComboboxClear />}
      </InputGroupAddon>
      {children}
    </InputGroup>
  )
}

function ComboboxContent({
  className,
  placement = "bottom",
  offset = 6,
  crossOffset = 0,
  anchor,
  ...props
}: Omit<
  React.ComponentProps<typeof PopoverPrimitive>,
  "className" | "children"
> & {
  className?: string
  children?: React.ReactNode
  anchor?: React.RefObject<HTMLDivElement | null>
}) {
  return (
    <PopoverPrimitive
      data-slot="combobox-content"
      placement={placement}
      offset={offset}
      crossOffset={crossOffset}
      triggerRef={anchor}
      className={cn("relative isolate z-50 max-h-72 w-(--trigger-width) min-w-36 origin-(--trigger-anchor-point) overflow-hidden rounded-lg bg-popover text-popover-foreground shadow-md ring-1 ring-foreground/10 **:data-[slot$=-item]:data-focused:bg-foreground/10 *:data-[slot=input-group]:m-1 *:data-[slot=input-group]:mb-0 *:data-[slot=input-group]:h-8 *:data-[slot=input-group]:border-input/30 *:data-[slot=input-group]:bg-input/30 *:data-[slot=input-group]:shadow-none", className )}
      {...props}
    />
  )
}

function ComboboxList<T extends object>({
  className,
  ...props
}: ListBoxProps<T>) {
  return (
    <ListBoxPrimitive
      data-slot="combobox-list"
      className={composeRenderProps(className, (className) =>
        cn(
          "group/combobox-content no-scrollbar max-h-[inherit] scroll-py-1 overflow-y-auto overscroll-contain p-1 data-empty:p-0",
          className
        )
      )}
      {...props}
    />
  )
}

/**
 * The filter and the input text after a pick use the `textValue` of an item. A
 * string child gives the text. Other children do not, so the item must set `textValue`.
 */
type ComboboxItemProps<T extends object = object> = Omit<
  ListBoxItemProps<T>,
  "children" | "textValue"
> &
  (
    | { children: string; textValue?: string }
    | {
        children?: Exclude<ListBoxItemProps<T>["children"], string>
        textValue: string
      }
  )

function ComboboxItem<T extends object>({
  className,
  children,
  textValue,
  ...props
}: ComboboxItemProps<T>) {
  return (
    <ListBoxItemPrimitive
      data-slot="combobox-item"
      textValue={textValue ?? (typeof children === "string" ? children : undefined)}
      className={composeRenderProps(className, (className) =>
        cn(
          "relative flex w-full cursor-default items-center gap-2 rounded-md py-1 pr-8 pl-1.5 text-sm outline-hidden select-none data-focused:bg-accent data-focused:text-accent-foreground not-data-[variant=destructive]:data-focused:**:text-accent-foreground data-disabled:pointer-events-none data-disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
          className
        )
      )}
      {...props}
    >
      {composeRenderProps(children, (children, { isSelected }) => (
        <>
          {children}
          <span className="pointer-events-none absolute right-2 flex size-4 items-center justify-center">
            {isSelected ? (
              <HugeiconsIcon icon={Tick02Icon} strokeWidth={2} className="pointer-events-none" />
            ) : null}
          </span>
        </>
      ))}
    </ListBoxItemPrimitive>
  )
}

function ComboboxGroup<T extends object>({
  className,
  ...props
}: ListBoxSectionProps<T>) {
  return (
    <ListBoxSectionPrimitive
      data-slot="combobox-group"
      className={cn(className)}
      {...props}
    />
  )
}

function ComboboxEmpty({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="combobox-empty"
      className={cn(
        "hidden w-full justify-center py-2 text-center text-sm text-muted-foreground group-data-empty/combobox-content:flex",
        className
      )}
      {...props}
    />
  )
}

function ComboboxChips({ children, className, ...props }: GroupProps) {
  return (
    <Group
      data-slot="combobox-chips"
      className={composeRenderProps(className, (className) =>
        cn(
          "flex min-h-8 flex-wrap items-center gap-1 rounded-lg border border-input bg-transparent bg-clip-padding px-2.5 py-1 text-sm transition-colors focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50 has-aria-invalid:border-destructive has-aria-invalid:ring-3 has-aria-invalid:ring-destructive/20 has-data-[slot=combobox-chip]:px-1 dark:bg-input/30 dark:has-aria-invalid:border-destructive/50 dark:has-aria-invalid:ring-destructive/40",
          className
        )
      )}
      {...props}
    >
      {children}
    </Group>
  )
}

function ComboboxChipList<T extends object>({
  className,
  "aria-label": ariaLabel = "Selected items",
  ...props
}: Omit<TagListProps<T>, "className" | "items"> & {
  className?: string
  "aria-label"?: string
}) {
  return (
    <ComboBoxValuePrimitive<T> className="contents">
      {({ selectedItems, state }) => (
        <TagGroupPrimitive
          data-slot="combobox-chip-list"
          aria-label={ariaLabel}
          className={cn("contents", className)}
          onRemove={(keys) => {
            if (Array.isArray(state.value)) {
              state.setValue(state.value.filter((k) => !keys.has(k)))
            }
          }}
        >
          <TagListPrimitive
            className="contents"
            items={selectedItems.filter((item) => item != null)}
            {...props}
          />
        </TagGroupPrimitive>
      )}
    </ComboBoxValuePrimitive>
  )
}

function ComboboxChip({
  className,
  children,
  showRemove = true,
  ...props
}: Omit<TagProps, "children"> & {
  showRemove?: boolean
  children?: React.ReactNode
}) {
  return (
    <TagPrimitive
      data-slot="combobox-chip"
      className={composeRenderProps(className, (className) =>
        cn(
          "flex h-[calc(--spacing(5.25))] w-fit items-center justify-center gap-1 rounded-sm bg-muted px-1.5 text-xs font-medium whitespace-nowrap text-foreground has-disabled:pointer-events-none has-disabled:cursor-not-allowed has-disabled:opacity-50 has-data-[slot=combobox-chip-remove]:pr-0",
          className
        )
      )}
      {...props}
    >
      {children}
      {showRemove && (
        <Button
          slot="remove"
          variant="ghost"
          size="icon-xs"
          className="-ml-1 opacity-50 hover:opacity-100"
          data-slot="combobox-chip-remove"
        >
          <HugeiconsIcon icon={Cancel01Icon} strokeWidth={2} className="pointer-events-none" />
        </Button>
      )}
    </TagPrimitive>
  )
}

function ComboboxChipsInput({ className, onKeyDown, ...props }: InputProps) {
  const state = React.useContext(ComboBoxStateContext)
  return (
    <InputPrimitive
      data-slot="combobox-chip-input"
      className={composeRenderProps(className, (className) =>
        cn("min-w-16 flex-1 outline-none", className)
      )}
      onKeyDown={(e) => {
        if (
          e.key === "Backspace" &&
          e.currentTarget.value === "" &&
          Array.isArray(state?.value) &&
          state.value.length > 0
        ) {
          e.preventDefault()
          state.setValue(state.value.slice(0, -1))
        }
        onKeyDown?.(e)
      }}
      {...props}
    />
  )
}

export {
  Combobox,
  ComboboxInput,
  ComboboxContent,
  ComboboxList,
  ComboboxItem,
  ComboboxGroup,
  ComboboxEmpty,
  ComboboxChips,
  ComboboxChip,
  ComboboxChipList,
  ComboboxChipsInput,
  ComboboxValue,
}
