import { useId } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import * as z from 'zod'

import type { InitiateUserAndTenantDto } from '@/api/generated/models/types/initiate-user-and-tenant-dto'
import { initiateUserAndTenantDtoSchema } from '@/api/generated/models/zod/initiate-user-and-tenant-dto'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldLabel } from '@/components/ui/field'
import { Form } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { stringWithMaxLengthOf } from '@/lib/api-contract'
import { isProblemDetailsError } from '@/lib/problem-details'

import type { StartTenantProfile } from '../apis/get-membership'

const contract = initiateUserAndTenantDtoSchema.shape
const schema = z.object({
  tenantName: stringWithMaxLengthOf(contract.tenantName).min(1, 'Enter a tenant name.'),
  firstName: stringWithMaxLengthOf(contract.firstName).min(1, 'Enter your first name.'),
  lastName: stringWithMaxLengthOf(contract.lastName).min(1, 'Enter your last name.'),
  email: stringWithMaxLengthOf(contract.email).min(1, 'Enter your email address.'),
  username: stringWithMaxLengthOf(contract.username).min(1, 'Enter a username.'),
})
type StartTenantValues = z.infer<typeof schema>
const FIELD_NAMES = Object.keys(schema.shape) as (keyof StartTenantValues)[]

interface StartTenantFormProps {
  profile: StartTenantProfile
  onStart: (body: InitiateUserAndTenantDto) => Promise<unknown>
}

export function StartTenantForm({ profile: { identifier, ...names }, onStart }: StartTenantFormProps) {
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: { tenantName: '', ...names },
  })
  const id = useId()
  const { errors } = form.formState

  const showRefusal = (error: unknown) => {
    if (!isProblemDetailsError(error)) {
      form.setError('root.server', { message: 'The tenant was not started. Check your connection, then try again.' })
      return
    }
    const fieldErrors = (error.problemDetails.errors ?? {}) as Record<string, string[]>
    let shownOnField = false
    for (const name of FIELD_NAMES) {
      const message = fieldErrors[name]?.[0]
      if (message) {
        form.setError(name, { message }, { shouldFocus: !shownOnField })
        shownOnField = true
      }
    }
    if (!shownOnField) form.setError('root.server', { message: error.displayMessage })
  }

  const handleSubmit = form.handleSubmit(async (values) => {
    try {
      // The first user is the admin, so that someone can add the other users.
      await onStart({ ...values, identifier, role: 'Admin' })
    } catch (error) {
      showRefusal(error)
    }
  })

  const field = (name: keyof StartTenantValues, label: string, type = 'text') => (
    <Field data-invalid={!!errors[name] || undefined}>
      <FieldLabel htmlFor={`${id}-${name}`}>{label}</FieldLabel>
      <Input
        id={`${id}-${name}`}
        type={type}
        // The notice link that was clicked unmounts here, so the focus goes to the first field.
        autoFocus={name === 'tenantName'}
        aria-invalid={!!errors[name] || undefined}
        aria-describedby={`${id}-${name}-error`}
        {...form.register(name)}
      />
      <FieldError id={`${id}-${name}-error`} errors={[errors[name]]} />
    </Field>
  )

  return (
    <Form {...form}>
      <form onSubmit={handleSubmit} noValidate aria-label="Start a tenant" className="flex max-w-lg flex-col gap-5">
        {field('tenantName', 'Tenant name')}
        {field('firstName', 'First name')}
        {field('lastName', 'Last name')}
        {field('email', 'Email', 'email')}
        {field('username', 'Username')}
        <FieldError errors={[errors.root?.server]} />
        <Button type="submit" className="self-start" isPending={form.formState.isSubmitting}>
          Start tenant
        </Button>
      </form>
    </Form>
  )
}
