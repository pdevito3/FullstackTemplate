import type { UserPermissionOverrideDto } from '@/api/generated/models/types/user-permission-override-dto'

/** The permissions that someone added (granted) or removed by hand, sorted, or a dash when there are none. */
export const overrideNames = (overrides: readonly UserPermissionOverrideDto[], granted: boolean) =>
  overrides.filter((o) => o.granted === granted).map((o) => o.permission).sort().join(', ') || '—'

/** A short count of the changes by hand, such as "2 added, 1 removed", or a dash when there are none. */
export const overrideSummary = (overrides: readonly UserPermissionOverrideDto[]) => {
  const added = overrides.filter((o) => o.granted).length
  const removed = overrides.length - added
  return [added && `${added} added`, removed && `${removed} removed`].filter(Boolean).join(', ') || '—'
}
