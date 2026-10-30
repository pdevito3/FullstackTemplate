import * as React from "react"
import {
  Heading,
  ModalOverlay as ModalOverlayPrimitive,
  Modal as ModalPrimitive,
  Dialog as SheetPrimitive,
  DialogTrigger as SheetTriggerPrimitive,
  type ModalOverlayProps as ModalOverlayPrimitiveProps,
  type DialogTriggerProps as SheetTriggerPrimitiveProps,
} from "@animate-ui/react/primitives/react-aria"

import { HugeiconsIcon } from "@hugeicons/react"
import { Cancel01Icon } from "@hugeicons/core-free-icons"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import {
  DialogDescriptionContext,
  useDialogDescriptionId,
} from "@/components/ui/dialog-description"

function SheetTrigger({ ...props }: SheetTriggerPrimitiveProps) {
  return <SheetTriggerPrimitive {...props} />
}

function SheetClose({
  className,
  variant = "outline",
  size = "default",
  ...props
}: React.ComponentProps<typeof Button>) {
  return (
    <Button
      slot="close"
      data-slot="sheet-close"
      variant={variant}
      size={size}
      className={cn(className)}
      {...props}
    />
  )
}

function SheetOverlay({
  className,
  children,
  ...props
}: Omit<ModalOverlayPrimitiveProps, "className" | "children"> & {
  className?: string
  children: React.ReactNode
}) {
  return (
    <ModalOverlayPrimitive
      data-slot="sheet-overlay"
      isDismissable
      className={cn(
        "fixed inset-0 z-50 bg-black/10 supports-backdrop-filter:backdrop-blur-xs",
        className
      )}
      {...props}
    >
      {children}
    </ModalOverlayPrimitive>
  )
}

/**
 * The overlay gets the open state and the dismiss props. The content panel
 * gets all other props (data attributes, `dir`, `style`), so a caller such as
 * the mobile sidebar can style and target the panel.
 */
function Sheet({
  className,
  children,
  side = "right",
  showCloseButton = true,
  isOpen,
  defaultOpen,
  onOpenChange,
  isDismissable = true,
  isKeyboardDismissDisabled,
  shouldCloseOnInteractOutside,
  isEntering,
  isExiting,
  UNSTABLE_portalContainer,
  ...props
}: Omit<ModalOverlayPrimitiveProps, "className" | "children"> & {
  className?: string
  children: React.ReactNode
  side?: "top" | "right" | "bottom" | "left"
  showCloseButton?: boolean
}) {
  const [descriptionId, setDescriptionId] = React.useState<string>()
  return (
    <SheetOverlay
      isOpen={isOpen}
      defaultOpen={defaultOpen}
      onOpenChange={onOpenChange}
      isDismissable={isDismissable}
      isKeyboardDismissDisabled={isKeyboardDismissDisabled}
      shouldCloseOnInteractOutside={shouldCloseOnInteractOutside}
      isEntering={isEntering}
      isExiting={isExiting}
      UNSTABLE_portalContainer={UNSTABLE_portalContainer}
    >
      <ModalPrimitive
        data-slot="sheet-content"
        data-side={side}
        className={cn(
          "fixed z-50 flex flex-col gap-4 bg-popover bg-clip-padding text-sm text-popover-foreground shadow-lg data-[side=bottom]:inset-x-0 data-[side=bottom]:bottom-0 data-[side=bottom]:h-auto data-[side=bottom]:border-t data-[side=left]:inset-y-0 data-[side=left]:left-0 data-[side=left]:h-full data-[side=left]:w-3/4 data-[side=left]:border-r data-[side=right]:inset-y-0 data-[side=right]:right-0 data-[side=right]:h-full data-[side=right]:w-3/4 data-[side=right]:border-l data-[side=top]:inset-x-0 data-[side=top]:top-0 data-[side=top]:h-auto data-[side=top]:border-b data-[side=left]:sm:max-w-sm data-[side=right]:sm:max-w-sm",
          className
        )}
        {...props}
      >
        <SheetPrimitive
          data-slot="sheet"
          aria-describedby={descriptionId}
          data-close-button={showCloseButton || undefined}
          className="[display:inherit] h-full max-h-[inherit] [flex-direction:inherit] [gap:inherit] outline-none"
        >
          <DialogDescriptionContext.Provider value={setDescriptionId}>
            {children}
          </DialogDescriptionContext.Provider>
          {showCloseButton && (
            <SheetClose
              variant="ghost"
              className="absolute top-3 right-3"
              size="icon-sm"
            >
              <HugeiconsIcon icon={Cancel01Icon} strokeWidth={2} />
              <span className="sr-only">Close</span>
            </SheetClose>
          )}
        </SheetPrimitive>
      </ModalPrimitive>
    </SheetOverlay>
  )
}

function SheetContent({
  className,
  children,
  side = "right",
  showCloseButton = true,
  ...props
}: React.ComponentProps<typeof Sheet> & {
  side?: "top" | "right" | "bottom" | "left"
  showCloseButton?: boolean
}) {
  return (
    <Sheet
      className={className}
      side={side}
      showCloseButton={showCloseButton}
      {...props}
    >
      {children}
    </Sheet>
  )
}

function SheetHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="sheet-header"
      className={cn("flex flex-col gap-0.5 p-4", className)}
      {...props}
    />
  )
}

function SheetFooter({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="sheet-footer"
      className={cn("mt-auto flex flex-col gap-2 p-4", className)}
      {...props}
    />
  )
}

function SheetTitle({
  className,
  ...props
}: Omit<React.ComponentProps<typeof Heading>, "slot">) {
  return (
    <Heading
      slot="title"
      data-slot="sheet-title"
      className={cn(
        "text-base font-medium text-foreground wrap-anywhere in-data-close-button:pr-8",
        className
      )}
      {...props}
    />
  )
}

function SheetDescription({
  className,
  id,
  ...props
}: Omit<React.ComponentProps<"div">, "slot">) {
  return (
    <div
      id={useDialogDescriptionId(id)}
      data-slot="sheet-description"
      className={cn("text-sm text-muted-foreground wrap-anywhere", className)}
      {...props}
    />
  )
}

export {
  Sheet,
  SheetTrigger,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetFooter,
  SheetTitle,
  SheetDescription,
}
