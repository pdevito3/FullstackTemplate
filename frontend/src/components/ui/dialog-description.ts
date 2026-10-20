import * as React from "react"

/**
 * React Aria links a description to its dialog only for an `alertdialog`. A
 * dialog wrapper gives this context and sets `aria-describedby` to the id
 * that its description element registers.
 */
const DialogDescriptionContext = React.createContext<
  ((id: string | undefined) => void) | null
>(null)

/** The id of a description element. The element registers it with its dialog while it is mounted. */
function useDialogDescriptionId(id?: string) {
  const register = React.useContext(DialogDescriptionContext)
  const generatedId = React.useId()
  const descriptionId = id ?? generatedId

  React.useLayoutEffect(() => {
    register?.(descriptionId)
    return () => register?.(undefined)
  }, [register, descriptionId])

  return descriptionId
}

export { DialogDescriptionContext, useDialogDescriptionId }
