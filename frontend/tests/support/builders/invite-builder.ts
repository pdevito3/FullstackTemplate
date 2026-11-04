import type { InviteDto } from '@/api/generated/models/types/invite-dto'
import type { InvitePreviewDto } from '@/api/generated/models/types/invite-preview-dto'

let seed = 0
const next = () => ++seed

export const inviteExpiry = '2099-01-15T12:00:00Z'

class InviteBuilder {
  private readonly invite: InviteDto

  private constructor(invite: InviteDto) {
    this.invite = invite
  }

  static create() {
    return new InviteBuilder({
      id: `inv_${next()}`,
      tenantId: 'tnt_1',
      role: 'User',
      email: null,
      requireEmailMatch: false,
      expiresAt: inviteExpiry,
      status: 'Pending',
      claimedByUserId: null,
      allowedActions: ['Revoke'],
      version: 1,
    })
  }

  private with(patch: Partial<InviteDto>) {
    return new InviteBuilder({ ...this.invite, ...patch })
  }

  forEmail(email: string) {
    return this.with({ email })
  }

  matchingEmail(email: string) {
    return this.with({ email, requireEmailMatch: true })
  }

  inRole(role: InviteDto['role']) {
    return this.with({ role })
  }

  /** The Server stores no expired state. The status stays Pending, and the Server still allows a revoke. */
  expired() {
    return this.with({ expiresAt: '2020-01-15T12:00:00Z' })
  }

  claimed() {
    return this.with({ status: 'Claimed', allowedActions: [] })
  }

  revoked() {
    return this.with({ status: 'Revoked', allowedActions: [] })
  }

  build(): InviteDto {
    return this.invite
  }
}

export const anInvite = () => InviteBuilder.create()

export const aPreview = (preview: Partial<InvitePreviewDto> = {}): InvitePreviewDto => ({
  tenantName: 'Acme',
  role: 'User',
  expiresAt: inviteExpiry,
  requireEmailMatch: false,
  ...preview,
})
