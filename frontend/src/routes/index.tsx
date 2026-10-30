import { createFileRoute } from '@tanstack/react-router'
import { Login01Icon, Logout01Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'

import { useAuth } from '@/domain/auth/apis/get-user'
import { useAuthActions } from '@/domain/auth/apis/auth-actions'
import { useCurrentUser } from '@/domain/users/apis/get-current-user'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeading } from '@/components/page-heading'

export const Route = createFileRoute('/')({
  component: Home,
  staticData: { breadcrumb: 'Home' },
})

function Home() {
  const auth = useAuth()
  const { login, logout } = useAuthActions()

  return (
    <div className="bg-background">
      <div className="max-w-2xl mx-auto px-4 py-8">
        <PageHeading>Fullstack Template</PageHeading>
        <p className="text-muted-foreground mt-1">.NET Aspire + React with a BFF</p>

        <Card className="mt-8">
          <CardHeader>
            <CardTitle>
              <h2>Your session</h2>
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center justify-between gap-4">
            {auth.isLoading ? (
              <Skeleton className="h-5 w-48" aria-label="Loading your session" />
            ) : auth.isLoggedIn ? (
              <>
                <SignedInUser />
                <Button variant="outline" onPress={() => auth.logoutUrl && logout(auth.logoutUrl)}>
                  <HugeiconsIcon icon={Logout01Icon} className="size-4" />
                  Logout
                </Button>
              </>
            ) : (
              <>
                <p className="text-sm text-muted-foreground">You are not signed in.</p>
                <Button onPress={() => login('/')}>
                  <HugeiconsIcon icon={Login01Icon} className="size-4" />
                  Login
                </Button>
              </>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function SignedInUser() {
  const { data: user, isLoading, isError } = useCurrentUser()

  if (isLoading) {
    return <Skeleton className="h-5 w-48" aria-label="Loading your user" />
  }
  if (isError || !user) {
    return <p className="text-sm text-destructive">The Server could not load your user.</p>
  }
  return (
    <p className="min-w-0 text-sm wrap-anywhere">
      Signed in as <span className="font-medium">{user.name ?? user.identifier}</span>
    </p>
  )
}
