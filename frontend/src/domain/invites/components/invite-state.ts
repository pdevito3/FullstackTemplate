import type { InviteDto, InviteDtoStatusEnumKey } from '@/api/generated/models/types/invite-dto'
import { browserTimeZone, formatInstant } from '@/lib/zoned-time'

export type InviteState = InviteDtoStatusEnumKey | 'Expired'

export const INVITE_STATE_LABELS: Record<InviteState, string> = {
  Pending: 'Pending',
  Claimed: 'Claimed',
  Revoked: 'Revoked',
  Expired: 'Expired',
}

/** The Server stores no expired state. It computes the flag, so the clock of the browser cannot change the state. */
export const inviteStateOf = ({ status, isExpired }: Pick<InviteDto, 'status' | 'isExpired'>): InviteState =>
  isExpired ? 'Expired' : status

/** The expiry in the zone of the browser (section 7 of docs/specs/user-invites.md). */
export const formatExpiry = (expiresAt: string): string =>
  formatInstant(new Date(expiresAt), browserTimeZone(), {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
