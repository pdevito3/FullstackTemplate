export interface InviteListParams {
  page: number
}

export const InviteKeys = {
  all: ['invites'] as const,
  lists: () => [...InviteKeys.all, 'list'] as const,
  list: (params: InviteListParams) => [...InviteKeys.lists(), params] as const,
  /** A tab has one invite link, and the page reloads for a new one. The token stays out of the key (INV-3). */
  preview: () => [...InviteKeys.all, 'preview'] as const,
}
