export type SaveProblem = { kind: 'stale' } | { kind: 'refused'; messages: string[] }

interface RefusedSaveAlertProps {
  problem: SaveProblem | null
}

export function RefusedSaveAlert({ problem }: RefusedSaveAlertProps) {
  if (problem?.kind !== 'refused') {
    return null
  }

  return (
    <div role="alert" className="rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-sm text-destructive wrap-anywhere">
      {problem.messages.map((message, i) => (
        <p key={`${i}-${message}`}>{message}</p>
      ))}
    </div>
  )
}
