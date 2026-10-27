import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { render } from 'vitest-browser-react'

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

const longName = 'Doctor Alexandra Montgomery-Fitzgerald of the Riverside Family Practice Clinic'

test('a long cell value wraps, so the table fits a 320 px container', async () => {
  const screen = await render(
    <div style={{ width: 320 }}>
      <Table aria-label="Providers">
        <TableHeader>
          <TableHead isRowHeader>Name</TableHead>
          <TableHead>Status</TableHead>
        </TableHeader>
        <TableBody>
          <TableRow id="long">
            <TableCell>{longName}</TableCell>
            <TableCell>Active</TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
  )

  const table = screen.getByRole('grid', { name: 'Providers' }).element()
  expect(table.scrollWidth).toBeLessThanOrEqual(320)
})

interface Vehicle {
  id: string
  name: string
  status: string
}

const vehicles: Vehicle[] = [
  { id: 'v1', name: 'Truck 1', status: 'Active' },
  { id: 'v2', name: 'Truck 2', status: 'In repair' },
]

function Vehicles({ isStacked }: { isStacked: boolean }) {
  return (
    <Table aria-label="Vehicles">
      <TableHeader>
        <TableHead isRowHeader>Name</TableHead>
        {!isStacked && <TableHead>Status</TableHead>}
      </TableHeader>
      <TableBody items={vehicles}>
        {(vehicle) => (
          <TableRow id={vehicle.id}>
            <TableCell>{vehicle.name}</TableCell>
            {!isStacked && <TableCell>{vehicle.status}</TableCell>}
          </TableRow>
        )}
      </TableBody>
    </Table>
  )
}

const errors: unknown[] = []
const handleError = (event: ErrorEvent) => errors.push(event.error ?? event.message)

beforeEach(() => {
  errors.length = 0
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => errors.push(args))
  window.addEventListener('error', handleError)
})

afterEach(() => {
  window.removeEventListener('error', handleError)
  vi.restoreAllMocks()
})

for (const [from, to] of [
  [false, true],
  [true, false],
] as const) {
  test(`a TableBody with items renders its rows again when the row render changes (stacked ${from} to ${to})`, async () => {
    const screen = await render(<Vehicles isStacked={from} />)
    await expect.element(screen.getByRole('rowheader', { name: 'Truck 1' })).toBeVisible()

    await screen.rerender(<Vehicles isStacked={to} />)

    await expect.element(screen.getByRole('rowheader', { name: 'Truck 1' })).toBeVisible()
    const statusCells = screen.getByRole('row', { name: /Truck 1/ }).getByRole('gridcell')
    expect(statusCells.elements()).toHaveLength(to ? 0 : 1)
    expect(errors).toEqual([])
  })
}
