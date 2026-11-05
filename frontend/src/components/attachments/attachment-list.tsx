import { File02Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { type ReactElement, useState } from 'react'

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
import { formatFileSize } from '@/lib/format-file-size'
import { cn } from '@/lib/utils'
import { browserTimeZone, formatInstant } from '@/lib/zoned-time'

import type { Attachment } from './types'

interface AttachmentListProps {
  attachments: Attachment[]
  canManage: boolean
  deletingId?: string
  onDelete: (attachment: Attachment) => void
}

export function AttachmentList({ attachments, canManage, deletingId, onDelete }: AttachmentListProps) {
  return (
    <ul aria-label="Files" className="flex flex-col divide-y rounded-lg border">
      {attachments.map((attachment) => (
        <li key={attachment.id} aria-label={attachment.fileName} className="flex flex-wrap items-center gap-3 p-3">
          {/* In a narrow row, the buttons go to a second line, so the name keeps its width */}
          <div className="flex min-w-0 flex-1 basis-48 items-center gap-3">
            <Thumbnail attachment={attachment} />
            <div className="flex min-w-0 flex-col">
              <span className="text-sm font-medium wrap-anywhere">{attachment.fileName}</span>
              <span className="text-xs text-muted-foreground">
                {formatFileSize(attachment.sizeBytes)} · {formatUploadedAt(attachment.uploadedAt)}
              </span>
            </div>
          </div>
          <div className="ml-auto flex flex-wrap justify-end gap-2">
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

// The thumbnail and the file icon share one tile. The color of the border replaces the transparent border of the
// button, and the muted background shows behind a transparent image.
const TILE = 'size-12 shrink-0 rounded-md border border-border bg-muted'

function Thumbnail({ attachment }: { attachment: Attachment }) {
  // A reload of the list signs new URLs, so an image that failed to load gets a new try with the new URL
  const [failedUrl, setFailedUrl] = useState<string>()
  const { previewUrl } = attachment

  if (!previewUrl || previewUrl === failedUrl) {
    return (
      <span className={cn(TILE, 'flex items-center justify-center text-muted-foreground')}>
        <HugeiconsIcon icon={File02Icon} aria-hidden className="size-6" />
      </span>
    )
  }

  return (
    <DialogTrigger>
      <Button variant="ghost" aria-label={`View ${attachment.fileName}`} className={cn(TILE, 'overflow-hidden p-0')}>
        <img
          src={previewUrl}
          alt={attachment.fileName}
          onError={() => setFailedUrl(previewUrl)}
          className="size-full object-cover"
        />
      </Button>
      <Dialog className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{attachment.fileName}</DialogTitle>
        </DialogHeader>
        <img
          src={previewUrl}
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
