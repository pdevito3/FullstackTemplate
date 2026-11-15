import { createFileRoute } from '@tanstack/react-router'

import { Button } from '@/components/ui/button'
import { login } from '@/domain/auth/apis/auth-actions'
import { AuthFailure } from '@/domain/auth/components/auth-failure'

export const Route = createFileRoute('/sign-in-failed')({
  component: SignInFailed,
  staticData: { breadcrumb: 'Sign in failed', withoutSession: true },
})

function SignInFailed() {
  return (
    <AuthFailure title="We could not sign you in" action="Sign in again to continue.">
      <Button className="mt-6" onPress={() => login()}>
        Sign in again
      </Button>
    </AuthFailure>
  )
}
