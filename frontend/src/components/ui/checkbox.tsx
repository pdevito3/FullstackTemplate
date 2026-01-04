"use client"

import { motion } from "motion/react"
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

const tickVariants = {
  checked: {
    pathLength: 1,
    opacity: 1,
    transition: {
      duration: 0.1,
      delay: 0.1,
    },
  },
  unchecked: {
    pathLength: 0,
    opacity: 0,
    transition: {
      duration: 0.1,
    },
  },
}

const indeterminateVariants = {
  checked: {
    scaleX: 1,
    opacity: 1,
    transition: {
      duration: 0.1,
      delay: 0.1,
    },
  },
  unchecked: {
    scaleX: 0,
    opacity: 0,
    transition: {
      duration: 0.1,
    },
  },
}

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
            <motion.svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 16 16"
              className="size-3.5"
              initial="unchecked"
              animate="checked"
            >
              <motion.path
                fill="currentColor"
                d="M3 8a.75.75 0 0 1 .75-.75h8.5a.75.75 0 0 1 0 1.5h-8.5A.75.75 0 0 1 3 8Z"
                variants={indeterminateVariants}
              />
            </motion.svg>
          ) : (
            <motion.svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth="3"
              stroke="currentColor"
              className="size-3.5"
              initial={false}
              animate={renderProps.isSelected ? "checked" : "unchecked"}
            >
              <motion.path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M4.5 12.75l6 6 9-13.5"
                variants={tickVariants}
              />
            </motion.svg>
          )}
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
