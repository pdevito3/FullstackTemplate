"use client"

import { Tick02Icon, Remove01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"
import {
  Checkbox as AriaCheckbox,
  CheckboxGroup as AriaCheckboxGroup,
  composeRenderProps,
  Text,
  type CheckboxGroupProps as AriaCheckboxGroupProps,
  type ValidationResult as AriaValidationResult,
  type CheckboxProps as AriaCheckboxProps,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { Label } from "@/components/ui/label"

const labelVariants =
  "text-sm font-medium leading-none data-[disabled]:cursor-not-allowed data-[disabled]:opacity-70 group-data-[invalid]:text-destructive"

const CheckboxGroup = AriaCheckboxGroup

const Checkbox = ({ className, children, ...props }: AriaCheckboxProps) => (
  <AriaCheckbox
    className={composeRenderProps(className, (className) =>
      cn(
        "group/checkbox flex items-center gap-x-2",
        /* Disabled */
        "data-[disabled]:cursor-not-allowed data-[disabled]:opacity-70",
        labelVariants,
        className
      )
    )}
    {...props}
  >
    {composeRenderProps(children, (children, renderProps) => (
      <>
        <div
          className={cn(
            "flex size-4 shrink-0 items-center justify-center rounded-sm border border-primary text-current ring-offset-background",
            /* Focus Visible */
            "group-data-[focus-visible]/checkbox:outline-none group-data-[focus-visible]/checkbox:ring-2 group-data-[focus-visible]/checkbox:ring-ring group-data-[focus-visible]/checkbox:ring-offset-2",
            /* Selected */
            "group-data-[indeterminate]/checkbox:bg-primary group-data-[selected]/checkbox:bg-primary group-data-[indeterminate]/checkbox:text-primary-foreground group-data-[selected]/checkbox:text-primary-foreground",
            /* Disabled */
            "group-data-[disabled]/checkbox:cursor-not-allowed group-data-[disabled]/checkbox:opacity-50",
            /* Invalid */
            "group-data-[invalid]/checkbox:border-destructive group-data-[invalid]/checkbox:group-data-[selected]/checkbox:bg-destructive group-data-[invalid]/checkbox:group-data-[selected]/checkbox:text-destructive-foreground",
            /* Resets */
            "focus:outline-none focus-visible:outline-none"
          )}
        >
          {renderProps.isIndeterminate ? (
            <HugeiconsIcon icon={Remove01Icon} className="size-3.5" />
          ) : renderProps.isSelected ? (
            <HugeiconsIcon icon={Tick02Icon} className="size-3.5" />
          ) : null}
        </div>
        {children}
      </>
    ))}
  </AriaCheckbox>
)

interface CheckboxGroupWithLabelProps extends AriaCheckboxGroupProps {
  label?: string
  description?: string
  errorMessage?: string | ((validation: AriaValidationResult) => string)
}

function CheckboxGroupWithLabel({
  label,
  description,
  errorMessage,
  className,
  children,
  ...props
}: CheckboxGroupWithLabelProps) {
  return (
    <CheckboxGroup
      className={composeRenderProps(className, (className) =>
        cn("group flex flex-col gap-2", className)
      )}
      {...props}
    >
      {composeRenderProps(children, (children) => (
        <>
          {label && <Label>{label}</Label>}
          {children}
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
        </>
      ))}
    </CheckboxGroup>
  )
}

export { Checkbox, CheckboxGroup, CheckboxGroupWithLabel }
export type { CheckboxGroupWithLabelProps }
