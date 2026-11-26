import { useMutation, useQueryClient } from '@tanstack/react-query'

import type { UpdateUserRolesDtoRolesEnumKey } from '@/api/generated/models/types/update-user-roles-dto'
import { updateUserRoles } from '@/api/generated/users/clients/update-user-roles'
import { refreshCallerMembership } from '@/domain/membership/apis/refresh-caller-membership'

import { UserKeys } from './user.keys'

export function useUpdateUserRoles(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (roles: UpdateUserRolesDtoRolesEnumKey[]) => updateUserRoles({ path: { id }, body: { roles } }),
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
