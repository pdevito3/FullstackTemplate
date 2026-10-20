"use client"

import * as React from "react"
import {
  Autocomplete,
  Collection,
  composeRenderProps,
  Header,
  Input,
  Menu,
  MenuItem,
  MenuSection,
  SearchField,
  Separator,
  useFilter,
  type AutocompleteProps,
  type InputProps,
  type MenuItemProps,
  type MenuProps,
  type MenuSectionProps,
  type SeparatorProps,
} from "@animate-ui/react/primitives/react-aria"
import { HugeiconsIcon } from "@hugeicons/react"
import { SearchIcon, Tick02Icon } from "@hugeicons/core-free-icons"
import { cn } from "@/lib/utils"

import {
  Dialog,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  InputGroup,
  InputGroupAddon,
} from "@/components/ui/input-group"

function Command({
  className,
  dir,
  style,
  ...props
}: Omit<AutocompleteProps, "className" | "style"> & {
  className?: string
  dir?: React.HTMLAttributes<HTMLDivElement>["dir"]
  style?: React.CSSProperties
}) {
  const { contains } = useFilter({ sensitivity: "base" })
  return (
    <div
      data-slot="command"
      dir={dir}
      className={cn(
        "flex size-full flex-col overflow-hidden rounded-xl! bg-popover p-1 text-popover-foreground",
        className
      )}
      style={style}
    >
      <Autocomplete {...props} filter={props.filter || contains}>
        {props.children}
      </Autocomplete>
    </div>
  )
}

function CommandDialog({
  title = "Command Palette",
  description = "Search for a command to run...",
  children,
  isOpen,
  onOpenChange,
  className,
  showCloseButton = false,
  ...props
}: Omit<
  React.ComponentProps<typeof Dialog>,
  "children" | "className" | "isOpen" | "onOpenChange"
> & {
  title?: string
  description?: string
  isOpen?: boolean
  onOpenChange?: (isOpen: boolean) => void
  className?: string
  showCloseButton?: boolean
  children: React.ReactNode
}) {
  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={onOpenChange}
      className={cn(
        "top-1/3 translate-y-0 overflow-hidden rounded-xl! p-0",
        className
      )}
      showCloseButton={showCloseButton}
      isDismissable
      {...props}
    >
      <DialogHeader className="sr-only">
        <DialogTitle>{title}</DialogTitle>
        <DialogDescription>{description}</DialogDescription>
      </DialogHeader>
      {children}
    </Dialog>
  )
}

function CommandInput({ className, autoFocus, ...props }: InputProps) {
  return (
    <SearchField
      autoFocus={autoFocus}
      aria-label={props.placeholder || "Search"}
      data-slot="command-input-wrapper"
      className="p-1 pb-0"
    >
      <InputGroup className="h-8! rounded-lg! border-input/30 bg-input/30 shadow-none! *:data-[slot=input-group-addon]:pl-2!">
        <Input
          {...props}
          data-slot="command-input"
          className={composeRenderProps(className, (className) =>
            cn(
              "w-full text-sm outline-hidden disabled:cursor-not-allowed disabled:opacity-50 [&::-webkit-search-cancel-button]:hidden",
              className
            )
          )}
        />
        <InputGroupAddon>
          <HugeiconsIcon icon={SearchIcon} strokeWidth={2} className="size-4 shrink-0 opacity-50" />
        </InputGroupAddon>
      </InputGroup>
    </SearchField>
  )
}

function CommandList<T extends object>({ className, ...props }: MenuProps<T>) {
  return (
    <Menu
      {...props}
      data-slot="command-list"
      className={composeRenderProps(className, (className) =>
        cn(
          "no-scrollbar max-h-72 scroll-py-1 overflow-x-hidden overflow-y-auto outline-none",
          className
        )
      )}
    />
  )
}

function CommandEmpty({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="command-empty"
      className={cn("py-6 text-center text-sm", className)}
      {...props}
    />
  )
}

function CommandGroup<T extends object>({
  className,
  children,
  items,
  heading,
  ...props
}: MenuSectionProps<T> & { heading?: string }) {
  return (
    <MenuSection
      data-slot="command-group"
      className={cn(
        "overflow-hidden p-1 text-foreground **:data-[slot=command-group-heading]:px-2 **:data-[slot=command-group-heading]:py-1.5 **:data-[slot=command-group-heading]:text-xs **:data-[slot=command-group-heading]:font-medium **:data-[slot=command-group-heading]:text-muted-foreground",
        className
      )}
      {...props}
    >
      {heading && <Header data-slot="command-group-heading">{heading}</Header>}
      <Collection items={items}>{children}</Collection>
    </MenuSection>
  )
}

function CommandSeparator({ className, ...props }: SeparatorProps) {
  return (
    <Separator
      data-slot="command-separator"
      className={cn("-mx-1 h-px bg-border", className)}
      {...props}
    />
  )
}

/**
 * The filter and the typeahead find an item by its `textValue`. A string child
 * gives the text. Other children, for example an icon and a label, do not give
 * the text, so the item must set `textValue`.
 */
type CommandItemProps<T extends object = object> = Omit<
  MenuItemProps<T>,
  "children" | "textValue"
> &
  (
    | { children: string; textValue?: string }
    | {
        children?: Exclude<MenuItemProps<T>["children"], string>
        textValue: string
      }
  )

function CommandItem<T extends object>({
  className,
  children,
  textValue,
  ...props
}: CommandItemProps<T>) {
  return (
    <MenuItem
      {...props}
      data-slot="command-item"
      className={composeRenderProps(className, (className) =>
        cn(
          "group/command-item relative flex cursor-default items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden select-none in-data-[slot=dialog-content]:rounded-lg! data-focused:bg-muted data-focused:text-foreground data-[disabled=true]:pointer-events-none data-[disabled=true]:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4 data-focused:*:[svg]:text-foreground",
          className
        )
      )}
      textValue={
        textValue || (typeof children === "string" ? children : undefined)
      }
    >
      {composeRenderProps(children, (children) => (
        <>
          {children}
          <HugeiconsIcon icon={Tick02Icon} strokeWidth={2} className="ml-auto opacity-0 group-has-data-[slot=command-shortcut]/command-item:hidden group-data-selected/command-item:opacity-100" />
        </>
      ))}
    </MenuItem>
  )
}

function CommandShortcut({
  className,
  ...props
}: React.ComponentProps<"span">) {
  return (
    <span
      data-slot="command-shortcut"
      className={cn(
        "ml-auto text-xs tracking-widest text-muted-foreground group-data-focused/command-item:text-foreground group-data-selected/command-item:text-foreground",
        className
      )}
      {...props}
    />
  )
}

export {
  Command,
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandGroup,
  CommandItem,
  CommandShortcut,
  CommandSeparator,
}
export type { CommandItemProps }
