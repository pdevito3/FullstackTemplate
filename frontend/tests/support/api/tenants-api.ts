import type { TenantDto } from '@/api/generated/models/types/tenant-dto'
import type { ProblemDetails } from '@/lib/problem-details'
import type { MockRoute } from './mock-route'

interface Buildable<T> {
  build(): T
}

export const tenantsPaths = {
  one: (id: string) => `/api/v1/tenants/${id}`,
}

export const tenantsApi = {
  oneIs(tenant: Buildable<TenantDto>): MockRoute {
    const body = tenant.build()
    return { method: 'get', path: tenantsPaths.one(body.id), status: 200, body }
  },

  oneFails(id: string, problem: Buildable<ProblemDetails>): MockRoute {
    const body = problem.build()
    return { method: 'get', path: tenantsPaths.one(id), status: body.status ?? 500, body }
  },
}
