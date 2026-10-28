import { buttonVariants } from "@/components/ui/button-variants"

type CalendarLayout = "wide" | "narrow"

const layoutClassNames = {
  wide: {
    body: "flex",
    quickSelects: "mr-3 border-r pr-3",
    // One width for both preset lists, so that the calendar keeps its width when the mode changes.
    presets: "flex w-22 min-w-max flex-col gap-1",
    preset: buttonVariants({ variant: "ghost", size: "sm", className: "h-7 justify-start text-xs font-normal" }),
    header: "px-1",
    navButton: "size-7",
    headingButton: "",
  },
  narrow: {
    body: "flex flex-col",
    quickSelects: "mb-3 border-b pb-3",
    presets: "grid grid-cols-3 gap-1",
    preset: buttonVariants({
      variant: "outline",
      size: "sm",
      className: "h-auto min-h-8 px-1 text-xs leading-tight font-normal whitespace-normal",
    }),
    header: "",
    navButton: "size-8",
    headingButton: "h-8",
  },
} satisfies Record<CalendarLayout, Record<string, string>>

export { layoutClassNames }
export type { CalendarLayout }
