import type { WorkshopListParams } from './get-workshop-list'

export const WorkshopKeys = {
  all: ['workshops'] as const,
  lists: () => [...WorkshopKeys.all, 'list'] as const,
  list: (params: WorkshopListParams) => [...WorkshopKeys.lists(), params] as const,
  details: () => [...WorkshopKeys.all, 'detail'] as const,
  detail: (id: string) => [...WorkshopKeys.details(), id] as const,
}
