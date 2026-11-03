import { useMutation, useQueryClient } from '@tanstack/react-query'

import { deleteUser } from '@/api/generated/users/clients/delete-user'
import { refreshCallerMembership } from '@/domain/membership/apis/refresh-caller-membership'

export function useDeleteUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deleteUser({ path: { id } }),
    onSuccess: () => refreshCallerMembership(queryClient),
  })
}
