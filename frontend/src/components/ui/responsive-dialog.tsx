import * as React from "react"
import { useMediaQuery } from "@/hooks/use-media-query"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
  DialogClose,
} from "@/components/ui/dialog"
import {
  Drawer,
  DrawerClose,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from "@/components/ui/drawer"
import { cn } from "@/lib/utils"

interface ResponsiveDialogProps {
  children: React.ReactNode
  open?: boolean
  onOpenChange?: (open: boolean) => void
}

interface ResponsiveDialogContentProps {
  children: React.ReactNode
  className?: string
}

interface ResponsiveDialogHeaderProps {
  children: React.ReactNode
  className?: string
}

interface ResponsiveDialogFooterProps {
  children: React.ReactNode
  className?: string
}

interface ResponsiveDialogTitleProps {
  children: React.ReactNode
  className?: string
}

interface ResponsiveDialogDescriptionProps {
  children: React.ReactNode
  className?: string
}

interface ResponsiveDialogTriggerProps {
  children: React.ReactNode
  render?: React.ReactElement
}

interface ResponsiveDialogCloseProps {
  children: React.ReactNode
  render?: React.ReactElement
}

const ResponsiveDialogContext = React.createContext<{ isDesktop: boolean }>({
  isDesktop: true,
})

function ResponsiveDialog({
  children,
  open,
  onOpenChange,
}: ResponsiveDialogProps) {
  const isDesktop = useMediaQuery("(min-width: 768px)")

  if (isDesktop) {
    return (
      <ResponsiveDialogContext.Provider value={{ isDesktop }}>
        <Dialog open={open} onOpenChange={onOpenChange}>
          {children}
        </Dialog>
      </ResponsiveDialogContext.Provider>
    )
  }

  return (
    <ResponsiveDialogContext.Provider value={{ isDesktop }}>
      <Drawer open={open} onOpenChange={onOpenChange}>
        {children}
      </Drawer>
    </ResponsiveDialogContext.Provider>
  )
}

function ResponsiveDialogTrigger({
  children,
  render,
}: ResponsiveDialogTriggerProps) {
  const { isDesktop } = React.useContext(ResponsiveDialogContext)

  if (isDesktop) {
    return <DialogTrigger render={render}>{children}</DialogTrigger>
  }

  // Drawer uses vaul which uses asChild pattern
  return <DrawerTrigger asChild={!!render}>{render ? React.cloneElement(render, {}, children) : children}</DrawerTrigger>
}

function ResponsiveDialogContent({
  children,
  className,
}: ResponsiveDialogContentProps) {
  const { isDesktop } = React.useContext(ResponsiveDialogContext)

  if (isDesktop) {
    return <DialogContent className={className}>{children}</DialogContent>
  }

  return <DrawerContent className={className}>{children}</DrawerContent>
}

function ResponsiveDialogHeader({
  children,
  className,
}: ResponsiveDialogHeaderProps) {
  const { isDesktop } = React.useContext(ResponsiveDialogContext)

  if (isDesktop) {
    return <DialogHeader className={className}>{children}</DialogHeader>
  }

  return <DrawerHeader className={cn("text-left", className)}>{children}</DrawerHeader>
}

function ResponsiveDialogFooter({
  children,
  className,
}: ResponsiveDialogFooterProps) {
  const { isDesktop } = React.useContext(ResponsiveDialogContext)

  if (isDesktop) {
    return <DialogFooter className={className}>{children}</DialogFooter>
  }

  return <DrawerFooter className={cn("pt-2", className)}>{children}</DrawerFooter>
}

function ResponsiveDialogTitle({
  children,
  className,
}: ResponsiveDialogTitleProps) {
  const { isDesktop } = React.useContext(ResponsiveDialogContext)

  if (isDesktop) {
    return <DialogTitle className={className}>{children}</DialogTitle>
  }

  return <DrawerTitle className={className}>{children}</DrawerTitle>
}

function ResponsiveDialogDescription({
  children,
  className,
}: ResponsiveDialogDescriptionProps) {
  const { isDesktop } = React.useContext(ResponsiveDialogContext)

  if (isDesktop) {
    return <DialogDescription className={className}>{children}</DialogDescription>
  }

  return <DrawerDescription className={className}>{children}</DrawerDescription>
}

function ResponsiveDialogClose({
  children,
  render,
}: ResponsiveDialogCloseProps) {
  const { isDesktop } = React.useContext(ResponsiveDialogContext)

  if (isDesktop) {
    return <DialogClose render={render}>{children}</DialogClose>
  }

  // Drawer uses vaul which uses asChild pattern
  return <DrawerClose asChild={!!render}>{render ? React.cloneElement(render, {}, children) : children}</DrawerClose>
}

export {
  ResponsiveDialog,
  ResponsiveDialogTrigger,
  ResponsiveDialogContent,
  ResponsiveDialogHeader,
  ResponsiveDialogFooter,
  ResponsiveDialogTitle,
  ResponsiveDialogDescription,
  ResponsiveDialogClose,
}
