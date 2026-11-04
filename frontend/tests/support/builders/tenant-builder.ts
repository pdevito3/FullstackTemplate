import type { TenantDto } from '@/api/generated/models/types/tenant-dto'

class TenantBuilder {
  private readonly tenant: TenantDto

  private constructor(tenant: TenantDto) {
    this.tenant = tenant
  }

  static create() {
    // The tenant of the user that `aUser` builds.
    return new TenantBuilder({ id: 'tnt_1', name: 'Acme', allowedActions: [], version: 1 })
  }

  private with(patch: Partial<TenantDto>) {
    return new TenantBuilder({ ...this.tenant, ...patch })
  }

  named(name: string) {
    return this.with({ name })
  }

  seenByAdmin() {
    return this.with({ allowedActions: ['InviteUser'] })
  }

  build(): TenantDto {
    return this.tenant
  }
}

export const aTenant = () => TenantBuilder.create()
