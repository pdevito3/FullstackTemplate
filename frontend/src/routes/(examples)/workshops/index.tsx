import { createFileRoute } from '@tanstack/react-router'
import * as z from 'zod'

import {
  getWorkshopListQueryPageNumberSchema,
  getWorkshopListQuerySearchSchema,
} from '@/api/generated/workshops/zod/get-workshop-list'
import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'
import { Pager } from '@/components/ui/pager'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useWorkshopList } from '@/examples/workshops/apis/get-workshop-list'
import { formatStart } from '@/examples/workshops/components/format-start'
import { QueryError } from '@/examples/components/query-error'
import { WorkshopSearch } from '@/examples/workshops/components/workshop-search'
import { WorkshopStatusBadge } from '@/examples/workshops/components/workshop-status-badge'
import { useIsMobile } from '@/hooks/use-mobile'
import { maxLengthOf } from '@/lib/api-contract'

const listSearchSchema = z.object({
  search: getWorkshopListQuerySearchSchema.catch(undefined),
  page: getWorkshopListQueryPageNumberSchema.catch(undefined),
})

const searchMaxLength = maxLengthOf(getWorkshopListQuerySearchSchema.unwrap())

export const Route = createFileRoute('/(examples)/workshops/')({
  validateSearch: listSearchSchema,
  component: WorkshopsPage,
  staticData: { breadcrumb: 'Workshops' },
})

function WorkshopsPage() {
  const { search = '', page = 1 } = Route.useSearch()
  const navigate = Route.useNavigate()
  const list = useWorkshopList({ search, page })

  const handleSearch = (text: string, base: string) =>
    navigate({
      search: (prev) => ((prev.search ?? '') === base ? { search: text || undefined } : prev),
      replace: true,
    })

  const handlePageChange = (pageNumber: number) =>
    navigate({ search: (prev) => ({ ...prev, page: pageNumber === 1 ? undefined : pageNumber }) })

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-3xl font-bold tracking-tight">Workshops</h1>
        <div className="flex gap-2">
          <Link to="/venues/new" className={buttonVariants({ variant: 'outline' })}>
            New venue
          </Link>
          <Link to="/workshops/new" className={buttonVariants()}>
            New workshop
          </Link>
        </div>
      </div>

      <WorkshopSearch search={search} maxLength={searchMaxLength} onSearch={handleSearch} />

      <WorkshopRows list={list} search={search} page={page} onPageChange={handlePageChange} />
    </div>
  )
}

interface WorkshopRowsProps {
  list: ReturnType<typeof useWorkshopList>
  search: string
  page: number
  onPageChange: (pageNumber: number) => void
}

function WorkshopRows({ list, search, page, onPageChange }: WorkshopRowsProps) {
  const isMobile = useIsMobile()

  if (list.isPending) {
    return <p role="status" aria-label="Loading workshops" className="text-muted-foreground">Loading workshops</p>
  }

  if (list.isError) {
    return <QueryError title="The workshops did not load." error={list.error} onRetry={() => list.refetch()} />
  }

  const { workshops, pagination } = list.data

  if (workshops.length === 0 && page > 1) {
    return (
      <p className="text-muted-foreground">
        There are no workshops on page {page}.{' '}
        <Link to="/workshops" search={{ search: search || undefined }} className="underline underline-offset-4 hover:text-primary">
          Go to page 1
        </Link>
      </p>
    )
  }

  if (workshops.length === 0) {
    return (
      <p className="text-muted-foreground">
        {search ? `No workshop has "${search.trim()}" in its title.` : 'No workshops yet. Add one with New workshop.'}
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="rounded-md border">
        <Table aria-label="Workshops">
          <TableHeader>
            <TableHead isRowHeader>Title</TableHead>
            {!isMobile && <TableHead>Starts</TableHead>}
            {!isMobile && <TableHead>Capacity</TableHead>}
            <TableHead>Status</TableHead>
          </TableHeader>
          <TableBody>
            {workshops.map((workshop) => (
              <TableRow key={workshop.id} id={workshop.id}>
                <TableCell className="wrap-anywhere">
                  <Link to="/workshops/$id" params={{ id: workshop.id }} className="font-medium underline-offset-4 hover:underline">
                    {workshop.title}
                  </Link>
                  {isMobile && (
                    <p className="text-sm text-muted-foreground">
                      {formatStart(workshop)}, capacity {workshop.capacity}
                    </p>
                  )}
                </TableCell>
                {!isMobile && <TableCell className="whitespace-nowrap">{formatStart(workshop)}</TableCell>}
                {!isMobile && <TableCell className="whitespace-nowrap">{workshop.capacity}</TableCell>}
                <TableCell className="whitespace-nowrap">
                  <WorkshopStatusBadge status={workshop.status} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      <Pager label="Pages of workshops" pagination={pagination} onPageChange={onPageChange} />
    </div>
  )
}
