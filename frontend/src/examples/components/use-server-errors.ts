import { useState } from 'react'
import type { FieldValues, Path, UseFormReturn } from 'react-hook-form'

import { isStaleVersionError, validationErrorsOf } from '@/lib/problem-details'

import type { SaveProblem } from './refused-save-alert'

interface Refusal<F extends string> {
  byField: Partial<Record<F, string>>
  problem: SaveProblem | null
}

/**
 * Sorts the answer of a refused save: a stale version is its own problem, a validation message of a field goes on the
 * field, and each other message goes above the buttons. An error with no message for a field, such as a 500, is a
 * message above the buttons, so the form always tells the user why the save failed.
 */
export function refusalOf<F extends string>(error: unknown, fields: readonly F[]): Refusal<F> {
  if (isStaleVersionError(error)) {
    return { byField: {}, problem: { kind: 'stale' } }
  }
  const byField: Partial<Record<F, string>> = {}
  const other: string[] = []
  for (const [name, messages] of Object.entries(validationErrorsOf(error))) {
    const field = fields.find((f) => f === name)
    if (field) {
      byField[field] = messages.join(' ')
    } else {
      other.push(...messages)
    }
  }
  if (other.length === 0 && Object.keys(byField).length === 0) {
    other.push(error instanceof Error && error.message ? error.message : 'The save failed. Try again.')
  }
  return { byField, problem: other.length > 0 ? { kind: 'refused', messages: other } : null }
}

/** Shows a refused save on the form. The save sends no toast, so the form is the one place that shows it. */
export function useServerErrors<T extends FieldValues>(form: UseFormReturn<T>, fields: readonly Path<T>[]) {
  const [problem, setProblem] = useState<SaveProblem | null>(null)

  const show = (error: unknown) => {
    const refusal = refusalOf(error, fields)
    for (const field of fields) {
      const message = refusal.byField[field]
      if (message) form.setError(field, { type: 'server', message })
    }
    setProblem(refusal.problem)
  }

  return { problem, show, clear: () => setProblem(null) }
}
