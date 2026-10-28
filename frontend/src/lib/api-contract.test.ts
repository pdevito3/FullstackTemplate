import { describe, expect, it } from 'vitest'
import * as z from 'zod'

import { tenantForUpdateDtoSchema } from '@/api/generated/models/zod/tenant-for-update-dto'
import { userForCreationDtoSchema } from '@/api/generated/models/zod/user-for-creation-dto'
import { maxLengthOf, maximumOf, stringWithMaxLengthOf } from '@/lib/api-contract'

describe('maxLengthOf', () => {
  const contract = userForCreationDtoSchema.shape.firstName

  it('reads the max length that the generated schema checks', () => {
    const limit = maxLengthOf(contract)

    expect(contract.safeParse('a'.repeat(limit)).success).toBe(true)
    expect(contract.safeParse('a'.repeat(limit + 1)).success).toBe(false)
  })

  it('throws when the contract gives no max length', () => {
    expect(() => maxLengthOf(z.string())).toThrow('The API contract gives this field no max length.')
  })
})

describe('stringWithMaxLengthOf', () => {
  const contract = userForCreationDtoSchema.shape.firstName
  const limit = maxLengthOf(contract)

  it.each([
    ['a letter', 'a'],
    ['an emoji', '\u{1F600}'],
  ])('takes the limit of the Server in %s and refuses one more', (_, character) => {
    const formSchema = stringWithMaxLengthOf(contract)

    expect(formSchema.safeParse(character.repeat(limit)).success).toBe(true)
    expect(formSchema.safeParse(character.repeat(limit + 1)).success).toBe(false)
  })

  it('gives a message that names the limit', () => {
    const result = stringWithMaxLengthOf(contract).safeParse('a'.repeat(limit + 1))

    expect(result.error?.issues[0]?.message).toBe(`Use ${limit} characters or fewer.`)
  })

  it('gives the message of the caller', () => {
    const result = stringWithMaxLengthOf(contract, 'Too long').safeParse('a'.repeat(limit + 1))

    expect(result.error?.issues[0]?.message).toBe('Too long')
  })

  it('chains with a min check', () => {
    const formSchema = stringWithMaxLengthOf(contract).min(1, 'Required')

    expect(formSchema.safeParse('').error?.issues[0]?.message).toBe('Required')
    expect(formSchema.safeParse('a'.repeat(limit)).success).toBe(true)
    expect(formSchema.safeParse('a'.repeat(limit + 1)).success).toBe(false)
  })

  it('throws when the contract gives no max length', () => {
    expect(() => stringWithMaxLengthOf(z.string())).toThrow('The API contract gives this field no max length.')
  })
})

describe('maximumOf', () => {
  it('reads the maximum that the generated schema checks', () => {
    const contract = tenantForUpdateDtoSchema.shape.version
    const maximum = maximumOf(contract)

    expect(contract.safeParse(maximum).success).toBe(true)
    expect(contract.safeParse(maximum + 1).success).toBe(false)
  })

  it.each([
    ['a number', z.number()],
    ['an integer', z.int()],
  ])('throws when the contract gives %s no maximum', (_, contract) => {
    expect(() => maximumOf(contract)).toThrow('The API contract gives this field no maximum.')
  })
})
