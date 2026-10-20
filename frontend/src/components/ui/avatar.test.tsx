import { expect, onTestFinished, test, vi } from 'vitest'
import { commands } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'

interface DemoAvatarProps {
  src?: string
  onLoad?: () => void
}

function DemoAvatar({ src, onLoad }: DemoAvatarProps) {
  return (
    <Avatar>
      <AvatarImage src={src} alt="Avatar" onLoad={onLoad} />
      <AvatarFallback>CN</AvatarFallback>
    </Avatar>
  )
}

// Each src has its own query string, so that the browser cache does not load an image before the test looks at it.
test('avatar shows the initials until each new src loads, and keeps the onLoad of the caller', async () => {
  const screen = await render(<DemoAvatar />)
  const image = screen.getByAltText('Avatar')
  const initials = screen.getByText('CN')
  await expect.element(initials).toBeVisible()

  // The src arrives after the first render.
  const late = '/avatar.png?case=late'
  await commands.holdRequests(late)
  onTestFinished(() => commands.releaseRequests(late))
  await screen.rerender(<DemoAvatar src={late} />)
  await expect.element(initials).toBeVisible()
  await expect.element(image).not.toBeVisible()
  await commands.releaseRequests(late)
  await expect.element(image).toBeVisible()
  await expect.element(initials).not.toBeVisible()

  // The src changes: the initials show again until the new image loads.
  const changed = '/avatar.png?case=changed'
  await commands.holdRequests(changed)
  onTestFinished(() => commands.releaseRequests(changed))
  await screen.rerender(<DemoAvatar src={changed} />)
  await expect.element(initials).toBeVisible()
  await expect.element(image).not.toBeVisible()
  await commands.releaseRequests(changed)
  await expect.element(image).toBeVisible()

  // The caller's onLoad runs, and the image still shows.
  const handleLoad = vi.fn()
  await screen.rerender(<DemoAvatar src="/avatar.png?case=onload" onLoad={handleLoad} />)
  await expect.poll(() => handleLoad.mock.calls.length).toBe(1)
  await expect.element(image).toBeVisible()
  await expect.element(initials).not.toBeVisible()
})
