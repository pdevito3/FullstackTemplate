import { useId, useState } from 'react'

import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useDebouncedCallback } from '@/hooks/use-debounced-callback'

interface SearchBoxProps {
  label: string
  placeholder?: string
  /** The search in the URL. */
  search: string
  /** The limit of the search parameter in the contract, from `maxLengthOf`. */
  maxLength: number
  /** After the debounce. `base` is the search of the URL when the user typed, so the page can drop a search that a later navigation replaced. */
  onSearch: (text: string, base: string) => void
}

/**
 * The search box of a list. The URL owns the search: a new search from outside, such as Back, replaces the text.
 * A search that this box sent comes back through the URL too, and it keeps the text, because the user can have
 * typed more since the debounce.
 */
export function SearchBox({ label, placeholder, search, maxLength, onSearch }: SearchBoxProps) {
  const id = useId()
  const [text, setText] = useState(search)
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
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="search"
        value={text}
        maxLength={maxLength}
        placeholder={placeholder}
        onChange={(event) => {
          setText(event.target.value)
          sendSearch(event.target.value, search)
        }}
      />
    </div>
  )
}
