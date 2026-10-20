"use client"

import * as React from "react"
import { Heading } from "react-aria-components"
import {
  Sheet as SheetPrimitive,
  SheetBackdrop as SheetBackdropPrimitive,
  SheetContent as SheetContentPrimitive,
  SheetOverlay as SheetOverlayPrimitive,
  SheetTrigger as SheetTriggerPrimitive,
  type SheetOverlayProps as SheetOverlayPrimitiveProps,
  type SheetTriggerProps as SheetTriggerPrimitiveProps,
} from "react-aria-components/Sheet"

import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import {
  DialogDescriptionContext,
  useDialogDescriptionId,
} from "@/components/ui/dialog-description"

// Known fault, accepted for now: the experimental React Aria `Sheet` ignores Escape
// while its enter animation runs, because the focus stays on the trigger until the
// animation ends. vaul (the drawer on main before the React Aria migration) did not
// have this fault. If React Aria does not correct it, go back to vaul for the drawer.

function DrawerTrigger({ ...props }: SheetTriggerPrimitiveProps) {
  return <SheetTriggerPrimitive {...props} />
}

function DrawerClose({
  className,
  variant = "outline",
  size = "default",
  ...props
}: React.ComponentProps<typeof Button>) {
  return (
    <Button
      slot="close"
      data-slot="drawer-close"
      variant={variant}
      size={size}
      className={cn(className)}
      {...props}
    />
  )
}

/**
 * A swipeable panel that slides in from an edge of the viewport. The user can
 * swipe it away. Put it inside a `DrawerTrigger`, or control it with `isOpen`.
 */
function Drawer({
  className,
  children,
  position = "bottom",
  ...props
}: Omit<SheetOverlayPrimitiveProps, "className" | "children" | "position"> & {
  className?: string
  children: React.ReactNode
  position?: "top" | "right" | "bottom" | "left"
}) {
  const [descriptionId, setDescriptionId] = React.useState<string>()
  return (
    <SheetOverlayPrimitive
      data-slot="drawer-overlay"
      position={position}
      className="fixed inset-0 z-50"
      {...props}
    >
      <SheetBackdropPrimitive
        data-slot="drawer-backdrop"
        swipeAnimation="drawer-backdrop-fade"
        className="fixed inset-0 bg-black/10 supports-backdrop-filter:backdrop-blur-xs"
      />
      <SheetPrimitive
        data-slot="drawer-content"
        overscrollPadding
        className={cn(
          // overscrollPadding adds 100vh or 100vw of padding. box-content keeps it out of the size classes.
          "group/drawer-content z-10 box-content flex flex-col bg-background text-sm will-change-transform",
          "data-[position=bottom]:max-h-[80vh] data-[position=bottom]:w-full data-[position=bottom]:rounded-t-xl data-[position=bottom]:border-t",
          "data-[position=top]:max-h-[80vh] data-[position=top]:w-full data-[position=top]:rounded-b-xl data-[position=top]:border-b",
          "data-[position=left]:h-full data-[position=left]:w-3/4 data-[position=left]:rounded-r-xl data-[position=left]:border-r data-[position=left]:sm:max-w-sm",
          "data-[position=right]:h-full data-[position=right]:w-3/4 data-[position=right]:rounded-l-xl data-[position=right]:border-l data-[position=right]:sm:max-w-sm",
          className
        )}
      >
        <div className="mx-auto mt-4 hidden h-1 w-[100px] shrink-0 rounded-full bg-muted group-data-[position=bottom]/drawer-content:block" />
        <SheetContentPrimitive
          data-slot="drawer"
          aria-describedby={descriptionId}
          className="flex min-h-0 flex-1 flex-col overflow-y-auto pb-(--sheet-scroll-padding-y) outline-none"
        >
          <DialogDescriptionContext.Provider value={setDescriptionId}>
            {children}
          </DialogDescriptionContext.Provider>
        </SheetContentPrimitive>
      </SheetPrimitive>
    </SheetOverlayPrimitive>
  )
}

function DrawerHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="drawer-header"
      className={cn(
        "flex flex-col gap-0.5 p-4 group-data-[position=bottom]/drawer-content:text-center group-data-[position=top]/drawer-content:text-center md:gap-0.5 md:text-left",
        className
      )}
      {...props}
    />
  )
}

function DrawerFooter({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="drawer-footer"
      className={cn("mt-auto flex flex-col gap-2 p-4", className)}
      {...props}
    />
  )
}

function DrawerTitle({
  className,
  ...props
}: Omit<React.ComponentProps<typeof Heading>, "slot">) {
  return (
    <Heading
      slot="title"
      data-slot="drawer-title"
      className={cn("text-base font-medium text-foreground", className)}
      {...props}
    />
  )
}

function DrawerDescription({
  className,
  id,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      id={useDialogDescriptionId(id)}
      data-slot="drawer-description"
      className={cn("text-sm text-muted-foreground", className)}
      {...props}
    />
  )
}

export {
  Drawer,
  DrawerClose,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
}
