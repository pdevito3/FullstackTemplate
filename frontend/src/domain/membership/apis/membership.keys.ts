export const MembershipKeys = {
  all: ['membership'] as const,
  of: (identifier: string) => [...MembershipKeys.all, identifier] as const,
}
