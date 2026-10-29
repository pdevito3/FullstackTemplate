import { useState } from 'react'
import type { FieldValues, Path, PathValue, UseFormReturn } from 'react-hook-form'

import { latestOverEdits, type TextComparable } from '@/lib/latest-over-edits'

/**
 * The Load the latest button of an edit form after a stale save. `load` reads the record again and gives its form
 * values. Each field that the latest save changed takes the saved value, each other field keeps the edit of the user,
 * and the focus goes to the first changed field. `changed` names those fields for `LatestLoadedNote`.
 */
export function useLoadLatest<T extends FieldValues, F extends Path<T> & string>(
  form: UseFormReturn<T>,
  fields: readonly F[],
) {
  const [isLoading, setIsLoading] = useState(false)
  const [changed, setChanged] = useState<F[] | null>(null)

  /** Returns true when it loaded the latest into the form. */
  const load = async (read: () => Promise<T | undefined>) => {
    setIsLoading(true)
    try {
      const latest = await read()
      if (!latest) return false
      const edited = form.getValues()
      const fieldsOf = (values: T) => values as Partial<Record<F, TextComparable>>
      const result = latestOverEdits(form.formState.defaultValues ?? {}, fieldsOf(edited), fieldsOf(latest), fields)
      // Before the reset, because a reset drops the field refs that setFocus reads until the next render.
      form.setFocus(result.changed[0] ?? fields[0])
      form.reset(latest)
      for (const field of result.kept) form.setValue(field, edited[field] as PathValue<T, F>, { shouldDirty: true })
      setChanged(result.changed)
      return true
    } finally {
      setIsLoading(false)
    }
  }

  return { isLoading, changed, load, clear: () => setChanged(null) }
}
