import { Button } from '@/components/ui/button'

interface QueryErrorProps {
  title: string
  error: Error
  onRetry: () => void
}

export function QueryError({ title, error, onRetry }: QueryErrorProps) {
  return (
    <div role="alert" className="flex flex-col items-start gap-3 rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-destructive wrap-anywhere">
      <p className="font-medium">{title}</p>
      <p className="text-sm">{error.message}</p>
      <Button variant="outline" onPress={onRetry}>
        Try again
      </Button>
    </div>
  )
}
