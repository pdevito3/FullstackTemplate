import { useId, useState } from 'react'

import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

import { useDebouncedCallback } from '@/hooks/use-debounced-callback'

interface WorkshopSearchProps {
  search: string
  maxLength: number
  onSearch: (text: string, base: string) => void
}

export function WorkshopSearch({ search, maxLength, onSearch }: WorkshopSearchProps) {
  const id = useId()
  // The text follows a search that the URL gets from outside, during render below (docs/frontend/client-state.md, "Effects").
  // eslint-disable-next-line server-state/no-copied-state
  const [text, setText] = useState(search)
  // eslint-disable-next-line server-state/no-copied-state
  const [shownSearch, setShownSearch] = useState(search)
  const [sentSearch, setSentSearch] = useState<string | null>(null)
  const sendSearch = useDebouncedCallback((next: string, base: string) => {
    setSentSearch(next)
    onSearch(next, base)
  })

  if (search !== shownSearch) {
    setShownSearch(search)
    if (search === sentSearch) {
      setSentSearch(null)
    } else {
      setText(search)
    }
  }

  return (
    <div className="flex max-w-sm flex-col gap-2">
      <Label htmlFor={id}>Search workshops</Label>
      <Input
        id={id}
        type="search"
        value={text}
        maxLength={maxLength}
        placeholder="Title contains"
        onChange={(event) => {
          setText(event.target.value)
          sendSearch(event.target.value, search)
        }}
      />
    </div>
  )
}
