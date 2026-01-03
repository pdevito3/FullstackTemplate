"use client"

import { cva, type VariantProps } from "class-variance-authority"
import {
  DateField as AriaDateField,
  DateInput as AriaDateInput,
  DateSegment as AriaDateSegment,
  TimeField as AriaTimeField,
  composeRenderProps,
  Text,
  type DateFieldProps as AriaDateFieldProps,
  type DateInputProps as AriaDateInputProps,
  type DateSegmentProps as AriaDateSegmentProps,
  type DateValue as AriaDateValue,
  type TimeFieldProps as AriaTimeFieldProps,
  type TimeValue as AriaTimeValue,
  type ValidationResult as AriaValidationResult,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { Label } from "@/components/ui/label"

const DateField = AriaDateField

const TimeField = AriaTimeField

function DateSegment({ className, ...props }: AriaDateSegmentProps) {
  return (
    <AriaDateSegment
      className={composeRenderProps(className, (className) =>
        cn(
          "type-literal:px-0 inline rounded p-0.5 caret-transparent outline outline-0",
          /* Placeholder */
          "data-[placeholder]:text-muted-foreground",
          /* Disabled */
          "data-[disabled]:cursor-not-allowed data-[disabled]:opacity-50",
          /* Focused */
          "data-[focused]:bg-accent data-[focused]:text-accent-foreground",
          /* Invalid */
          "data-[invalid]:data-[focused]:bg-destructive data-[invalid]:data-[focused]:data-[placeholder]:text-destructive-foreground data-[invalid]:data-[focused]:text-destructive-foreground data-[invalid]:data-[placeholder]:text-destructive data-[invalid]:text-destructive",
          className
        )
      )}
      {...props}
    />
  )
}

const dateInputVariants = cva("", {
  variants: {
    variant: {
      default: [
        "relative flex h-10 w-full items-center overflow-hidden rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background",
        /* Focus Within */
        "data-[focus-within]:outline-none data-[focus-within]:ring-2 data-[focus-within]:ring-ring data-[focus-within]:ring-offset-2",
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

interface DateInputProps
  extends AriaDateInputProps,
    VariantProps<typeof dateInputVariants> {}

function DateInput({
  className,
  variant,
  ...props
}: Omit<DateInputProps, "children">) {
  return (
    <AriaDateInput
      className={composeRenderProps(className, (className) =>
        cn(dateInputVariants({ variant }), "text-sm", className)
      )}
      {...props}
    >
      {(segment) => <DateSegment segment={segment} />}
    </AriaDateInput>
  )
}

interface JollyDateFieldProps<T extends AriaDateValue>
  extends AriaDateFieldProps<T> {
  label?: string
  description?: string
  errorMessage?: string | ((validation: AriaValidationResult) => string)
}

function JollyDateField<T extends AriaDateValue>({
  label,
  description,
  className,
  errorMessage,
  ...props
}: JollyDateFieldProps<T>) {
  return (
    <DateField
      className={composeRenderProps(className, (className) =>
        cn("group flex flex-col gap-2", className)
      )}
      {...props}
    >
      {label && <Label>{label}</Label>}
      <DateInput />
      {description && (
        <Text className="text-sm text-muted-foreground" slot="description">
          {description}
        </Text>
      )}
      {errorMessage && (
        <Text className="text-sm text-destructive" slot="errorMessage">
          {typeof errorMessage === "function" ? "" : errorMessage}
        </Text>
      )}
    </DateField>
  )
}

interface JollyTimeFieldProps<T extends AriaTimeValue>
  extends AriaTimeFieldProps<T> {
  label?: string
  description?: string
  errorMessage?: string | ((validation: AriaValidationResult) => string)
}

function JollyTimeField<T extends AriaTimeValue>({
  label,
  description,
  errorMessage,
  className,
  ...props
}: JollyTimeFieldProps<T>) {
  return (
    <TimeField
      className={composeRenderProps(className, (className) =>
        cn("group flex flex-col gap-2", className)
      )}
      {...props}
    >
      {label && <Label>{label}</Label>}
      <DateInput />
      {description && (
        <Text className="text-sm text-muted-foreground" slot="description">
          {description}
        </Text>
      )}
      {errorMessage && (
        <Text className="text-sm text-destructive" slot="errorMessage">
          {typeof errorMessage === "function" ? "" : errorMessage}
        </Text>
      )}
    </TimeField>
  )
}

export {
  DateField,
  DateSegment,
  DateInput,
  TimeField,
  JollyDateField,
  JollyTimeField,
  dateInputVariants,
}
export type { DateInputProps, JollyDateFieldProps, JollyTimeFieldProps }
