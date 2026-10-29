import { Button } from '@/components/ui/button'
import type { SaveProblem } from '@/examples/components/refused-save-alert'

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
      <p className="font-medium text-destructive">Someone else saved this workshop after you opened it.</p>
      <p className="text-foreground">
        Your edits are still in the form, and they are not saved. Load the latest to see the saved workshop, then make
        your edits again.
      </p>
      <Button variant="outline" className="self-start" onPress={onLoadLatest} isPending={isLoadingLatest}>
        Load the latest
      </Button>
    </div>
  )
}
