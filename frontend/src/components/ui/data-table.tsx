import * as React from "react"
import {
  type ColumnDef,
  type ColumnFiltersState,
  type SortingState,
  type VisibilityState,
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
} from "@tanstack/react-table"

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { DataTableViewOptions } from "@/components/ui/data-table-view-options"

interface DataTableProps<TData, TValue> {
  /** The accessible name of the table */
  label: string
  columns: ColumnDef<TData, TValue>[]
  data: TData[]
  searchKey?: string
  searchPlaceholder?: string
}

function DataTable<TData, TValue>({
  label,
  columns,
  data,
  searchKey,
  searchPlaceholder = "Filter...",
}: DataTableProps<TData, TValue>) {
  const [sorting, setSorting] = React.useState<SortingState>([])
  const [columnFilters, setColumnFilters] = React.useState<ColumnFiltersState>([])
  const [columnVisibility, setColumnVisibility] = React.useState<VisibilityState>({})
  const [rowSelection, setRowSelection] = React.useState({})

  const table = useReactTable({
    data,
    columns,
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    onColumnVisibilityChange: setColumnVisibility,
    onRowSelectionChange: setRowSelection,
    state: {
      sorting,
      columnFilters,
      columnVisibility,
      rowSelection,
    },
  })

  // A React Aria table has one header row, so it shows the leaf headers.
  const headers = table.getLeafHeaders().filter((header) => header.column.getIsVisible())
  // The selection column holds only a checkbox, so it cannot name the row.
  const rowHeaderIndex = Math.max(
    headers.findIndex((header) => header.column.id !== "select"),
    0
  )

  return (
    <div className="w-full">
      <div className="flex items-center py-4">
        {searchKey && (
          <Input
            placeholder={searchPlaceholder}
            value={(table.getColumn(searchKey)?.getFilterValue() as string) ?? ""}
            onChange={(event) =>
              table.getColumn(searchKey)?.setFilterValue(event.target.value)
            }
            className="max-w-sm"
          />
        )}
        <DataTableViewOptions table={table} />
      </div>
      <div className="rounded-md border">
        <Table aria-label={label}>
          <TableHeader>
            {headers.map((header, index) => (
              <TableHead key={header.id} id={header.id} isRowHeader={index === rowHeaderIndex}>
                {header.isPlaceholder
                  ? null
                  : flexRender(
                      header.column.columnDef.header,
                      header.getContext()
                    )}
              </TableHead>
            ))}
          </TableHeader>
          <TableBody renderEmptyState={() => "No results."}>
            {table.getRowModel().rows.map((row) => (
              <TableRow
                key={row.id}
                id={row.id}
                data-state={row.getIsSelected() ? "selected" : undefined}
              >
                {row.getVisibleCells().map((cell) => (
                  <TableCell key={cell.id}>
                    {flexRender(
                      cell.column.columnDef.cell,
                      cell.getContext()
                    )}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      <div className="flex items-center justify-end space-x-2 py-4">
        <div className="flex-1 text-sm text-muted-foreground">
          {table.getFilteredSelectedRowModel().rows.length} of{" "}
          {table.getFilteredRowModel().rows.length} row(s) selected.
        </div>
        <div className="space-x-2">
          <PagerButton isAvailable={table.getCanPreviousPage()} onPress={() => table.previousPage()}>
            Previous
          </PagerButton>
          <PagerButton isAvailable={table.getCanNextPage()} onPress={() => table.nextPage()}>
            Next
          </PagerButton>
        </div>
      </div>
    </div>
  )
}

interface PagerButtonProps {
  isAvailable: boolean
  onPress: () => void
  children: React.ReactNode
}

/**
 * A pager button that keeps the focus at the first and the last page. A disabled button loses the focus to the
 * body, so a keyboard user who pressed Next to reach the last page would have to Tab from the start of the page.
 */
function PagerButton({ isAvailable, onPress, children }: PagerButtonProps) {
  return (
    <Button
      variant="outline"
      size="sm"
      aria-disabled={!isAvailable || undefined}
      className="aria-disabled:pointer-events-none aria-disabled:opacity-50 aria-disabled:focus-visible:ring-ring"
      onPress={() => {
        if (isAvailable) onPress()
      }}
    >
      {children}
    </Button>
  )
}

export { DataTable }
