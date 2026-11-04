import * as z from 'zod'

import { inviteForCreationDtoSchema } from '@/api/generated/models/zod/invite-for-creation-dto'
import { stringWithMaxLengthOf } from '@/lib/api-contract'

const contract = inviteForCreationDtoSchema.shape

/** The Server refuses an email match with no email, and the dialog shows its message on the field. */
export const inviteUserSchema = z.object({
  role: contract.role,
  email: stringWithMaxLengthOf(contract.email.unwrap().unwrap()),
  requireEmailMatch: z.boolean(),
})

export const INVITE_USER_FIELDS = ['role', 'email', 'requireEmailMatch'] as const
