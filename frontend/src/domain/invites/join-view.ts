import type { MutationStatus } from '@tanstack/react-query'

import type { InvitePreviewDto } from '@/api/generated/models/types/invite-preview-dto'
import type { Membership, StartTenantProfile } from '@/domain/membership/apis/get-membership'
import { isRefusal } from '@/domain/membership/apis/get-membership'

/** The text of `Invite.NotValidMessage` and `Invite.CallerHasTenantMessage` of the Server (section 6 of docs/specs/user-invites.md). */
export const LINK_NOT_VALID = 'This invite link is not valid. Ask a tenant admin for a new invite.'
export const ALREADY_MEMBER = 'Your account already belongs to a tenant.'

export type JoinView =
  | { kind: 'loading' }
  | { kind: 'membership-failed'; message: string; retry?: () => void }
  | { kind: 'joining' }
  | { kind: 'member' }
  | { kind: 'link-refused'; message: string }
  | { kind: 'preview-failed'; error: Error }
  | { kind: 'claimable'; preview: InvitePreviewDto; profile: StartTenantProfile }

interface JoinFacts {
  membership: Membership
  /** False when the tab has no token, or a token that the contract of the Server cannot take. */
  linkFits: boolean
  preview: { data?: InvitePreviewDto; error: Error | null }
  /** The refusal of a claim that killed the link. */
  refusedLink: string | null
  claim: MutationStatus
}

export function joinViewOf({ membership, linkFits, preview, refusedLink, claim }: JoinFacts): JoinView {
  if (claim === 'success') return { kind: 'joining' }
  if (membership.status === 'loading') return { kind: 'loading' }
  if (membership.status === 'error') {
    return { kind: 'membership-failed', message: membership.message, retry: membership.retry }
  }
  // The check of a double submit can find the record while the claim still runs.
  if (membership.status === 'member') return { kind: claim === 'pending' ? 'joining' : 'member' }
  if (refusedLink) return { kind: 'link-refused', message: refusedLink }
  if (!linkFits) return { kind: 'link-refused', message: LINK_NOT_VALID }
  // A failed background read keeps the data of the last answer, and the form with the typed values. A refusal wins.
  if (preview.error && (isRefusal(preview.error) || !preview.data)) {
    return isRefusal(preview.error)
      ? { kind: 'link-refused', message: preview.error.displayMessage }
      : { kind: 'preview-failed', error: preview.error }
  }
  return preview.data ? { kind: 'claimable', preview: preview.data, profile: membership.profile } : { kind: 'loading' }
}
