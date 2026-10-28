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

test('shows a button for each action that the Server allows, and none for the others', async () => {
  await render(<UserActions user={aUser(['Update', 'Delete'])} onAction={() => {}} />)

  const actions = page.getByRole('group', { name: 'Actions for Bob Smith' })
  await expect.element(actions.getByRole('button', { name: 'Edit' })).toBeVisible()
  await expect.element(actions.getByRole('button', { name: 'Delete' })).toBeVisible()
  expect(actions.getByRole('button').elements()).toHaveLength(2)
})

test('shows no actions when the Server allows none', async () => {
  await render(<UserActions user={aUser([])} onAction={() => {}} />)

  expect(page.getByRole('button').elements()).toHaveLength(0)
})

test('a press sends the action that the button shows', async () => {
  const onAction = vi.fn()
  await render(<UserActions user={aUser(['UpdateRole', 'AddPermission'])} onAction={onAction} />)

  await page.getByRole('button', { name: 'Grant permission' }).click()

  expect(onAction).toHaveBeenCalledExactlyOnceWith('AddPermission')
})
