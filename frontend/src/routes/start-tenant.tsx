import { createFileRoute } from '@tanstack/react-router'

import { Button } from '@/components/ui/button'
import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuthActions } from '@/domain/auth/apis/auth-actions'
import { useMembership } from '@/domain/membership/apis/get-membership'
import { useStartTenant } from '@/domain/membership/apis/start-tenant'
import { StartTenantForm } from '@/domain/membership/components/start-tenant-form'

export const Route = createFileRoute('/start-tenant')({
  component: StartTenantPage,
  staticData: { breadcrumb: 'Start a tenant' },
})

function StartTenantPage() {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <h1 className="text-3xl font-bold tracking-tight">Start a tenant</h1>
      <div className="mt-6">
        <StartTenantContent />
      </div>
    </div>
  )
}

function StartTenantContent() {
  const membership = useMembership()
  const startTenant = useStartTenant()
  const { login } = useAuthActions()

  // Before the loading state: the start makes the membership load again.
  if (startTenant.data) {
    return (
      <div className="flex flex-col items-start gap-4">
        <p className="min-w-0 wrap-anywhere">
          You started {startTenant.data.tenant.name}. You are its admin, and you can now add the other users.
        </p>
        {/* The form that had the focus is gone. */}
        <Link to="/" autoFocus className={buttonVariants()}>Go to the home page</Link>
      </div>
    )
  }

  if (membership.status === 'loading') {
    return <Skeleton role="status" aria-label="Loading your account" className="h-64 max-w-lg" />
  }

  if (membership.status === 'signed-out') {
    return (
      <div className="flex flex-col items-start gap-4">
        <p className="text-muted-foreground">Log in to start a tenant.</p>
        <Button onPress={() => login('/start-tenant')}>Login</Button>
      </div>
    )
  }

  if (membership.status === 'error') {
    return (
      <div role="alert" className="flex flex-col items-start gap-3 rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-destructive">
        <p>We could not check whether you are a member of a tenant. Try again.</p>
        <Button variant="outline" onPress={membership.retry}>Try Again</Button>
      </div>
    )
  }

  if (membership.status === 'member') {
    return (
      <div className="flex flex-col items-start gap-4">
        <p className="text-muted-foreground">You are already a member of a tenant. A user belongs to one tenant only.</p>
        <Link to="/" className={buttonVariants({ variant: 'outline' })}>Go to the home page</Link>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="max-w-lg text-muted-foreground">
        You are not a member of a tenant yet. Start one to use the app. You become its admin, and you can then add the
        other users.
      </p>
      <StartTenantForm profile={membership.profile} onStart={startTenant.mutateAsync} />
    </div>
  )
}
