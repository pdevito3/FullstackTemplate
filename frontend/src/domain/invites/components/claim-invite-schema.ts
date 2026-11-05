import * as z from 'zod'

import { claimInviteDtoSchema } from '@/api/generated/models/zod/claim-invite-dto'
import { stringWithMaxLengthOf } from '@/lib/api-contract'

const contract = claimInviteDtoSchema.shape

/** On an invite with an email match, the Server uses the email of the token, so the form does not ask for one (INV-9). */
export const claimInviteSchema = (requireEmailMatch: boolean) =>
  z.object({
    firstName: stringWithMaxLengthOf(contract.firstName).min(1, 'Enter your first name.'),
    lastName: stringWithMaxLengthOf(contract.lastName).min(1, 'Enter your last name.'),
    email: requireEmailMatch ? z.string() : stringWithMaxLengthOf(contract.email).min(1, 'Enter your email address.'),
    username: stringWithMaxLengthOf(contract.username).min(1, 'Enter a username.'),
  })

export const CLAIM_FIELDS = ['firstName', 'lastName', 'email', 'username'] as const
