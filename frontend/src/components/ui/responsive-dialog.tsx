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

type ScrollPadding = { top: number; bottom: number }

const ScrollPaddingContext = React.createContext<React.Dispatch<React.SetStateAction<ScrollPadding>> | null>(null)

// The header and the footer stick over the content that scrolls. The scroll box gets scroll padding of their heights,
// so the browser does not scroll a field that gets focus under them.
function useStickyHeight(side: keyof ScrollPadding) {
  const setScrollPadding = React.useContext(ScrollPaddingContext)
  const ref = React.useRef<HTMLDivElement>(null)

  React.useLayoutEffect(() => {
    const element = ref.current
    if (!element || !setScrollPadding) return
    const setHeight = (height: number) =>
      setScrollPadding((padding) => (padding[side] === height ? padding : { ...padding, [side]: height }))
    const observer = new ResizeObserver(() => setHeight(element.offsetHeight))
    observer.observe(element)
    return () => {
      observer.disconnect()
      setHeight(0)
    }
  }, [setScrollPadding, side])

  return ref
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
  const [scrollPadding, setScrollPadding] = React.useState<ScrollPadding>({ top: 0, bottom: 0 })

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
          // Mobile: bottom sheet. While it is open, index.css shows the toasts at the top and sets --toast-reserve, the
          // room of the toasts, so the sheet stops below them. The fallback is the room of one toast of one line. dvh is
          // the visible height of a phone with its toolbar shown. vh is the height without the toolbar.
          "right-0 bottom-0 left-0 max-h-[85vh] supports-[height:100dvh]:max-h-[min(85vh,calc(100dvh-var(--toast-reserve,4.5rem)))] rounded-t-xl",
          // Desktop: centered. The toasts show at the bottom right, so the dialog keeps their room free below it: it moves
          // up only as far as it must, and it gets shorter only when it does not fit above them. The toaster publishes
          // --toast-region-height, and the 2rem margin covers the bottom-6 of the toasts and a gap of 0.5rem.
          "md:top-0 md:bottom-auto md:left-1/2 md:max-h-[calc(100vh-4rem-var(--toast-region-height,0px))] md:max-w-md md:-translate-x-1/2 md:translate-y-[min(calc(50vh-50%),calc(100vh-2rem-var(--toast-region-height,0px)-100%))] md:rounded-xl",
          // Move with the toast fade, not in one frame.
          "motion-safe:transition-[max-height,translate] motion-safe:duration-200 motion-safe:ease-out",
          className
        )}
      >
        <DialogPrimitive
          data-slot="responsive-dialog"
          aria-describedby={descriptionId}
          data-close-button={showCloseButton || undefined}
          className="relative flex max-h-[inherit] flex-col outline-none"
        >
          {/* Drag handle for mobile */}
          <div className="mx-auto mt-3 h-1 w-12 shrink-0 rounded-full bg-muted md:hidden" />
          {/* The rounded corners clip the sticky header and footer, which cover the full width. */}
          <div
            className="overflow-y-auto p-4 md:rounded-xl"
            style={{ scrollPaddingTop: scrollPadding.top, scrollPaddingBottom: scrollPadding.bottom }}
          >
            <DialogDescriptionContext.Provider value={setDescriptionId}>
              <ScrollPaddingContext.Provider value={setScrollPadding}>
                {children}
              </ScrollPaddingContext.Provider>
            </DialogDescriptionContext.Provider>
          </div>
          {showCloseButton && (
            <ResponsiveDialogClose
              variant="ghost"
              className="absolute top-2 right-2 z-20 max-md:size-8"
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

/**
 * Sticks to the top of the content that scrolls. Put it first in the dialog.
 * Its background covers the first 0.5rem below it. Leave a gap of at least 0.75rem, so that a focus ring there shows.
 */
function ResponsiveDialogHeader({
  className,
  ...props
}: Omit<React.ComponentProps<"div">, "ref">) {
  return (
    <div
      ref={useStickyHeight("top")}
      data-slot="responsive-dialog-header"
      className={cn(
        // The negative margins cover the p-4 of the content that scrolls, so a focus ring in the side gutter does not show
        // next to the header. pb-2 covers the close button and the content just below the title. -mb-2 keeps pb-2 out of
        // the layout, so the dialog is not taller.
        "sticky -top-4 z-10 -mx-4 -mt-4 -mb-2 flex flex-col gap-2 bg-background px-4 pt-4 pb-2 text-center md:text-left",
        className
      )}
      {...props}
    />
  )
}

/** Sticks to the bottom of the content that scrolls. Put it last in the dialog. */
function ResponsiveDialogFooter({
  className,
  ...props
}: Omit<React.ComponentProps<"div">, "ref">) {
  return (
    <div
      ref={useStickyHeight("bottom")}
      data-slot="responsive-dialog-footer"
      className={cn(
        // The negative margins cover the p-4 of the content that scrolls, so a focus ring in the side gutter does not show
        // next to the footer. index.css shows the top border only while content scrolls under the footer.
        "sticky -bottom-4 z-10 -mx-4 -mb-4 flex flex-col-reverse gap-2 border-t bg-background px-4 pt-4 pb-4 sm:flex-row sm:justify-end",
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
        "in-data-close-button:max-md:px-8 in-data-close-button:md:pr-8",
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
