import { expect, test, vi } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { UserDto } from '@/api/generated/models/types/user-dto'

import { UserActions } from './user-actions'

function aUser(allowedActions: UserDto['allowedActions']): UserDto {
  return {
    id: 'usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f',
    tenantId: 'ten_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f',
    firstName: 'Bob',
    lastName: 'Smith',
    fullName: 'Bob Smith',
    identifier: 'bob',
    email: 'bob@example.com',
    username: 'bob',
    role: 'User',
    permissions: [],
    allowedActions,
    version: 1,
  }
}

const allActions: UserDto['allowedActions'] = ['Update', 'UpdateRole', 'AddPermission', 'RemovePermission', 'Delete']

test('shows a button for each action that the Server allows and that the page can send', async () => {
  await render(<UserActions user={aUser(allActions)} onChangeRole={() => {}} onDelete={() => {}} />)

  const actions = page.getByRole('group', { name: 'Actions for Bob Smith' })
  await expect.element(actions.getByRole('button', { name: 'Change role' })).toBeVisible()
  await expect.element(actions.getByRole('button', { name: 'Delete' })).toBeVisible()
  expect(actions.getByRole('button').elements()).toHaveLength(2)
})

test('shows no actions when the Server allows none that the page can send', async () => {
  await render(<UserActions user={aUser(['Update', 'AddPermission'])} onChangeRole={() => {}} onDelete={() => {}} />)

  expect(page.getByRole('button').elements()).toHaveLength(0)
})

test('Change role asks for the role, offers the current role first, and sends the role that the user picks', async () => {
  const onChangeRole = vi.fn()
  await render(<UserActions user={aUser(['UpdateRole'])} onChangeRole={onChangeRole} onDelete={() => {}} />)

  await page.getByRole('button', { name: 'Change role' }).click()
  const dialog = page.getByRole('dialog', { name: 'Change the role of Bob Smith' })
  await expect.element(dialog.getByRole('button', { name: /^User/ })).toBeVisible()
  await dialog.getByRole('button', { name: /^User/ }).click()
  await page.getByRole('option', { name: 'Admin' }).click()
  await dialog.getByRole('button', { name: 'Change role', exact: true }).click()

  await expect.element(dialog).not.toBeInTheDocument()
  await vi.waitFor(() => expect(onChangeRole).toHaveBeenCalledExactlyOnceWith('Admin'))
})

test('Keep role closes the dialog and sends nothing', async () => {
  const onChangeRole = vi.fn()
  await render(<UserActions user={aUser(['UpdateRole'])} onChangeRole={onChangeRole} onDelete={() => {}} />)

  await page.getByRole('button', { name: 'Change role' }).click()
  await page.getByRole('button', { name: 'Keep role' }).click()

  await expect.element(page.getByRole('dialog')).not.toBeInTheDocument()
  expect(onChangeRole).not.toHaveBeenCalled()
})

test('Change role with the current role closes the dialog and sends nothing', async () => {
  const onChangeRole = vi.fn()
  await render(<UserActions user={aUser(['UpdateRole'])} onChangeRole={onChangeRole} onDelete={() => {}} />)

  await page.getByRole('button', { name: 'Change role' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Change role', exact: true }).click()

  await expect.element(page.getByRole('dialog')).not.toBeInTheDocument()
  expect(onChangeRole).not.toHaveBeenCalled()
})

test('Delete asks before it deletes, and Keep user sends nothing', async () => {
  const onDelete = vi.fn()
  await render(<UserActions user={aUser(['Delete'])} onChangeRole={() => {}} onDelete={onDelete} />)

  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('button', { name: 'Keep user' }).click()

  expect(onDelete).not.toHaveBeenCalled()

  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('dialog', { name: 'Delete Bob Smith?' }).getByRole('button', { name: 'Delete user' }).click()

  expect(onDelete).toHaveBeenCalledOnce()
})

test('while one action runs, its button is pending and the other buttons take no press', async () => {
  await render(
    <UserActions user={aUser(['UpdateRole', 'Delete'])} pendingAction="Delete" onChangeRole={() => {}} onDelete={() => {}} />,
  )

  await expect.element(page.getByRole('button', { name: 'Change role' })).toBeDisabled()
})
