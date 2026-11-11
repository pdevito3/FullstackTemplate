import { lazy, useState, type ComponentType } from 'react'
import { CodeLoadBoundary } from '@/components/code-load-boundary'
import type { FilterControlProps } from '../types'
import { ControlLoadError } from './control-load-error'

/**
 * A control whose code loads when it first shows. FilterControl gives the Suspense fallback. If the code does not
 * load, the control shows the error and a Try again button, and the page stays.
 */
export function lazyControl(load: () => Promise<ComponentType<FilterControlProps>>): ComponentType<FilterControlProps> {
  const newLazy = () => lazy(async () => ({ default: await load() }))
  // React.lazy keeps a failed load. Try again replaces it, also for each control that shows later. Do not replace it
  // in the lazy itself: React renders a control again after an error, and a control that never showed has no state,
  // so that render starts a new load, which fails again, with no end.
  let Loaded = newLazy()

  return function LazyControl(props: FilterControlProps) {
    // Each control keeps its own lazy component, so a failed Try again in one control does not break a control that loaded.
    const [shown, setShown] = useState(() => ({ Control: Loaded, attempt: 0 }))
    const tryAgain = () => {
      Loaded = newLazy()
      setShown({ Control: Loaded, attempt: shown.attempt + 1 })
    }

    return (
      <CodeLoadBoundary key={shown.attempt} fallback={<ControlLoadError onTryAgain={tryAgain} />}>
        <shown.Control {...props} />
      </CodeLoadBoundary>
    )
  }
}
