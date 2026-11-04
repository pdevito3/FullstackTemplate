import { useMutation, useQueryClient } from '@tanstack/react-query'

import type { UpdateUserRoleDtoRoleEnumKey } from '@/api/generated/models/types/update-user-role-dto'
import { updateUserRole } from '@/api/generated/users/clients/update-user-role'
import { refreshCallerMembership } from '@/domain/membership/apis/refresh-caller-membership'

import { UserKeys } from './user.keys'

export function useUpdateUserRole(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (role: UpdateUserRoleDtoRoleEnumKey) => updateUserRole({ path: { id }, body: { role } }),
    onMutate: () => queryClient.cancelQueries({ queryKey: UserKeys.detail(id) }),
    onSuccess: (user) => {
      queryClient.setQueryData(UserKeys.detail(id), user)
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: UserKeys.lists() }),
        refreshCallerMembership(queryClient),
      ])
    },
    onError: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: UserKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: UserKeys.lists() }),
      ]),
  })
}
