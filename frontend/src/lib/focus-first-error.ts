import { get, type FieldErrors, type FieldValues, type Path, type UseFormReturn } from 'react-hook-form'

/**
 * The invalid handler of `handleSubmit` for a form with `shouldFocusError: false`. It focuses the first field with an
 * error in the order of `fields`, the order of the form. React Hook Form focuses in the order that the fields
 * registered, and a `Controller` registers after the plain inputs of its parent.
 */
export function focusFirstError<T extends FieldValues>(form: UseFormReturn<T>, fields: readonly Path<T>[]) {
  return (errors: FieldErrors<T>) => {
    const first = fields.find((field) => get(errors, field))
    if (first) form.setFocus(first)
  }
}
