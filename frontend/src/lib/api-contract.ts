import * as z from 'zod'

/**
 * Reads the max length of a string field from the generated Zod schema of its DTO, so a form keeps no copy of the
 * number. Use the number in a message. To check a value, use `stringWithMaxLengthOf`, because `z.string().max()` and
 * the `maxLength` attribute of an input count UTF-16 units and refuse text that the Server takes. It throws when the contract has no
 * limit, so a test fails and the form does not quietly take any length. To fix it, give the DTO property
 * `[SchemaMaxLength]` on the Server, then regenerate.
 */
export function maxLengthOf(contract: z.ZodString): number {
  if (contract.maxLength === null) {
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
 * no maximum. To fix it, give the DTO property `[SchemaMaximum]` on the Server, then regenerate.
 */
export function maximumOf(contract: z.ZodNumber): number {
  // maxValue is never null for a number: it is Infinity for z.number() and the largest safe integer for z.int().
  // Only a max check comes from the contract.
  const hasMaximum = contract._zod.def.checks?.some((check) => check._zod.def.check === 'less_than') ?? false
  if (!hasMaximum || contract.maxValue === null) {
    throw new Error('The API contract gives this field no maximum. Give the DTO property [SchemaMaximum].')
  }
  return contract.maxValue
}
