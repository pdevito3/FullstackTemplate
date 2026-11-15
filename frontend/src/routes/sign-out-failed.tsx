import { createFileRoute } from '@tanstack/react-router'

import { Button } from '@/components/ui/button'
import { logout } from '@/domain/auth/apis/auth-actions'
import { useAuth } from '@/domain/auth/apis/get-user'
import { AuthFailure } from '@/domain/auth/components/auth-failure'

export const Route = createFileRoute('/sign-out-failed')({
  component: SignOutFailed,
  staticData: { breadcrumb: 'Sign out failed', withoutSession: true },
})

function SignOutFailed() {
  const { logoutUrl, isLoading } = useAuth()

  return (
    <AuthFailure title="We could not sign you out" action="You may still be signed in. Sign out again to end your session.">
      <Button className="mt-6" isPending={isLoading} onPress={() => logout(logoutUrl ?? undefined)}>
        Sign out again
      </Button>
    </AuthFailure>
  )
}
