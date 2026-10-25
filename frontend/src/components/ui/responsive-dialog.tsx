"use client"

import * as React from "react"
import {
  Dialog as DialogPrimitive,
  DialogTrigger as DialogTriggerPrimitive,
  Heading,
  Modal as ModalPrimitive,
  ModalOverlay as ModalOverlayPrimitive,
  type DialogTriggerProps as DialogTriggerPrimitiveProps,
  type ModalOverlayProps as ModalOverlayPrimitiveProps,
} from "@animate-ui/react/primitives/react-aria"
import { Cancel01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { cn } from "@/lib/utils"
import { useIsMobile } from "@/hooks/use-mobile"
import { Button } from "@/components/ui/button"
import {
  DialogDescriptionContext,
  useDialogDescriptionId,
} from "@/components/ui/dialog-description"

function ResponsiveDialogTrigger({ ...props }: DialogTriggerPrimitiveProps) {
  return (
    <DialogTriggerPrimitive {...props} />
  )
}

function ResponsiveDialogClose({
  className,
  variant = "outline",
  size = "default",
  ...props
}: React.ComponentProps<typeof Button>) {
  return (
    <Button
      slot="close"
      data-slot="responsive-dialog-close"
      variant={variant}
      size={size}
      className={cn(className)}
      {...props}
    />
  )
}

/**
 * A dialog that is a bottom sheet on mobile and a centered modal on desktop.
 * Put it inside a `ResponsiveDialogTrigger`, or control it with `isOpen`.
 */
function ResponsiveDialog({
  className,
  children,
  showCloseButton = true,
  isDismissable = true,
  ...props
}: Omit<ModalOverlayPrimitiveProps, "className" | "children"> & {
  className?: string
  children: React.ReactNode
  showCloseButton?: boolean
}) {
  const isMobile = useIsMobile()
  const [descriptionId, setDescriptionId] = React.useState<string>()

  return (
    <ModalOverlayPrimitive
      data-slot="responsive-dialog-overlay"
      isDismissable={isDismissable}
      className={cn(
        "fixed inset-0 isolate z-50 bg-black/10 supports-backdrop-filter:backdrop-blur-xs"
      )}
      {...props}
    >
      <ModalPrimitive
        data-slot="responsive-dialog-content"
        // The Animate UI Modal reads data-side: with "bottom" it slides the
        // content up as a sheet, without it it plays the dialog animation.
        data-side={isMobile ? "bottom" : undefined}
        className={cn(
          "fixed z-50 w-full bg-background text-sm ring-1 ring-foreground/10 outline-none",
          // Mobile: bottom sheet
          "right-0 bottom-0 left-0 max-h-[85vh] rounded-t-xl",
          "md:top-1/2 md:bottom-auto md:left-1/2 md:max-h-[calc(100vh-4rem)] md:max-w-md md:-translate-x-1/2 md:-translate-y-1/2 md:rounded-xl",
          className
        )}
      >
        <DialogPrimitive
          data-slot="responsive-dialog"
          aria-describedby={descriptionId}
          className="relative flex max-h-[inherit] flex-col outline-none"
        >
          {/* Drag handle for mobile */}
          <div className="mx-auto mt-3 h-1 w-12 shrink-0 rounded-full bg-muted md:hidden" />
          <div className="overflow-y-auto p-4">
            <DialogDescriptionContext.Provider value={setDescriptionId}>
              {children}
            </DialogDescriptionContext.Provider>
          </div>
          {showCloseButton && (
            <ResponsiveDialogClose
              variant="ghost"
              className="absolute top-2 right-2 max-md:size-8"
              size="icon-sm"
            >
              <HugeiconsIcon icon={Cancel01Icon} />
              <span className="sr-only">Close</span>
            </ResponsiveDialogClose>
          )}
        </DialogPrimitive>
      </ModalPrimitive>
    </ModalOverlayPrimitive>
  )
}

function ResponsiveDialogHeader({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="responsive-dialog-header"
      className={cn("flex flex-col gap-2 text-center md:text-left", className)}
      {...props}
    />
  )
}

function ResponsiveDialogFooter({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="responsive-dialog-footer"
      className={cn(
        "flex flex-col-reverse gap-2 pt-4 sm:flex-row sm:justify-end",
        className
      )}
      {...props}
    />
  )
}

function ResponsiveDialogTitle({
  className,
  ...props
}: Omit<React.ComponentProps<typeof Heading>, "slot">) {
  return (
    <Heading
      slot="title"
      data-slot="responsive-dialog-title"
      className={cn(
        "text-base leading-none font-semibold md:text-sm md:font-medium",
        className
      )}
      {...props}
    />
  )
}

function ResponsiveDialogDescription({
  className,
  id,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      id={useDialogDescriptionId(id)}
      data-slot="responsive-dialog-description"
      className={cn(
        "text-sm text-muted-foreground",
        "*:[a]:underline *:[a]:underline-offset-3 *:[a]:hover:text-foreground",
        className
      )}
      {...props}
    />
  )
}

export {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
}
