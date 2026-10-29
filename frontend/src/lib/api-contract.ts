import * as z from 'zod'

/**
 * Reads the max length of a string field from the generated Zod schema of its DTO, so a form keeps no copy of the
 * number. Use the number in a message. To check a value, use `stringWithMaxLengthOf`, because `z.string().max()` and
 * the `maxLength` attribute of an input count UTF-16 units and refuse text that the Server takes. It throws when the contract has no
 * limit, so a test fails and the form does not quietly take any length. To fix it, give the DTO property
 * `[SchemaMaxLength]` on the Server, then regenerate.
 */
export function maxLengthOf(contract: z.ZodString): number {
  if (typeof contract.maxLength !== 'number') {
    throw new Error('The API contract gives this field no max length. Give the DTO property [SchemaMaxLength].')
  }
  return contract.maxLength
}

/**
 * Makes a form string that takes the max length of the API contract. It counts characters (code points), as the
 * Server and JSON Schema do, so an emoji counts as one. It throws when the contract has no limit, as `maxLengthOf`
 * does.
 *
 * @example
 * ```typescript
 * const firstName = stringWithMaxLengthOf(userForCreationDtoSchema.shape.firstName).min(1, 'Required')
 * ```
 */
export function stringWithMaxLengthOf(contract: z.ZodString, message?: string): z.ZodString {
  const limit = maxLengthOf(contract)
  return z.string().refine((value) => [...value].length <= limit, {
    message: message ?? `Use ${limit} characters or fewer.`,
  })
}

/**
 * Reads the largest value of a number field from the generated Zod schema of its DTO. It throws when the contract has
 * no maximum. To fix it, give the DTO property `[SchemaMaximum]` on the Server, then regenerate. A `uint` field always
 * has the maximum 4294967295 in the contract, so this check does not catch a missing `[SchemaMaximum]` on a `uint`.
 */
export function maximumOf(contract: z.ZodNumber): number {
  // maxValue is never null for a number: it is Infinity for z.number() and the largest safe integer for z.int().
  // Only a max check comes from the contract.
  const maximums = (contract._zod.def.checks ?? []).filter((check) => check._zod.def.check === 'less_than')
  if (maximums.length === 0 || contract.maxValue === null) {
    throw new Error('The API contract gives this field no maximum. Give the DTO property [SchemaMaximum].')
  }
  if (maximums.some((check) => !(check as z.core.$ZodCheckLessThan)._zod.def.inclusive)) {
    throw new Error('The API contract gives this field an exclusive maximum. Write an inclusive maximum on the Server.')
  }
  return contract.maxValue
}

/**
 * Reads the smallest value of a number field from the generated Zod schema of its DTO. It throws when the contract has
 * no minimum. To fix it, give the DTO property `[SchemaMinimum]` on the Server, then regenerate. A `uint` field always
 * has the minimum 0 in the contract, so this check does not catch a missing `[SchemaMinimum]` on a `uint`.
 */
export function minimumOf(contract: z.ZodNumber): number {
  // minValue is never null for a number: it is -Infinity for z.number() and the smallest safe integer for z.int().
  // Only a min check comes from the contract.
  const minimums = (contract._zod.def.checks ?? []).filter((check) => check._zod.def.check === 'greater_than')
  if (minimums.length === 0 || contract.minValue === null) {
    throw new Error('The API contract gives this field no minimum. Give the DTO property [SchemaMinimum].')
  }
  if (minimums.some((check) => !(check as z.core.$ZodCheckGreaterThan)._zod.def.inclusive)) {
    throw new Error('The API contract gives this field an exclusive minimum. Write an inclusive minimum on the Server.')
  }
  return contract.minValue
}
