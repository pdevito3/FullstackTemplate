import { Button } from '@/components/ui/button'
import { failureText } from '@/lib/request-failure'

interface QueryErrorProps {
  title: string
  error: unknown
  isRetrying?: boolean
  onRetry: () => void
}

export function QueryError({ title, error, isRetrying, onRetry }: QueryErrorProps) {
  return (
    <div role="alert" className="flex flex-col items-start gap-3 rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-destructive wrap-anywhere">
      <p className="font-medium">{title}</p>
      <p className="text-sm">{failureText(error)}</p>
      <Button variant="outline" isPending={isRetrying} onPress={onRetry}>
        Try again
      </Button>
    </div>
  )
}
