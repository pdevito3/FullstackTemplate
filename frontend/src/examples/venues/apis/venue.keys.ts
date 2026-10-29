export const VenueKeys = {
  all: ['venues'] as const,
  lists: () => [...VenueKeys.all, 'list'] as const,
  list: (search: string) => [...VenueKeys.lists(), search] as const,
  details: () => [...VenueKeys.all, 'detail'] as const,
  detail: (id: string) => [...VenueKeys.details(), id] as const,
}
