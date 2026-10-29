import { useMutation, useQueryClient } from '@tanstack/react-query'

import type { VenueForCreationDto } from '@/api/generated/models/types/venue-for-creation-dto'
import { addVenue } from '@/api/generated/venues/clients/add-venue'

import { VenueKeys } from './venue.keys'

export function useAddVenue() {
  const queryClient = useQueryClient()

  return useMutation({
    // The form shows each refusal next to its fields, so a toast would say it twice.
    mutationFn: (body: VenueForCreationDto) => addVenue({ body, options: { suppressErrorToast: true } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: VenueKeys.lists() }),
  })
}
