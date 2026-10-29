import { useQuery } from '@tanstack/react-query'

import { getVenue } from '@/api/generated/venues/clients/get-venue'

import { VenueKeys } from './venue.keys'

export function useVenue(id: string | undefined, { suppressErrorToast = false } = {}) {
  return useQuery({
    queryKey: VenueKeys.detail(id ?? ''),
    queryFn: ({ signal }) => getVenue({ path: { id: id! }, signal, options: { suppressErrorToast } }),
    enabled: !!id,
  })
}
