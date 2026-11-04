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
      isExpired: false,
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

  /**
   * The status stays Pending, and the Server still allows a revoke. The expiry stays in the future, so a test fails if
   * the SPA reads the clock of the browser and not the flag of the Server.
   */
  expired() {
    return this.with({ isExpired: true })
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
