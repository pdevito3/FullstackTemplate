export const TenantKeys = {
  all: ['tenants'] as const,
  details: () => [...TenantKeys.all, 'detail'] as const,
  detail: (id: string) => [...TenantKeys.details(), id] as const,
}
