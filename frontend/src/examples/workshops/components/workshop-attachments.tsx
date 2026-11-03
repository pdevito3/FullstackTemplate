import { File02Icon, Upload04Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { type ReactElement, useRef, useState } from 'react'
import { FileTrigger } from 'react-aria-components'

import type { WorkshopAttachmentDto } from '@/api/generated/models/types/workshop-attachment-dto'
import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import { Notification } from '@/components/notification'
import { QueryError } from '@/components/query-error'
import { Button } from '@/components/ui/button'
import { buttonVariants } from '@/components/ui/button-variants'
import { Dialog, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'
import { cn } from '@/lib/utils'
import { browserTimeZone, formatInstant } from '@/lib/zoned-time'

import { useAddWorkshopAttachment } from '../apis/add-workshop-attachment'
import { useDeleteWorkshopAttachment } from '../apis/delete-workshop-attachment'
import { useWorkshopAttachments } from '../apis/get-workshop-attachments'
import { formatFileSize } from './format-file-size'

interface WorkshopAttachmentsProps {
  workshop: Pick<WorkshopDto, 'id' | 'allowedActions'>
}

export function WorkshopAttachments({ workshop }: WorkshopAttachmentsProps) {
  const attachments = useWorkshopAttachments(workshop.id)
  const add = useAddWorkshopAttachment(workshop.id)
  const remove = useDeleteWorkshopAttachment(workshop.id)
  // A delete removes the focused row, so the focus goes to the heading of the section
  const headingRef = useRef<HTMLHeadingElement>(null)
  const canManage = workshop.allowedActions.includes('ManageAttachments')

  const handleSelect = (files: FileList | null) => {
    const file = files?.[0]
    if (file) {
      add.mutate(file, { onSuccess: () => Notification.success('File uploaded') })
    }
  }

  const handleDelete = (attachment: WorkshopAttachmentDto) =>
    remove.mutate(attachment.id, {
      onSuccess: () => {
        Notification.success('File deleted')
        headingRef.current?.focus()
      },
    })

  return (
    <section aria-labelledby="workshop-files-heading" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="workshop-files-heading" ref={headingRef} tabIndex={-1} className="text-lg font-medium outline-none">
          Files
        </h2>
        {canManage && (
          <FileTrigger onSelect={handleSelect}>
            <Button variant="outline" isPending={add.isPending}>
              <HugeiconsIcon icon={Upload04Icon} aria-hidden />
              {add.isPending ? 'Uploading' : 'Upload file'}
            </Button>
          </FileTrigger>
        )}
      </div>
      <AttachmentList
        attachments={attachments}
        canManage={canManage}
        deletingId={remove.isPending ? remove.variables : undefined}
        onDelete={handleDelete}
      />
    </section>
  )
}

interface AttachmentListProps {
  attachments: ReturnType<typeof useWorkshopAttachments>
  canManage: boolean
  deletingId?: string
  onDelete: (attachment: WorkshopAttachmentDto) => void
}

function AttachmentList({ attachments, canManage, deletingId, onDelete }: AttachmentListProps) {
  if (attachments.isPending) {
    return (
      <p role="status" aria-label="Loading files" className="text-sm text-muted-foreground">
        Loading files
      </p>
    )
  }

  if (attachments.isError) {
    return <QueryError title="The files did not load." error={attachments.error} onRetry={() => attachments.refetch()} />
  }

  if (attachments.data.length === 0) {
    return <p className="text-sm text-muted-foreground">This workshop has no files.</p>
  }

  return (
    <ul aria-label="Files" className="flex flex-col divide-y rounded-lg border">
      {attachments.data.map((attachment) => (
        <li key={attachment.id} aria-label={attachment.fileName} className="flex items-center gap-3 p-3">
          {/* A new URL after a reload gives a new image, so an image that failed can load again */}
          <Thumbnail key={attachment.previewUrl ?? attachment.id} attachment={attachment} />
          <div className="flex min-w-0 flex-1 flex-col">
            <span className="text-sm font-medium wrap-anywhere">{attachment.fileName}</span>
            <span className="text-xs text-muted-foreground">
              {formatFileSize(attachment.sizeBytes)} · {formatUploadedAt(attachment.uploadedAt)}
            </span>
          </div>
          <div className="flex shrink-0 flex-wrap justify-end gap-2">
            <a
              href={attachment.downloadUrl}
              aria-label={`Download ${attachment.fileName}`}
              className={buttonVariants({ variant: 'outline', size: 'sm' })}
            >
              Download
            </a>
            {canManage && (
              <ConfirmDelete fileName={attachment.fileName} onConfirm={() => onDelete(attachment)}>
                <Button
                  variant="destructive"
                  size="sm"
                  aria-label={`Delete ${attachment.fileName}`}
                  isPending={deletingId === attachment.id}
                >
                  Delete
                </Button>
              </ConfirmDelete>
            )}
          </div>
        </li>
      ))}
    </ul>
  )
}

function formatUploadedAt(uploadedAt: string): string {
  return formatInstant(new Date(uploadedAt), browserTimeZone(), { month: 'short', day: 'numeric', year: 'numeric' })
}

const TILE = 'size-12 shrink-0 rounded-md'

function Thumbnail({ attachment }: { attachment: WorkshopAttachmentDto }) {
  const [failed, setFailed] = useState(false)

  if (!attachment.previewUrl || failed) {
    return (
      <span className={cn(TILE, 'flex items-center justify-center bg-muted text-muted-foreground')}>
        <HugeiconsIcon icon={File02Icon} aria-hidden className="size-6" />
      </span>
    )
  }

  return (
    <DialogTrigger>
      <Button variant="ghost" aria-label={`View ${attachment.fileName}`} className={cn(TILE, 'overflow-hidden p-0')}>
        <img
          src={attachment.previewUrl}
          alt={attachment.fileName}
          onError={() => setFailed(true)}
          className="size-full object-cover"
        />
      </Button>
      <Dialog className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{attachment.fileName}</DialogTitle>
        </DialogHeader>
        <img
          src={attachment.previewUrl}
          alt={attachment.fileName}
          className="max-h-[70vh] w-full rounded-md object-contain"
        />
      </Dialog>
    </DialogTrigger>
  )
}

function ConfirmDelete({
  fileName,
  children,
  onConfirm,
}: {
  fileName: string
  children: ReactElement
  onConfirm: () => void
}) {
  return (
    <ResponsiveDialogTrigger>
      {children}
      <ResponsiveDialog>
        <ResponsiveDialogHeader>
          <ResponsiveDialogTitle>Delete {fileName}?</ResponsiveDialogTitle>
          <ResponsiveDialogDescription>You cannot get the file back after you delete it.</ResponsiveDialogDescription>
        </ResponsiveDialogHeader>
        <ResponsiveDialogFooter>
          <ResponsiveDialogClose>Keep file</ResponsiveDialogClose>
          <ResponsiveDialogClose variant="destructive" onPress={onConfirm}>
            Delete file
          </ResponsiveDialogClose>
        </ResponsiveDialogFooter>
      </ResponsiveDialog>
    </ResponsiveDialogTrigger>
  )
}
