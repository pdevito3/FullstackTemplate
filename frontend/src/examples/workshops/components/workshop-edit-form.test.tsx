import { expect, test, vi } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import { workshopForUpdateDtoSchema } from '@/api/generated/models/zod/workshop-for-update-dto'
import { maximumOf, maxLengthOf, minimumOf } from '@/lib/api-contract'
import { ProblemDetailsError, STALE_VERSION_PROBLEM_TYPE } from '@/lib/problem-details'

import { WorkshopEditForm } from './workshop-edit-form'

const workshop: WorkshopDto = {
  id: 'w_1',
  venueId: 'v_1',
  title: 'Intro to Kubb',
  startsAt: '2026-06-01T15:00:00+00:00',
  venueTimeZone: 'America/Chicago',
  capacity: 20,
  status: 'Draft',
  allowedActions: ['Update', 'Schedule', 'Cancel'],
  version: 3,
}

const stale = new ProblemDetailsError({
  type: STALE_VERSION_PROBLEM_TYPE,
  title: 'The record changed after you loaded it.',
  status: 409,
})

async function renderForm(onSave = vi.fn().mockResolvedValue(undefined), onLoadLatest = vi.fn()) {
  const screen = await render(<WorkshopEditForm workshop={workshop} onSave={onSave} onLoadLatest={onLoadLatest} />)
  return {
    screen,
    onSave,
    onLoadLatest,
    title: screen.getByRole('textbox', { name: 'Title' }),
    capacity: screen.getByRole('spinbutton', { name: 'Capacity' }),
    save: screen.getByRole('button', { name: 'Save' }),
  }
}

test('refuses a title and a capacity over the limits of the contract, and does not save', async () => {
  const titleMax = maxLengthOf(workshopForUpdateDtoSchema.shape.title)
  const capacityMax = maximumOf(workshopForUpdateDtoSchema.shape.capacity)
  const form = await renderForm()

  await userEvent.fill(form.title, 'x'.repeat(titleMax + 1))
  await userEvent.fill(form.capacity, String(capacityMax + 1))
  await userEvent.click(form.save)

  await expect.element(form.screen.getByText(`Use ${titleMax} characters or fewer.`)).toBeVisible()
  await expect.element(form.screen.getByText(`Use ${capacityMax} or fewer.`)).toBeVisible()
  await expect.element(form.screen.getByText(`Up to ${titleMax} characters.`)).toBeVisible()
  expect(form.onSave).not.toHaveBeenCalled()
})

test('refuses a capacity under the minimum of the contract, and its hint gives the range of the Server', async () => {
  const capacityMin = minimumOf(workshopForUpdateDtoSchema.shape.capacity)
  const capacityMax = maximumOf(workshopForUpdateDtoSchema.shape.capacity)
  const form = await renderForm()

  await userEvent.fill(form.capacity, String(capacityMin - 1))
  await userEvent.click(form.save)

  await expect.element(form.screen.getByText(`Use ${capacityMin} or more.`)).toBeVisible()
  await expect.element(form.capacity).toHaveAttribute('aria-invalid', 'true')
  await expect.element(form.capacity).toHaveFocus()
  await expect.element(form.screen.getByText(`A workshop has room for ${capacityMin} to ${capacityMax} people.`)).toBeVisible()
  expect(form.onSave).not.toHaveBeenCalled()
})

test('sends the version that it loaded', async () => {
  const form = await renderForm()

  await userEvent.fill(form.title, 'Kubb in depth')
  await userEvent.click(form.save)

  await expect.poll(() => form.onSave).toHaveBeenLastCalledWith(expect.objectContaining({ title: 'Kubb in depth', version: 3 }))
})

test('after a stale save, loads the latest into the form and saves with its version', async () => {
  const latest = { ...workshop, title: 'Kubb for teams', version: 4 }
  const form = await renderForm(vi.fn().mockRejectedValueOnce(stale).mockResolvedValue(undefined), vi.fn().mockResolvedValue(latest))
  const alert = form.screen.getByRole('alert').filter({ hasText: 'Someone else saved this workshop' })

  await userEvent.fill(form.title, 'Kubb in depth')
  await userEvent.click(form.save)

  await expect.element(alert).toBeVisible()
  await expect.element(form.title).toHaveValue('Kubb in depth')

  await userEvent.click(alert.getByRole('button', { name: 'Load the latest' }))

  await expect.element(form.title).toHaveValue('Kubb for teams')
  await expect.element(alert).not.toBeInTheDocument()

  await userEvent.click(form.save)

  await expect.poll(() => form.onSave).toHaveBeenLastCalledWith(expect.objectContaining({ title: 'Kubb for teams', version: 4 }))
})

test('shows each validation message of the Server on its field', async () => {
  const refused = new ProblemDetailsError({
    type: 'validation',
    title: 'One or more validation errors occurred.',
    status: 422,
    errors: { title: ['A workshop with this title already starts at this time.'], status: ['Only a draft can be edited.'] },
  })
  const form = await renderForm(vi.fn().mockRejectedValue(refused))

  await userEvent.click(form.save)

  await expect.element(form.title).toHaveAttribute('aria-invalid', 'true')
  await expect.element(form.screen.getByText('A workshop with this title already starts at this time.')).toBeVisible()
  await expect.element(form.screen.getByRole('alert').filter({ hasText: 'Only a draft can be edited.' })).toBeVisible()
})

test('puts the focus on the field that the Server refused, so a keyboard user is at the fault', async () => {
  const refused = new ProblemDetailsError({
    type: 'validation',
    title: 'One or more validation errors occurred.',
    status: 422,
    errors: { capacity: ['The venue has room for 15 people.'] },
  })
  const form = await renderForm(vi.fn().mockRejectedValue(refused))

  await userEvent.click(form.save)

  await expect.element(form.screen.getByText('The venue has room for 15 people.')).toBeVisible()
  await expect.element(form.capacity).toHaveFocus()
})

test('puts the focus in the start when the Server refuses the start', async () => {
  const refused = new ProblemDetailsError({
    type: 'validation',
    title: 'One or more validation errors occurred.',
    status: 422,
    errors: { startsAt: ['Choose a start after today.'] },
  })
  const form = await renderForm(vi.fn().mockRejectedValue(refused))

  await userEvent.click(form.save)

  await expect.element(form.screen.getByText('Choose a start after today.')).toBeVisible()
  await expect.element(form.screen.getByRole('group', { name: 'Starts at' }).getByRole('spinbutton').first()).toHaveFocus()
})
