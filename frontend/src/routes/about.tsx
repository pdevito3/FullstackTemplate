import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/about')({
  component: About,
})

function About() {
  return (
    <div className="w-full min-h-screen flex flex-col bg-linear-to-br from-bg-gradient-start to-bg-gradient-end text-text-primary">
      <main className="flex-1 max-w-[1400px] w-full mx-auto px-8 py-16 flex flex-col items-center justify-center max-md:px-4">
        <div className="bg-card-bg backdrop-blur-md rounded-2xl px-8 py-10 shadow-lg border border-weather-card-border max-w-2xl w-full">
          <h1 className="text-3xl font-bold mb-6 bg-linear-to-br from-text-primary to-text-secondary bg-clip-text text-transparent">
            About
          </h1>
          <p className="text-text-secondary mb-4 leading-relaxed">
            This is a demo application built with .NET Aspire and React, showcasing modern distributed application development.
          </p>
          <p className="text-text-secondary mb-4 leading-relaxed">
            The stack includes:
          </p>
          <ul className="list-disc list-inside text-text-secondary space-y-2 mb-6">
            <li>
              <strong className="text-text-primary">.NET Aspire</strong> - Cloud-native orchestration
            </li>
            <li>
              <strong className="text-text-primary">React 19</strong> - UI framework
            </li>
            <li>
              <strong className="text-text-primary">TanStack Router</strong> - Type-safe routing
            </li>
            <li>
              <strong className="text-text-primary">TanStack Query</strong> - Data fetching
            </li>
            <li>
              <strong className="text-text-primary">Tailwind CSS</strong> - Styling
            </li>
            <li>
              <strong className="text-text-primary">Duende BFF</strong> - Backend-for-Frontend security
            </li>
          </ul>
        </div>
      </main>
    </div>
  )
}
