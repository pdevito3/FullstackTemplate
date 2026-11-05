import { Upload04Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import type { UseQueryResult } from '@tanstack/react-query'
import { useId, useRef } from 'react'
import { FileTrigger } from 'react-aria-components'

import { Notification } from '@/components/notification'
import { QueryError } from '@/components/query-error'
import { Button } from '@/components/ui/button'

import { AttachmentList } from './attachment-list'
import type { Attachment, AttachmentList as AttachmentListData } from './types'
import { acceptedFileTypes, uploadHint } from './upload-rules'

interface Mutation<TVariables> {
  mutate: (variables: TVariables, options?: { onSuccess?: () => void }) => void
  isPending: boolean
  variables?: TVariables
}

interface AttachmentsSectionProps {
  /** The list query of the record. Its upload rules give the hint and the types that the file chooser shows. */
  list: UseQueryResult<AttachmentListData>
  upload: Mutation<File>
  /** Takes the ID of the attachment. */
  remove: Mutation<string>
  /** From `allowedActions` of the record. */
  canManage: boolean
  emptyText: string
}

/** The Files section of a record: the list, the upload, and the delete. */
export function AttachmentsSection({ list, upload, remove, canManage, emptyText }: AttachmentsSectionProps) {
  const headingId = useId()
  const hintId = useId()
  // A delete removes the focused row, so the focus goes to the heading of the section
  const headingRef = useRef<HTMLHeadingElement>(null)
  const rules = list.data?.uploadRules

  const handleSelect = (files: FileList | null) => {
    const file = files?.[0]
    if (file) {
      upload.mutate(file, { onSuccess: () => Notification.success('File uploaded') })
    }
  }

  const handleDelete = (attachment: Attachment) =>
    remove.mutate(attachment.id, {
      onSuccess: () => {
        Notification.success('File deleted')
        headingRef.current?.focus()
      },
    })

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex min-w-0 flex-col gap-1">
          <h2 id={headingId} ref={headingRef} tabIndex={-1} className="text-lg font-medium outline-none">
            Files
          </h2>
          {canManage && rules && (
            <p id={hintId} className="text-sm text-muted-foreground wrap-anywhere">
              {uploadHint(rules)}
            </p>
          )}
        </div>
        {canManage && (
          <FileTrigger acceptedFileTypes={rules && acceptedFileTypes(rules)} onSelect={handleSelect}>
            <Button variant="outline" isPending={upload.isPending} aria-describedby={rules && hintId}>
              <HugeiconsIcon icon={Upload04Icon} aria-hidden />
              {upload.isPending ? 'Uploading' : 'Upload file'}
            </Button>
          </FileTrigger>
        )}
      </div>
      <AttachmentListState
        list={list}
        canManage={canManage}
        emptyText={emptyText}
        deletingId={remove.isPending ? remove.variables : undefined}
        onDelete={handleDelete}
      />
    </section>
  )
}

interface AttachmentListStateProps {
  list: UseQueryResult<AttachmentListData>
  canManage: boolean
  emptyText: string
  deletingId?: string
  onDelete: (attachment: Attachment) => void
}

function AttachmentListState({ list, canManage, emptyText, deletingId, onDelete }: AttachmentListStateProps) {
  if (list.isPending) {
    return (
      <p role="status" aria-label="Loading files" className="text-sm text-muted-foreground">
        Loading files
      </p>
    )
  }

  if (list.isError) {
    return <QueryError title="The files did not load." error={list.error} onRetry={() => list.refetch()} />
  }

  if (list.data.attachments.length === 0) {
    return <p className="text-sm text-muted-foreground">{emptyText}</p>
  }

  return (
    <AttachmentList
      attachments={list.data.attachments}
      canManage={canManage}
      deletingId={deletingId}
      onDelete={onDelete}
    />
  )
}
