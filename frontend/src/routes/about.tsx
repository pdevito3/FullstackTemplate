import { createFileRoute } from '@tanstack/react-router'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'

export const Route = createFileRoute('/about')({
  component: About,
})

function About() {
  return (
    <div className="min-h-screen bg-background">
      <main className="max-w-2xl mx-auto px-4 py-8">
        <Card>
          <CardHeader>
            <CardTitle className="text-3xl">About</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-muted-foreground leading-relaxed">
              This is a demo application built with .NET Aspire and React, showcasing modern distributed application development.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The stack includes:
            </p>
            <ul className="list-disc list-inside text-muted-foreground space-y-2">
              <li>
                <strong className="text-foreground">.NET Aspire</strong> - Cloud-native orchestration
              </li>
              <li>
                <strong className="text-foreground">React 19</strong> - UI framework
              </li>
              <li>
                <strong className="text-foreground">TanStack Router</strong> - Type-safe routing
              </li>
              <li>
                <strong className="text-foreground">TanStack Query</strong> - Data fetching
              </li>
              <li>
                <strong className="text-foreground">Tailwind CSS</strong> - Styling
              </li>
              <li>
                <strong className="text-foreground">Base UI + shadcn</strong> - Component library
              </li>
              <li>
                <strong className="text-foreground">Duende BFF</strong> - Backend-for-Frontend security
              </li>
            </ul>
          </CardContent>
        </Card>
      </main>
    </div>
  )
}
