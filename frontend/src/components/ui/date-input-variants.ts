import { cva } from "class-variance-authority"
import { withMergedClasses } from "@/lib/utils"

const dateInputVariants = withMergedClasses(
  cva("", {
    variants: {
      variant: {
        default: [
          "relative flex h-10 w-full items-center overflow-hidden rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background",
          /* Focus Within */
          "data-[focus-within]:outline-none data-[focus-within]:ring-2 data-[focus-within]:ring-ring data-[focus-within]:ring-offset-2",
          /* Invalid */
          "data-[invalid]:border-destructive data-[invalid]:ring-3 data-[invalid]:ring-destructive/20 dark:data-[invalid]:border-destructive/50 dark:data-[invalid]:ring-destructive/40",
          /* Disabled */
          "data-[disabled]:opacity-50",
        ],
        ghost: "",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  })
)

export { dateInputVariants }
