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
    roles: ['User'],
    permissions: [],
    allowedActions,
    version: 1,
  }
}

const allActions: UserDto['allowedActions'] = ['Update', 'UpdateRoles', 'AddPermission', 'RemovePermission', 'Delete']

test('shows a button for each action that the Server allows and that the page can send', async () => {
  await render(<UserActions user={aUser(allActions)} onChangeRoles={() => {}} onDelete={() => {}} />)

  const actions = page.getByRole('group', { name: 'Actions for Bob Smith' })
  await expect.element(actions.getByRole('button', { name: 'Change roles' })).toBeVisible()
  await expect.element(actions.getByRole('button', { name: 'Delete' })).toBeVisible()
  expect(actions.getByRole('button').elements()).toHaveLength(2)
})

test('shows no actions when the Server allows none that the page can send', async () => {
  await render(<UserActions user={aUser(['Update', 'AddPermission'])} onChangeRoles={() => {}} onDelete={() => {}} />)

  expect(page.getByRole('button').elements()).toHaveLength(0)
})

test('Change roles shows the current roles checked, and sends every role that is checked', async () => {
  const onChangeRoles = vi.fn()
  await render(<UserActions user={aUser(['UpdateRoles'])} onChangeRoles={onChangeRoles} onDelete={() => {}} />)

  await page.getByRole('button', { name: 'Change roles' }).click()
  const dialog = page.getByRole('dialog', { name: 'Change the roles of Bob Smith' })
  await expect.element(dialog.getByRole('checkbox', { name: 'User' })).toBeChecked()
  await expect.element(dialog.getByRole('checkbox', { name: 'Admin' })).not.toBeChecked()
  await dialog.getByText('Admin', { exact: true }).click()
  await dialog.getByRole('button', { name: 'Change roles', exact: true }).click()

  await expect.element(dialog).not.toBeInTheDocument()
  await vi.waitFor(() => expect(onChangeRoles).toHaveBeenCalledExactlyOnceWith(['User', 'Admin']))
})

test('Change roles sends no role when the caller clears each role', async () => {
  const onChangeRoles = vi.fn()
  await render(<UserActions user={aUser(['UpdateRoles'])} onChangeRoles={onChangeRoles} onDelete={() => {}} />)

  await page.getByRole('button', { name: 'Change roles' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByText('User', { exact: true }).click()
  await dialog.getByRole('button', { name: 'Change roles', exact: true }).click()

  await vi.waitFor(() => expect(onChangeRoles).toHaveBeenCalledExactlyOnceWith([]))
})

test('Keep roles closes the dialog and sends nothing', async () => {
  const onChangeRoles = vi.fn()
  await render(<UserActions user={aUser(['UpdateRoles'])} onChangeRoles={onChangeRoles} onDelete={() => {}} />)

  await page.getByRole('button', { name: 'Change roles' }).click()
  await page.getByRole('dialog').getByText('Admin', { exact: true }).click()
  await page.getByRole('button', { name: 'Keep roles' }).click()

  await expect.element(page.getByRole('dialog')).not.toBeInTheDocument()
  expect(onChangeRoles).not.toHaveBeenCalled()
})

test('Change roles with the same roles, also after a role goes off and on again, sends nothing', async () => {
  const onChangeRoles = vi.fn()
  await render(<UserActions user={aUser(['UpdateRoles'])} onChangeRoles={onChangeRoles} onDelete={() => {}} />)

  await page.getByRole('button', { name: 'Change roles' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByText('User', { exact: true }).click()
  await dialog.getByText('User', { exact: true }).click()
  await dialog.getByRole('button', { name: 'Change roles', exact: true }).click()

  await expect.element(page.getByRole('dialog')).not.toBeInTheDocument()
  expect(onChangeRoles).not.toHaveBeenCalled()
})

test('Delete asks before it deletes, and Keep user sends nothing', async () => {
  const onDelete = vi.fn()
  await render(<UserActions user={aUser(['Delete'])} onChangeRoles={() => {}} onDelete={onDelete} />)

  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('button', { name: 'Keep user' }).click()

  expect(onDelete).not.toHaveBeenCalled()

  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('dialog', { name: 'Delete Bob Smith?' }).getByRole('button', { name: 'Delete user' }).click()

  expect(onDelete).toHaveBeenCalledOnce()
})

test('while one action runs, its button is pending and the other buttons take no press', async () => {
  await render(
    <UserActions user={aUser(['UpdateRoles', 'Delete'])} pendingAction="Delete" onChangeRoles={() => {}} onDelete={() => {}} />,
  )

  await expect.element(page.getByRole('button', { name: 'Change roles' })).toBeDisabled()
})
