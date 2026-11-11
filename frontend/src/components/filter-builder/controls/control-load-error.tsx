import { Button } from '@/components/ui/button'

/** Shows in place of a filter control whose code did not load. `autoFocus` keeps the focus on Try again after a failed try. */
export function ControlLoadError({ onTryAgain, autoFocus }: { onTryAgain: () => void; autoFocus: boolean }) {
  return (
    <div className="p-3">
      <div role="alert" className="flex w-full flex-col items-start gap-3 rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-destructive md:w-72">
        <p className="font-medium">This filter did not load.</p>
        <p className="text-sm">Check your connection, then try again. If it still does not load, reload the page.</p>
        <Button variant="outline" onPress={onTryAgain} autoFocus={autoFocus}>
          Try again
        </Button>
      </div>
    </div>
  )
}
