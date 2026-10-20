import * as React from "react"
import {
  OverlayArrow,
  Tooltip as TooltipPrimitive,
  TooltipTrigger as TooltipTriggerPrimitive,
} from "@animate-ui/react/primitives/react-aria"
import { cn } from "@/lib/utils"

function TooltipTrigger({
  delay = 0,
  ...props
}: React.ComponentProps<typeof TooltipTriggerPrimitive>) {
  return (
    <TooltipTriggerPrimitive
      delay={delay}
      {...props}
    />
  )
}

function Tooltip({
  className,
  placement = "top",
  offset = 4,
  crossOffset = 0,
  children,
  ...props
}: Omit<
  React.ComponentProps<typeof TooltipPrimitive>,
  "children" | "className"
> & {
  className?: string
  children?: React.ReactNode
}) {
  return (
    <TooltipPrimitive
      data-slot="tooltip-content"
      placement={placement}
      offset={offset}
      crossOffset={crossOffset}
      className={cn(
        "z-50 inline-flex w-fit max-w-xs origin-(--trigger-anchor-point) items-center gap-1.5 rounded-md bg-foreground px-3 py-1.5 text-xs text-background has-data-[slot=kbd]:pr-1.5 **:data-[slot=kbd]:relative **:data-[slot=kbd]:isolate **:data-[slot=kbd]:z-50 **:data-[slot=kbd]:rounded-sm",
        className
      )}
      {...props}
    >
      {children}
      <OverlayArrow
        className="z-50 size-2.5 rounded-[2px] bg-foreground fill-foreground"
        style={({ placement, defaultStyle }) => ({
          ...defaultStyle,
          transform:
            placement === "bottom"
              ? "translate(-50%, calc(50% + 2px)) rotate(45deg)"
              : placement === "top"
                ? "translate(-50%, calc(-50% - 2px)) rotate(45deg)"
                : placement === "left"
                  ? "translate(calc(-50% - 2px), -50%) rotate(45deg)"
                  : "translate(calc(50% + 2px), -50%) rotate(45deg)",
        })}
      />
    </TooltipPrimitive>
  )
}

export { Tooltip, TooltipTrigger }
