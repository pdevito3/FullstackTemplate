import { AnimatePresence } from "motion/react"
import {
  DisclosurePanel as CollapsibleContentPrimitive,
  Disclosure as CollapsiblePrimitive,
  Button as CollapsibleTriggerPrimitive,
  type ButtonProps,
  type DisclosurePanelProps,
  type DisclosureProps,
} from "@animate-ui/react/primitives/react-aria"

function Collapsible({ ...props }: DisclosureProps) {
  return <CollapsiblePrimitive data-slot="collapsible" {...props} />
}

function CollapsibleTrigger({ ...props }: ButtonProps) {
  return (
    <CollapsibleTriggerPrimitive
      slot="trigger"
      data-slot="collapsible-trigger"
      {...props}
    />
  )
}

function CollapsibleContent({ ...props }: DisclosurePanelProps) {
  // The Animate UI panel always mounts collapsed and then animates to its state.
  // With AnimatePresence initial={false}, a panel that starts expanded shows without that animation.
  return (
    <AnimatePresence initial={false}>
      <CollapsibleContentPrimitive data-slot="collapsible-content" {...props} />
    </AnimatePresence>
  )
}

export { Collapsible, CollapsibleTrigger, CollapsibleContent }
