import type { InviteDto, InviteDtoStatusEnumKey } from '@/api/generated/models/types/invite-dto'
import { browserTimeZone, formatInstant } from '@/lib/zoned-time'

export type InviteState = InviteDtoStatusEnumKey | 'Expired'

export const INVITE_STATE_LABELS: Record<InviteState, string> = {
  Pending: 'Pending',
  Claimed: 'Claimed',
  Revoked: 'Revoked',
  Expired: 'Expired',
}

/** The Server stores no expired state. A pending invite is expired from its expiry on (`Invite.IsExpiredAt`). */
export const inviteStateOf = ({ status, expiresAt }: Pick<InviteDto, 'status' | 'expiresAt'>, now: Date): InviteState =>
  status === 'Pending' && now.getTime() >= new Date(expiresAt).getTime() ? 'Expired' : status

/** The expiry in the zone of the browser (section 7 of docs/specs/user-invites.md). */
export const formatExpiry = (expiresAt: string): string =>
  formatInstant(new Date(expiresAt), browserTimeZone(), {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
