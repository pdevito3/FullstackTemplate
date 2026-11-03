import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import { AttachmentsSection } from '@/components/attachments/attachments-section'

import { useAddWorkshopAttachment } from '../apis/add-workshop-attachment'
import { useDeleteWorkshopAttachment } from '../apis/delete-workshop-attachment'
import { useWorkshopAttachments } from '../apis/get-workshop-attachments'

interface WorkshopAttachmentsProps {
  workshop: Pick<WorkshopDto, 'id' | 'allowedActions'>
}

export function WorkshopAttachments({ workshop }: WorkshopAttachmentsProps) {
  const attachments = useWorkshopAttachments(workshop.id)
  const add = useAddWorkshopAttachment(workshop.id)
  const remove = useDeleteWorkshopAttachment(workshop.id)

  return (
    <AttachmentsSection
      list={attachments}
      upload={add}
      remove={remove}
      canManage={workshop.allowedActions.includes('ManageAttachments')}
      emptyText="This workshop has no files."
    />
  )
}
