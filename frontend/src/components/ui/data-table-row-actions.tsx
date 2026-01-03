import type { Row } from "@tanstack/react-table"
import { MoreHorizontal } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

interface DataTableRowActionsProps<TData> {
  row: Row<TData>
  onEdit?: (row: TData) => void
  onDelete?: (row: TData) => void
  onView?: (row: TData) => void
  children?: React.ReactNode
}

function DataTableRowActions<TData>({
  row,
  onEdit,
  onDelete,
  onView,
  children,
}: DataTableRowActionsProps<TData>) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            className="flex h-8 w-8 p-0 data-[state=open]:bg-muted"
          />
        }
      >
        <MoreHorizontal className="h-4 w-4" />
        <span className="sr-only">Open menu</span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-[160px]">
        {onView && (
          <DropdownMenuItem onClick={() => onView(row.original)}>
            View
          </DropdownMenuItem>
        )}
        {onEdit && (
          <DropdownMenuItem onClick={() => onEdit(row.original)}>
            Edit
          </DropdownMenuItem>
        )}
        {children}
        {onDelete && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={() => onDelete(row.original)}
              className="text-destructive focus:text-destructive"
            >
              Delete
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

export { DataTableRowActions }
