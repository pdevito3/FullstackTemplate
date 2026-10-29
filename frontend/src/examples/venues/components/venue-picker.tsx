import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Collection, Text, type Key } from 'react-aria-components'

import type { VenueDto } from '@/api/generated/models/types/venue-dto'
import { getVenueListQuerySearchSchema } from '@/api/generated/venues/zod/get-venue-list'
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
  ComboboxLoadMore,
} from '@/components/ui/combobox'
import { Label } from '@/components/ui/label'
import { maxLengthOf } from '@/lib/api-contract'

import { useDebouncedCallback } from '../../use-debounced-callback'
import { useVenue } from '../apis/get-venue'
import { useVenueSearch } from '../apis/get-venue-list'
import { VenueKeys } from '../apis/venue.keys'

const searchMaxLength = maxLengthOf(getVenueListQuerySearchSchema.unwrap())

/**
 * The options are the pages of the search, and the picked venue when no loaded page has it. React Aria shows the
 * name of the pick only while the pick is an option, so a pick from a later page or from a link stays in the list.
 */
function optionsWithPick(venues: VenueDto[], pick: VenueDto | undefined): VenueDto[] {
  if (!pick || venues.some((venue) => venue.id === pick.id)) {
    return venues
  }
  return [pick, ...venues]
}

interface VenuePickerProps {
  value: string
  onChange: (venueId: string) => void
  onBlur?: () => void
  errorMessage?: string
}

export function VenuePicker({ value, onChange, onBlur, errorMessage }: VenuePickerProps) {
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const sendText = useDebouncedCallback(setText)
  const pick = useVenue(value || undefined)
  // After a pick, the input shows the name of the venue. That text is not a search.
  const results = useVenueSearch(text === pick.data?.name ? '' : text.trim())
  const venues = results.data?.pages.flatMap((page) => page.venues) ?? []
  const options = optionsWithPick(venues, pick.data)

  const handleSelectionChange = (key: Key | null) => {
    const venue = options.find((option) => option.id === key)
    if (venue) queryClient.setQueryData(VenueKeys.detail(venue.id), venue)
    onChange(venue?.id ?? '')
  }

  return (
    // The items prop turns off the filter of React Aria, because the Server already filtered the venues.
    <Combobox
      items={options}
      value={value || null}
      onChange={handleSelectionChange}
      onInputChange={sendText}
      onBlur={onBlur}
      isInvalid={!!errorMessage}
      allowsEmptyCollection
      className="flex flex-col gap-2"
    >
      <Label>Venue</Label>
      <ComboboxInput placeholder="Search venues" maxLength={searchMaxLength} className="w-full" />
      {errorMessage && (
        <Text slot="errorMessage" className="text-sm text-destructive">
          {errorMessage}
        </Text>
      )}
      <ComboboxContent>
        <ComboboxList renderEmptyState={() => <ComboboxEmpty>{results.isPending ? 'Loading venues' : 'No venue found.'}</ComboboxEmpty>}>
          <Collection items={options}>
            {(venue) => (
              <ComboboxItem id={venue.id} textValue={venue.name}>
                {venue.name}
              </ComboboxItem>
            )}
          </Collection>
          {results.hasNextPage && (
            <ComboboxLoadMore isLoading={results.isFetchingNextPage} onLoadMore={() => results.fetchNextPage()} />
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}
