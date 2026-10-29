import type { DateValue } from '@internationalized/date'

export type TextComparable = string | number | boolean | DateValue | null | undefined

export interface LatestOverEdits<F extends string> {
  changed: F[]
  kept: F[]
}

/**
 * After a stale save, which fields of the form take the latest record, and which keep the edits of the user. A field
 * that the latest record changed takes the saved value, so the save cannot overwrite a change that the user did not
 * see.
 */
export function latestOverEdits<F extends string>(
  loaded: Partial<Record<F, unknown>>,
  edited: Partial<Record<F, TextComparable>>,
  latest: Partial<Record<F, TextComparable>>,
  fields: readonly F[],
): LatestOverEdits<F> {
  const sameText = (a: unknown, b: TextComparable) => String(a) === String(b)
  const changed = fields.filter((field) => !sameText(loaded[field], latest[field]))
  const kept = fields.filter((field) => !changed.includes(field) && !sameText(edited[field], latest[field]))
  return { changed, kept }
}
