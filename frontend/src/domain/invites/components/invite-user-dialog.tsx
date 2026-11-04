import { zodResolver } from '@hookform/resolvers/zod'
import { useId, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'

import type { AddInviteResponseDto } from '@/api/generated/models/types/add-invite-response-dto'
import { RefusedSaveAlert } from '@/components/refused-save-alert'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Form } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { isUserRole, ROLE_LABELS, USER_ROLES } from '@/domain/users/components/user-role'
import { useServerErrors } from '@/hooks/use-server-errors'
import { focusFirstError } from '@/lib/focus-first-error'
import { isProblemDetailsError } from '@/lib/problem-details'

import { useAddInvite } from '../apis/add-invite'
import { inviteLinkOf } from '../invite-link'
import { formatExpiry } from './invite-state'
import { INVITE_USER_FIELDS, inviteUserSchema } from './invite-user-schema'

export function InviteUserButton() {
  const add = useAddInvite()
  // The link shows one time only, so a stray press outside the dialog or on Escape must not lose it or its create.
  const holdsDialog = add.isPending || !!add.data

  return (
    <ResponsiveDialogTrigger onOpenChange={(isOpen) => { if (!isOpen) add.reset() }}>
      <Button>Invite a user</Button>
      <ResponsiveDialog isDismissable={!holdsDialog} isKeyboardDismissDisabled={holdsDialog} showCloseButton={!add.isPending}>
        {add.data ? <InviteLink created={add.data} /> : <InviteForm add={add} />}
      </ResponsiveDialog>
    </ResponsiveDialogTrigger>
  )
}

function InviteForm({ add }: { add: ReturnType<typeof useAddInvite> }) {
  const form = useForm({
    resolver: zodResolver(inviteUserSchema),
    defaultValues: { role: 'User' as const, email: '', requireEmailMatch: false },
    shouldFocusError: false,
  })
  const serverErrors = useServerErrors(form, INVITE_USER_FIELDS)
  const id = useId()
  const { errors } = form.formState

  const handleSubmit = form.handleSubmit(async ({ email, ...values }) => {
    // Enter submits again while the button waits, and a second create would replace the link of the first.
    if (add.isPending) return
    serverErrors.clear()
    try {
      await add.mutateAsync({ ...values, email: email.trim() || null })
    } catch (error) {
      serverErrors.show(
        isProblemDetailsError(error) ? error : new Error('The invite was not created. Check your connection, then try again.'),
      )
    }
  }, focusFirstError(form, INVITE_USER_FIELDS))

  return (
    <Form {...form}>
      <form onSubmit={handleSubmit} noValidate aria-label="Invite a user">
        <ResponsiveDialogHeader>
          <ResponsiveDialogTitle>Invite a user</ResponsiveDialogTitle>
          <ResponsiveDialogDescription>
            You get a link to send to your colleague. The colleague signs in and joins this tenant.
          </ResponsiveDialogDescription>
        </ResponsiveDialogHeader>
        <div className="flex flex-col gap-5 pt-5 pb-4">
          <Controller
            control={form.control}
            name="role"
            render={({ field }) => (
              <Select
                value={field.value}
                onChange={(key) => {
                  if (isUserRole(key)) field.onChange(key)
                }}
                className="flex w-full flex-col gap-2"
              >
                <Label>Role</Label>
                <SelectTrigger ref={field.ref} className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {USER_ROLES.map((role) => (
                    <SelectItem key={role} id={role}>
                      {ROLE_LABELS[role]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          <Field data-invalid={!!errors.email || undefined}>
            <FieldLabel htmlFor={`${id}-email`}>Email (optional)</FieldLabel>
            <Input
              id={`${id}-email`}
              type="email"
              aria-invalid={!!errors.email || undefined}
              aria-describedby={`${id}-email-error`}
              {...form.register('email')}
            />
            <FieldError id={`${id}-email-error`} errors={[errors.email]} />
          </Field>
          <Controller
            control={form.control}
            name="requireEmailMatch"
            render={({ field }) => (
              <Field>
                <Checkbox isSelected={field.value} onChange={field.onChange} aria-describedby={`${id}-match-note`}>
                  Require email match
                </Checkbox>
                <FieldDescription id={`${id}-match-note`}>
                  Only an account with the email above can use the link. A match needs an email.
                </FieldDescription>
              </Field>
            )}
          />
          <RefusedSaveAlert problem={serverErrors.problem} />
        </div>
        <ResponsiveDialogFooter>
          <ResponsiveDialogClose isDisabled={add.isPending}>Cancel</ResponsiveDialogClose>
          <Button type="submit" isPending={add.isPending}>
            Create link
          </Button>
        </ResponsiveDialogFooter>
      </form>
    </Form>
  )
}

type CopyState = 'idle' | 'copied' | 'failed'

function InviteLink({ created: { invite, token } }: { created: AddInviteResponseDto }) {
  const [copy, setCopy] = useState<CopyState>('idle')
  const id = useId()
  const link = inviteLinkOf(window.location.origin, token)

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(link)
      setCopy('copied')
    } catch {
      setCopy('failed')
    }
  }

  return (
    <>
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>Send this link to your colleague</ResponsiveDialogTitle>
        <ResponsiveDialogDescription>
          This is the only time that this link shows, because the app stores only a hash of it.
        </ResponsiveDialogDescription>
      </ResponsiveDialogHeader>
      <div className="flex flex-col gap-3 pt-5 pb-4">
        <Field>
          <FieldLabel htmlFor={`${id}-link`}>Invite link</FieldLabel>
          <Textarea
            id={`${id}-link`}
            readOnly
            spellCheck={false}
            value={link}
            onFocus={(event) => event.target.select()}
            className="resize-none wrap-anywhere"
          />
        </Field>
        <p className="text-sm text-muted-foreground">One person can use this link, until {formatExpiry(invite.expiresAt)}.</p>
        <div className="flex flex-wrap items-center gap-3">
          {/* The form with the focused button unmounted, so the focus moves here and not to the body. */}
          <Button variant="outline" autoFocus onPress={() => void handleCopy()}>
            Copy link
          </Button>
          <p role="status" className="text-sm text-muted-foreground empty:hidden">
            {copy === 'copied' ? 'Link copied.' : ''}
          </p>
        </div>
        {copy === 'failed' && <FieldError>The copy did not work. Select the link and copy it.</FieldError>}
      </div>
      <ResponsiveDialogFooter>
        <ResponsiveDialogClose variant="default">Done</ResponsiveDialogClose>
      </ResponsiveDialogFooter>
    </>
  )
}
