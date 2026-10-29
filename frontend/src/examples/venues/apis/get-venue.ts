import { useQuery } from '@tanstack/react-query'

import { getVenue } from '@/api/generated/venues/clients/get-venue'

import { VenueKeys } from './venue.keys'

export function useVenue(id: string | undefined) {
  return useQuery({
    queryKey: VenueKeys.detail(id ?? ''),
    queryFn: ({ signal }) => getVenue({ path: { id: id! }, signal }),
    enabled: !!id,
  })
}
