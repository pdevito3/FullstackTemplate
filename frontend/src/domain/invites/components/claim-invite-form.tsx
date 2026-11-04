import { zodResolver } from '@hookform/resolvers/zod'
import { useId, useMemo } from 'react'
import { useForm } from 'react-hook-form'

import { RefusedSaveAlert } from '@/components/refused-save-alert'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Form } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { useServerErrors } from '@/hooks/use-server-errors'
import { focusFirstError } from '@/lib/focus-first-error'
import { isProblemDetailsError } from '@/lib/problem-details'

import type { ClaimValues } from '../apis/claim-invite'
import { CLAIM_FIELDS, claimInviteSchema } from './claim-invite-schema'

interface ClaimInviteFormProps {
  profile: ClaimValues
  requireEmailMatch: boolean
  onClaim: (values: ClaimValues) => Promise<void>
}

export function ClaimInviteForm({ profile, requireEmailMatch, onClaim }: ClaimInviteFormProps) {
  const schema = useMemo(() => claimInviteSchema(requireEmailMatch), [requireEmailMatch])
  const form = useForm({ resolver: zodResolver(schema), defaultValues: profile, shouldFocusError: false })
  const serverErrors = useServerErrors(form, CLAIM_FIELDS)
  const id = useId()
  const { errors } = form.formState

  const handleSubmit = form.handleSubmit(async (values) => {
    serverErrors.clear()
    try {
      await onClaim(values)
    } catch (error) {
      serverErrors.show(
        isProblemDetailsError(error) ? error : new Error('You did not join the tenant. Check your connection, then try again.'),
      )
    }
  }, focusFirstError(form, CLAIM_FIELDS))

  const field = (name: (typeof CLAIM_FIELDS)[number], label: string, type = 'text') => (
    <Field data-invalid={!!errors[name] || undefined}>
      <FieldLabel htmlFor={`${id}-${name}`}>{label}</FieldLabel>
      <Input
        id={`${id}-${name}`}
        type={type}
        aria-invalid={!!errors[name] || undefined}
        aria-describedby={`${id}-${name}-error`}
        {...form.register(name)}
      />
      <FieldError id={`${id}-${name}-error`} errors={[errors[name]]} />
    </Field>
  )

  return (
    <Form {...form}>
      <form onSubmit={handleSubmit} noValidate aria-label="Join the tenant" className="flex max-w-lg flex-col gap-5">
        {field('firstName', 'First name')}
        {field('lastName', 'Last name')}
        {requireEmailMatch ? (
          <Field>
            <FieldLabel htmlFor={`${id}-email`}>Email</FieldLabel>
            <Input id={`${id}-email`} type="email" readOnly aria-describedby={`${id}-email-note`} {...form.register('email')} />
            <FieldDescription id={`${id}-email-note`}>
              This invite is for one email address, so the app uses the email of your account.
            </FieldDescription>
          </Field>
        ) : (
          field('email', 'Email', 'email')
        )}
        {field('username', 'Username')}
        <RefusedSaveAlert problem={serverErrors.problem} />
        <Button type="submit" className="self-start" isPending={form.formState.isSubmitting}>
          Join tenant
        </Button>
      </form>
    </Form>
  )
}
