import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/')({
  component: Index,
})

function Index() {
  return (
    <main className="max-w-4xl mx-auto px-4 py-8">
      <h1 className="text-3xl font-bold tracking-tight">Fullstack Template</h1>
      <p className="text-gray-500 mt-1">.NET Aspire + React</p>
    </main>
  )
}
