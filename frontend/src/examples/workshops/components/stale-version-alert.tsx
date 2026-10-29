import { Button } from '@/components/ui/button'
import type { SaveProblem } from '@/components/refused-save-alert'

interface StaleVersionAlertProps {
  problem: SaveProblem | null
  isLoadingLatest: boolean
  onLoadLatest: () => void
}

export function StaleVersionAlert({ problem, isLoadingLatest, onLoadLatest }: StaleVersionAlertProps) {
  if (problem?.kind !== 'stale') {
    return null
  }

  return (
    <div role="alert" className="flex flex-col gap-3 rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-sm">
      <p className="font-medium text-destructive">This workshop changed after you opened it.</p>
      <p className="text-foreground">
        Your edits are still in the form, and they are not saved. Load the latest to see what changed. The form keeps
        your edits to each field that the latest save did not change.
      </p>
      <Button variant="outline" className="self-start" onPress={onLoadLatest} isPending={isLoadingLatest}>
        Load the latest
      </Button>
    </div>
  )
}

interface LatestLoadedNoteProps<F extends string> {
  changed: F[] | null
  labels: Record<F, string>
}

const listFormat = new Intl.ListFormat('en', { type: 'conjunction' })

export function LatestLoadedNote<F extends string>({ changed, labels }: LatestLoadedNoteProps<F>) {
  if (changed === null) {
    return null
  }

  return (
    <p role="status" className="rounded-lg border p-4 text-sm">
      {changed.length > 0
        ? `A save after you opened this workshop changed ${listFormat.format(changed.map((field) => labels[field]))}. The form shows the saved values of these fields. Your edits to the other fields are still in the form. Check the form, then save.`
        : 'The latest workshop has no change to the fields of this form. Your edits are still in the form. Save again to keep them.'}
    </p>
  )
}
