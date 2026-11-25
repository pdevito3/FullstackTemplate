import { Component, type ReactNode } from 'react'
import { isModuleNotFoundError } from '@tanstack/router-core'

interface CodeLoadBoundaryProps {
  children: ReactNode
  /** Shows in place of the children when the code of a lazy component in them did not load. */
  fallback: ReactNode
}

interface CodeLoadBoundaryState {
  caught?: { error: unknown }
}

/**
 * Catches a failed load of the code of a lazy component, so that only the children show the error. Another error
 * goes on to the next boundary, such as the page error of the route. React.lazy keeps a failed load, so to load the
 * code again, render a new lazy component under a new boundary.
 */
export class CodeLoadBoundary extends Component<CodeLoadBoundaryProps, CodeLoadBoundaryState> {
  state: CodeLoadBoundaryState = {}

  static getDerivedStateFromError(cause: unknown) {
    return { caught: { error: cause } }
  }

  render() {
    const { caught } = this.state

    if (!caught) return this.props.children

    if (!isModuleNotFoundError(caught.error)) throw caught.error

    return this.props.fallback
  }
}
