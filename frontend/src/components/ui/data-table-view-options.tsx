import type { Table } from "@tanstack/react-table"
import { Settings01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

interface DataTableViewOptionsProps<TData> {
  table: Table<TData>
}

function DataTableViewOptions<TData>({
  table,
}: DataTableViewOptionsProps<TData>) {
  const hideableColumns = table
    .getAllColumns()
    .filter(
      (column) =>
        typeof column.accessorFn !== "undefined" && column.getCanHide()
    )

  return (
    <DropdownMenuTrigger>
      <Button variant="outline" size="sm" className="ml-auto hidden h-8 lg:flex">
        <HugeiconsIcon icon={Settings01Icon} className="size-4" />
        View
      </Button>
      <DropdownMenu placement="bottom end" className="w-[150px]">
        <DropdownMenuGroup
          selectionMode="multiple"
          selectedKeys={hideableColumns
            .filter((column) => column.getIsVisible())
            .map((column) => column.id)}
          onSelectionChange={(keys) => {
            for (const column of hideableColumns) {
              column.toggleVisibility(keys === "all" || keys.has(column.id))
            }
          }}
        >
          <DropdownMenuLabel>Toggle columns</DropdownMenuLabel>
          <DropdownMenuSeparator />
          {hideableColumns.map((column) => (
            <DropdownMenuItem key={column.id} id={column.id} className="capitalize">
              {column.id}
            </DropdownMenuItem>
          ))}
        </DropdownMenuGroup>
      </DropdownMenu>
    </DropdownMenuTrigger>
  )
}

export { DataTableViewOptions }
