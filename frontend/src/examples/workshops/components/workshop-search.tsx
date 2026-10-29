import { useId, useState } from 'react'

import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

import { useDebouncedCallback } from '../../use-debounced-callback'

interface WorkshopSearchProps {
  search: string
  maxLength: number
  /** Puts the text in the URL. `base` is the search that the URL had when the user typed. */
  onSearch: (text: string, base: string) => void
}

/** The text goes to the URL when the user stops typing, so each key press does not load the list. */
export function WorkshopSearch({ search, maxLength, onSearch }: WorkshopSearchProps) {
  const id = useId()
  const [text, setText] = useState(search)
  const [shownSearch, setShownSearch] = useState(search)
  // The search that this box sent and that the URL does not show yet.
  const [sentSearch, setSentSearch] = useState<string | null>(null)
  const sendSearch = useDebouncedCallback((next: string, base: string) => {
    setSentSearch(next)
    onSearch(next, base)
  })

  // A change of the URL from outside, such as Back or a link, shows its search. The search that this box sent does
  // not replace the text, because the user can type more before the URL changes.
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
        // The Server counts the search in UTF-16 units, as the maxLength attribute does.
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
