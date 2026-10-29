import type { DateValue } from '@internationalized/date'

/** A value that compares by its text. An object such as `{ id, name }` does not: each one is `[object Object]`. */
type FieldValue = string | number | boolean | DateValue | null | undefined

export interface LatestOverEdits<F extends string> {
  changed: F[]
  kept: F[]
}

/**
 * After a stale save, which fields of the form take the latest record, and which keep the edits of the user. A field
 * that the latest record changed takes the saved value, so the save cannot overwrite a change that the user did not
 * see. The values compare by their text, so a number, a string, and a date value of `@internationalized/date` work.
 */
export function latestOverEdits<F extends string>(
  loaded: Partial<Record<F, unknown>>,
  edited: Partial<Record<F, FieldValue>>,
  latest: Partial<Record<F, FieldValue>>,
  fields: readonly F[],
): LatestOverEdits<F> {
  const same = (a: unknown, b: FieldValue) => String(a) === String(b)
  const changed = fields.filter((field) => !same(loaded[field], latest[field]))
  const kept = fields.filter((field) => !changed.includes(field) && !same(edited[field], latest[field]))
  return { changed, kept }
}
