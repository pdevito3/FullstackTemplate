import { QueryClient } from '@tanstack/react-query'
import { expect, test, vi } from 'vitest'

import { MembershipKeys } from './membership.keys'
import { retryFailedMembership } from './refresh-caller-membership'

const queryClientAfter = async (queryFn: () => Promise<unknown>) => {
  const queryClient = new QueryClient()
  await queryClient.prefetchQuery({ queryKey: MembershipKeys.of('bob'), queryFn, retry: false })
  return queryClient
}

test('asks again after a failed check', async () => {
  const queryFn = vi.fn().mockRejectedValue(new Error('refused'))
  const queryClient = await queryClientAfter(queryFn)

  await retryFailedMembership(queryClient)

  expect(queryFn).toHaveBeenCalledTimes(2)
})

test.each([
  ['a user record', { id: 'usr_1' }],
  ['no user record', null],
])('sends no request after a check that found %s', async (_, answer) => {
  const queryFn = vi.fn().mockResolvedValue(answer)
  const queryClient = await queryClientAfter(queryFn)

  await retryFailedMembership(queryClient)

  expect(queryFn).toHaveBeenCalledTimes(1)
})

test('asks again after a failed check that keeps the last answer', async () => {
  const queryFn = vi.fn().mockResolvedValueOnce({ id: 'usr_1' }).mockRejectedValue(new Error('refused'))
  const queryClient = await queryClientAfter(queryFn)
  await queryClient.refetchQueries({ queryKey: MembershipKeys.of('bob') })

  await retryFailedMembership(queryClient)

  expect(queryFn).toHaveBeenCalledTimes(3)
})

test('does not restart a check that is in progress', async () => {
  const queryFn = vi.fn().mockResolvedValueOnce({ id: 'usr_1' }).mockRejectedValue(new Error('refused'))
  const queryClient = await queryClientAfter(queryFn)
  await queryClient.refetchQueries({ queryKey: MembershipKeys.of('bob') })

  await Promise.all([retryFailedMembership(queryClient), retryFailedMembership(queryClient)])

  expect(queryFn).toHaveBeenCalledTimes(3)
})
