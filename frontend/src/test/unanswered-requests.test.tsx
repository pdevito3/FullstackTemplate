import { expect, test } from 'vitest'
import { commands } from 'vitest/browser'

const path = '/api/v1/no-answer'

function send() {
  return new Promise<void>((resolve) => {
    const request = new XMLHttpRequest()
    request.addEventListener('loadend', () => resolve())
    request.open('GET', path)
    request.send()
  })
}

let requestOfLastTest = Promise.resolve()

test('a test can end while its request is on the way', async () => {
  await commands.holdRequests(path)

  requestOfLastTest = send()
})

test('a request of the last test that arrives before the answers of this test does not count for this test', async () => {
  await commands.releaseRequests(path)
  await requestOfLastTest

  expect(await commands.takeUnansweredRequests()).toEqual([])
})

test('a request of this test with no answer counts for this test', async () => {
  await send()

  expect(await commands.takeUnansweredRequests()).toEqual([`GET ${path}`])
})
