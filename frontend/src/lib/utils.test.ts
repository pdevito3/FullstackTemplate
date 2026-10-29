import { expect, test } from 'vitest'

import { cn } from '@/lib/utils'

test('a max width of the caller replaces max-w-overlay', () => {
  expect(cn('w-(--trigger-width) max-w-overlay', 'max-w-[200px]')).toBe('w-(--trigger-width) max-w-[200px]')
})
