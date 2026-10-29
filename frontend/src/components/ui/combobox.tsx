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
  useLocale,
  type Key,
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

interface ComboboxSettings {
  ariaLabelOnly?: string
  allowsCustomValue: boolean
  isReadOnly: boolean
}

const ComboboxSettingsContext = React.createContext<ComboboxSettings>({
  allowsCustomValue: false,
  isReadOnly: false,
})

function Combobox<T extends object, M extends "single" | "multiple" = "single">(
  props: ComboBoxProps<T, M>
) {
  const settings: ComboboxSettings = {
    ariaLabelOnly: props["aria-labelledby"] ? undefined : props["aria-label"],
    allowsCustomValue: props.allowsCustomValue ?? false,
    isReadOnly: props.isReadOnly ?? false,
  }
  return (
    // The same guard as in Select: in an overlay, the hidden copy that the
    // ComboBox builds its collection from inherits the open state of the
    // overlay, and React Aria throws "Duplicate key" when the overlay closes.
    <OverlayTriggerStateContext.Provider value={null}>
      <ComboboxSettingsContext.Provider value={settings}>
        <ComboBoxPrimitive {...props} />
      </ComboboxSettingsContext.Provider>
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

function onlyOptionWithText(
  options: NonNullable<React.ContextType<typeof ComboBoxStateContext>>["selectionManager"],
  text: string,
  collator: Intl.Collator
): Key | null {
  const matches = [...options.collection.getKeys()].filter((key) => {
    const node = options.collection.getItem(key)
    return (
      node?.type === "item" &&
      !options.isDisabled(key) &&
      !node.props?.href &&
      !node.props?.onAction &&
      collator.compare(node.textValue, text) === 0
    )
  })
  return matches.length === 1 ? matches[0] : null
}

/**
 * Enter and Tab pick the option that the arrow keys made active. With no
 * active option, React Aria resets a single-select Combobox to its selected
 * option and drops the typed text. Select the one option whose text matches.
 * With no match, Enter keeps the typed text and does not submit the form,
 * because the text is not the value. Tab moves on, and the blur resets the text.
 */
function usePickTypedOption() {
  const state = React.useContext(ComboBoxStateContext)
  const { allowsCustomValue, isReadOnly } = React.useContext(ComboboxSettingsContext)
  const { locale } = useLocale()

  return (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (
      !state ||
      !(e.key === "Tab" || (e.key === "Enter" && !e.shiftKey)) ||
      e.altKey ||
      e.ctrlKey ||
      e.metaKey ||
      e.repeat ||
      e.nativeEvent.isComposing ||
      // Safari ends an IME composition with an Enter that has isComposing false and keyCode 229.
      e.keyCode === 229 ||
      allowsCustomValue ||
      isReadOnly ||
      state.selectionManager.selectionMode !== "single" ||
      state.inputValue === "" ||
      (state.isOpen && state.selectionManager.focusedKey != null)
    ) {
      return
    }
    const collator = new Intl.Collator(locale, { sensitivity: "accent" })
    // The selection manager has all the options. While the list is closed,
    // state.collection is the last list that showed, which can be empty.
    const { collection, firstSelectedKey } = state.selectionManager
    const selectedText = firstSelectedKey != null ? collection.getItem(firstSelectedKey)?.textValue : undefined
    if (selectedText != null && collator.compare(selectedText, state.inputValue) === 0) {
      return
    }
    const key = onlyOptionWithText(state.selectionManager, state.inputValue, collator)
    if (key != null) {
      state.setValue(key)
    }
    // Tab goes on to React Aria, which closes the list, and to a focus scope,
    // which keeps the focus in a modal.
    if (e.key === "Tab") {
      return
    }
    if (key == null || state.isOpen) {
      e.preventDefault()
    }
    e.stopPropagation()
  }
}

function ComboboxInput({
  className,
  children,
  showTrigger = true,
  showClear = false,
  onKeyDownCapture,
  ...props
}: Omit<React.ComponentProps<"input">, "disabled"> & {
  showTrigger?: boolean
  showClear?: boolean
}) {
  const pickTypedOption = usePickTypedOption()
  return (
    <InputGroup className={cn("w-auto", className)}>
      <InputGroupInput
        onKeyDownCapture={(e) => {
          pickTypedOption(e)
          onKeyDownCapture?.(e)
        }}
        {...props}
      />
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
  maxHeight = remToPx(18),
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
  const state = React.useContext(ComboBoxStateContext)
  // React Aria closes the list on a scroll of the page with the close of the
  // ComboBox, which commits the input and clears typed text that is not a pick.
  // While the input has the focus, the list only hides, and the blur commits.
  const listState = state && {
    ...state,
    close: () => (state.isFocused ? state.isOpen && state.toggle() : state.close()),
  }
  return (
    <OverlayTriggerStateContext.Provider value={listState}>
      <PopoverPrimitive
        data-slot="combobox-content"
        placement={placement}
        offset={offset}
        crossOffset={crossOffset}
        // React Aria sets an inline max-height from the space on the page, which
        // overrides a class. It uses the smaller of that space and this value.
        maxHeight={maxHeight}
        triggerRef={anchor}
        className={cn("relative isolate z-50 w-(--trigger-width) max-w-overlay min-w-36 origin-(--trigger-anchor-point) overflow-hidden rounded-lg bg-popover text-popover-foreground shadow-md ring-1 ring-foreground/10 **:data-[slot$=-item]:data-focused:bg-foreground/10 *:data-[slot=input-group]:m-1 *:data-[slot=input-group]:mb-0 *:data-[slot=input-group]:h-8 *:data-[slot=input-group]:border-input/30 *:data-[slot=input-group]:bg-input/30 *:data-[slot=input-group]:shadow-none", className )}
        {...props}
      />
    </OverlayTriggerStateContext.Provider>
  )
}

function remToPx(rem: number) {
  return rem * parseFloat(getComputedStyle(document.documentElement).fontSize)
}

function ComboboxList<T extends object>({
  className,
  ...props
}: ListBoxProps<T>) {
  const { ariaLabelOnly } = React.useContext(ComboboxSettingsContext)
  // React Aria names the list "Suggestions" and the label of the Combobox. With
  // only an aria-label, it takes the trigger button as the label.
  const labelProps = ariaLabelOnly
    ? { "aria-label": `Suggestions ${ariaLabelOnly}`, "aria-labelledby": "" }
    : {}
  return (
    <ListBoxPrimitive
      data-slot="combobox-list"
      {...labelProps}
      // React Aria makes the option under the pointer the active option, and
      // Enter and Tab pick the active option. The user who types does not look
      // at the pointer, so only the arrow keys make an option active.
      shouldFocusOnHover={false}
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
          "relative flex w-full cursor-default items-center gap-2 rounded-md py-1 pr-8 pl-1.5 text-sm outline-hidden select-none group-not-has-data-focused/combobox-content:data-hovered:bg-foreground/5 data-focused:bg-accent data-focused:text-accent-foreground not-data-[variant=destructive]:data-focused:**:text-accent-foreground data-disabled:pointer-events-none data-disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
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
