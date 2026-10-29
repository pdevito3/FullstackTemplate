/** What a form shows after the Server refused a save. A message of a field shows on the field instead. */
export type SaveProblem = { kind: 'stale' } | { kind: 'refused'; messages: string[] }

interface RefusedSaveAlertProps {
  problem: SaveProblem | null
}

/** The messages of a refused save that belong to no field of the form, for example a rule about the state of the record. */
export function RefusedSaveAlert({ problem }: RefusedSaveAlertProps) {
  if (problem?.kind !== 'refused') {
    return null
  }

  return (
    <div role="alert" className="rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-sm text-destructive">
      {problem.messages.map((message) => (
        <p key={message}>{message}</p>
      ))}
    </div>
  )
}
