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

function isBefore(node: Node, other: Node) {
  return (node.compareDocumentPosition(other) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0
}

// While the React Aria `Sheet` slides in, it keeps the focus outside the sheet, but it
// handles Escape only inside the sheet.
function CloseOnEscape({
  overlayRef,
  onClose,
}: {
  overlayRef: React.RefObject<HTMLDivElement | null>
  onClose: () => void
}) {
  React.useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const overlay = overlayRef.current
      if (event.key !== "Escape" || !overlay || !(event.target instanceof Node)) return
      if (!isBefore(event.target, overlay)) return
      event.stopPropagation()
      onClose()
    }
    document.addEventListener("keydown", onKeyDown, true)
    return () => document.removeEventListener("keydown", onKeyDown, true)
  }, [overlayRef, onClose])
  return null
}

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
}: Omit<SheetOverlayPrimitiveProps, "className" | "children" | "position" | "UNSTABLE_portalContainer"> & {
  className?: string
  children: React.ReactNode
  position?: "top" | "right" | "bottom" | "left"
}) {
  const [descriptionId, setDescriptionId] = React.useState<string>()
  const overlayRef = React.useRef<HTMLDivElement>(null)
  return (
    <SheetOverlayPrimitive
      ref={overlayRef}
      data-slot="drawer-overlay"
      position={position}
      className="fixed inset-0 z-50"
      {...props}
    >
      {({ isEntering, isExiting, state }) => (
        <>
          {isEntering && !isExiting && !props.preventDismissal && (
            <CloseOnEscape overlayRef={overlayRef} onClose={state.close} />
          )}
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
              // On a phone, index.css sets --toast-reserve, the room of the toasts at the top. The drawer stops below them.
              // To change the bottom cap, override this class with the same variant prefix.
              "data-[position=bottom]:supports-[height:100dvh]:max-h-[min(80vh,calc(100dvh-var(--toast-reserve,0px)))]",
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
        </>
      )}
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
      className={cn("text-base font-medium text-foreground wrap-anywhere", className)}
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
      className={cn("text-sm text-muted-foreground wrap-anywhere", className)}
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
