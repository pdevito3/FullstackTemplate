import { useState } from 'react'
import type { FieldValues, Path, UseFormReturn } from 'react-hook-form'

import { isStaleVersionError, validationErrorsOf } from '@/lib/problem-details'

import type { SaveProblem } from './refused-save-alert'

interface Refusal<F extends string> {
  byField: Partial<Record<F, string>>
  problem: SaveProblem | null
}

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
