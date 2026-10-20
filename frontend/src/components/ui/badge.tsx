import type { VariantProps } from "class-variance-authority"
import { cn } from "@/lib/utils"
import { badgeVariants } from "@/components/ui/badge-variants"

function Badge({
  className,
  variant = "default",
  render,
  ...props
}: React.ComponentProps<"span"> &
  VariantProps<typeof badgeVariants> & {
    render?: (props: React.HTMLAttributes<HTMLElement>) => React.ReactNode
  }) {
  if (render) {
    const renderProps = {
      "data-slot": "badge",
      "data-variant": variant,
      className: cn(badgeVariants({ variant }), className),
      ...props,
    }

    return render(renderProps)
  }

  return (
    <span
      data-slot="badge"
      data-variant={variant}
      className={cn(badgeVariants({ variant }), className)}
      {...props}
    />
  )
}

export { Badge }
