"use client"

import { useContext } from "react"
import { ComboBoxStateContext, type ComboBoxProps } from "react-aria-components"

import {
  Combobox,
  ComboboxChip,
  ComboboxChipList,
  ComboboxChips,
  ComboboxChipsInput,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxItem,
  ComboboxList,
  ComboboxValue,
} from "@/components/ui/combobox"

export interface MultiSelectOption {
  value: string
  label: string
}

/**
 * `value`, `defaultValue`, `onChange`, and `isDisabled` follow the React Aria
 * ComboBox in multiple selection mode: the value is the keys of the selected
 * options (`MultiSelectOption.value`).
 */
interface MultiSelectProps
  extends Pick<
    ComboBoxProps<MultiSelectOption, "multiple">,
    "value" | "defaultValue" | "onChange" | "isDisabled"
  > {
  options: MultiSelectOption[]
  placeholder?: string
  emptyMessage?: string
  className?: string
  /** Maximum number of chips to show before collapsing to text */
  maxChips?: number
  /** Custom text when collapsed. Receives count as parameter. Default: "{count} items selected" */
  collapsedText?: (count: number) => string
}

function MultiSelect({
  options,
  value,
  defaultValue,
  onChange,
  placeholder = "Select options...",
  emptyMessage = "No options found.",
  className,
  isDisabled = false,
  maxChips,
  collapsedText = (count) => `${count} items selected`,
}: MultiSelectProps) {
  return (
    <Combobox<MultiSelectOption, "multiple">
      aria-label={placeholder}
      selectionMode="multiple"
      value={value}
      defaultValue={defaultValue}
      onChange={onChange}
      isDisabled={isDisabled}
      allowsEmptyCollection
      menuTrigger="focus"
    >
      <ComboboxChips className={className}>
        <MultiSelectValue
          placeholder={placeholder}
          maxChips={maxChips}
          collapsedText={collapsedText}
        />
      </ComboboxChips>
      <ComboboxContent>
        <ComboboxList
          items={options}
          renderEmptyState={() => <ComboboxEmpty>{emptyMessage}</ComboboxEmpty>}
        >
          {(option) => (
            <ComboboxItem id={option.value} value={option} textValue={option.label}>
              {option.label}
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}

/**
 * The chips and the input. It reads the count from the ComboBox state, because
 * ComboboxChipList or the collapsed text renders the one ComboBoxValue element, which has
 * the id that the input refers to.
 */
function MultiSelectValue({
  placeholder,
  maxChips,
  collapsedText,
}: Pick<MultiSelectProps, "placeholder" | "maxChips"> &
  Required<Pick<MultiSelectProps, "collapsedText">>) {
  const count = useContext(ComboBoxStateContext)?.selectedItems.length ?? 0
  const isCollapsed = maxChips !== undefined && count > maxChips

  return (
    <>
      {isCollapsed ? (
        <ComboboxValue className="px-1 text-sm">{collapsedText(count)}</ComboboxValue>
      ) : (
        <ComboboxChipList<MultiSelectOption>>
          {(option) => (
            <ComboboxChip id={option.value} textValue={option.label}>
              {option.label}
            </ComboboxChip>
          )}
        </ComboboxChipList>
      )}
      <ComboboxChipsInput placeholder={count > 0 ? "" : placeholder} />
    </>
  )
}

export { MultiSelect }
