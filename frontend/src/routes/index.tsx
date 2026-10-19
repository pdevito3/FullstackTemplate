import { createFileRoute, Link } from '@tanstack/react-router'
import { DashboardSquare01Icon, InformationCircleIcon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { Card, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'

export const Route = createFileRoute('/')({
  component: Index,
})

const pages = [
  {
    title: 'Components',
    description: 'The UI kit, with each component in its states',
    url: '/components',
    icon: DashboardSquare01Icon,
  },
  {
    title: 'About',
    description: 'The stack of this template',
    url: '/about',
    icon: InformationCircleIcon,
  },
] as const

function Index() {
  return (
    <div className="min-h-screen bg-background">
      <main className="max-w-4xl mx-auto px-4 py-8">
        <div className="mb-8">
          <h1 className="text-3xl font-bold tracking-tight">Fullstack Template</h1>
          <p className="text-muted-foreground mt-1">.NET Aspire + React</p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {pages.map((page) => (
            <Link key={page.url} to={page.url}>
              <Card className="h-full transition-shadow hover:shadow-md">
                <CardHeader>
                  <HugeiconsIcon icon={page.icon} className="w-6 h-6 text-muted-foreground mb-2" />
                  <CardTitle>{page.title}</CardTitle>
                  <CardDescription>{page.description}</CardDescription>
                </CardHeader>
              </Card>
            </Link>
          ))}
        </div>
      </main>
    </div>
  )
}
