import { clsx, type ClassValue } from "clsx"
import { extendTailwindMerge } from "tailwind-merge"

// The custom utilities of index.css, so that a class of the caller replaces them.
const twMerge = extendTailwindMerge({ extend: { classGroups: { "max-w": [{ "max-w": ["overlay"] }] } } })

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * `cva` joins the base classes and the variant classes without a Tailwind merge. When both set one CSS property
 * (`border-transparent` and `border-border`), the CSS rule order picks the class that applies, not the variant.
 */
export function withMergedClasses<Props>(variants: (props?: Props) => string): (props?: Props) => string {
  return (props) => cn(variants(props))
}
