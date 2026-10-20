import { createElement, type ReactElement, type ReactNode } from 'react'
import { describe, expectTypeOf, it } from 'vitest'

import type { CommandItemProps } from '@/components/ui/command'

// These are type tests. The Vitest typecheck in `pnpm test` checks them. A broken
// rule makes an `@ts-expect-error` line unused or an `expectTypeOf` call fail.
describe('CommandItemProps', () => {
  it('accepts a string child without textValue', () => {
    expectTypeOf<{ children: string }>().toExtend<CommandItemProps>()

    const props: CommandItemProps = { children: 'Calendar' }
    expectTypeOf(props).toExtend<CommandItemProps>()
  })

  it('requires textValue when the children are not a plain string', () => {
    expectTypeOf<{ children: ReactElement }>().not.toExtend<CommandItemProps>()
    expectTypeOf<{ children: ReactNode[] }>().not.toExtend<CommandItemProps>()

    // @ts-expect-error An icon item without textValue has no text for the filter.
    const iconItem: CommandItemProps = { children: [createElement('svg'), 'Calendar'] }
    expectTypeOf(iconItem).toExtend<CommandItemProps>()
  })

  it('accepts other children with textValue', () => {
    expectTypeOf<{ children: ReactNode[]; textValue: string }>().toExtend<CommandItemProps>()

    const props: CommandItemProps = {
      children: [createElement('svg'), 'Calendar'],
      textValue: 'Calendar',
    }
    expectTypeOf(props).toExtend<CommandItemProps>()
  })
})
