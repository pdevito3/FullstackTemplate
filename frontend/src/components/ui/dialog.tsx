"use client"

import * as React from "react"
import {
  Dialog as DialogPrimitive,
  DialogTrigger as DialogTriggerPrimitive,
  Heading,
  ModalOverlay as ModalOverlayPrimitive,
  Modal as ModalPrimitive,
  type DialogTriggerProps as DialogTriggerPrimitiveProps,
  type ModalOverlayProps as ModalOverlayPrimitiveProps,
} from "@animate-ui/react/primitives/react-aria"

import { HugeiconsIcon } from "@hugeicons/react"
import { Cancel01Icon } from "@hugeicons/core-free-icons"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import {
  DialogDescriptionContext,
  useDialogDescriptionId,
} from "@/components/ui/dialog-description"

function DialogTrigger({ ...props }: DialogTriggerPrimitiveProps) {
  return <DialogTriggerPrimitive {...props} />
}

function DialogClose({
  className,
  variant = "outline",
  size = "default",
  ...props
}: React.ComponentProps<typeof Button>) {
  return (
    <Button
      slot="close"
      data-slot="dialog-close"
      variant={variant}
      size={size}
      className={cn(className)}
      {...props}
    />
  )
}

function DialogOverlay({
  className,
  children,
  ...props
}: Omit<ModalOverlayPrimitiveProps, "className" | "children"> & {
  className?: string
  children: React.ReactNode
}) {
  return (
    <ModalOverlayPrimitive
      data-slot="dialog-overlay"
      className={cn(
        "fixed inset-0 isolate z-50 bg-black/10 supports-backdrop-filter:backdrop-blur-xs",
        className
      )}
      {...props}
    >
      {children}
    </ModalOverlayPrimitive>
  )
}

function Dialog({
  className,
  children,
  showCloseButton = true,
  isDismissable = true,
  ...props
}: Omit<ModalOverlayPrimitiveProps, "className" | "children"> &
  Pick<React.ComponentProps<typeof ModalPrimitive>, "isDismissable"> & {
    className?: string
    children: React.ReactNode
    showCloseButton?: boolean
  }) {
  const [descriptionId, setDescriptionId] = React.useState<string>()
  return (
    <DialogOverlay isDismissable={isDismissable} {...props}>
      <ModalPrimitive
        data-slot="dialog-content"
        className={cn(
          "fixed top-1/2 left-1/2 z-50 grid w-full max-w-[calc(100%-2rem)] -translate-x-1/2 -translate-y-1/2 gap-4 rounded-xl bg-popover p-4 text-sm text-popover-foreground ring-1 ring-foreground/10 outline-none sm:max-w-sm",
          className
        )}
      >
        <DialogPrimitive
          data-slot="dialog"
          aria-describedby={descriptionId}
          className="[display:inherit] [gap:inherit] outline-none"
        >
          <DialogDescriptionContext.Provider value={setDescriptionId}>
            {children}
          </DialogDescriptionContext.Provider>
          {showCloseButton && (
            <DialogClose
              variant="ghost"
              className="absolute top-2 right-2"
              size="icon-sm"
            >
              <HugeiconsIcon icon={Cancel01Icon} strokeWidth={2} />
              <span className="sr-only">Close</span>
            </DialogClose>
          )}
        </DialogPrimitive>
      </ModalPrimitive>
    </DialogOverlay>
  )
}

function DialogHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-header"
      className={cn("flex flex-col gap-2", className)}
      {...props}
    />
  )
}

function DialogTitle({
  className,
  ...props
}: Omit<React.ComponentProps<typeof Heading>, "slot">) {
  return (
    <Heading
      slot="title"
      data-slot="dialog-title"
      className={cn(
        "text-base leading-none font-medium",
        className
      )}
      {...props}
    />
  )
}

function DialogDescription({
  className,
  id,
  ...props
}: Omit<React.ComponentProps<"div">, "slot">) {
  return (
    <div
      id={useDialogDescriptionId(id)}
      data-slot="dialog-description"
      className={cn(
        "text-sm text-muted-foreground *:[a]:underline *:[a]:underline-offset-3 *:[a]:hover:text-foreground",
        className
      )}
      {...props}
    />
  )
}

export {
  Dialog,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
}
