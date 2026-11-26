import type {
  UserPermissionOverrideDto,
  UserPermissionOverrideDtoPermissionEnumKey,
} from '@/api/generated/models/types/user-permission-override-dto'

/** The words of each permission, as `Permission.Words` on the Server gives them. A new permission fails the build here. */
const PERMISSION_LABELS: Record<UserPermissionOverrideDtoPermissionEnumKey, string> = {
  manage_users: 'Manage users',
  manage_user_access: 'Manage the access of users',
  manage_tenant: 'Manage the tenant',
  manage_agents: 'Manage agents',
  read_audit_log: 'Read the audit log',
  read_audit_log_sensitive: 'Read the sensitive values of the audit log',
  read_users: 'See the users of the tenant',
  read_agents: 'See the agents of the tenant',
  // example:start
  manage_workshops: 'Manage workshops',
  // example:end
}

/** The permissions that someone added (granted) or removed by hand, by label, or a dash when there are none. */
export const overrideNames = (overrides: readonly UserPermissionOverrideDto[], granted: boolean) =>
  overrides
    .flatMap((o) => (o.granted === granted ? [PERMISSION_LABELS[o.permission]] : []))
    .sort()
    .join(', ') || '—'

/** A short count of the changes by hand, such as "2 added, 1 removed", or a dash when there are none. */
export const overrideSummary = (overrides: readonly UserPermissionOverrideDto[]) => {
  const added = overrides.filter((o) => o.granted).length
  const removed = overrides.length - added

  return [added && `${added} added`, removed && `${removed} removed`].filter(Boolean).join(', ') || '—'
}
