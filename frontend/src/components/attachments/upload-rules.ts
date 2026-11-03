import { formatFileSize } from '@/lib/format-file-size'

import type { UploadRules } from './types'

/** The limits of the Server for the user, for example `PNG, JPEG, or PDF. Up to 10 MB.` */
export function uploadHint(rules: UploadRules, locale?: string): string {
  const types = new Intl.ListFormat(locale, { type: 'disjunction' }).format(rules.fileTypes.map((t) => t.name))
  return `${types}. Up to ${formatFileSize(rules.maxSizeBytes, locale)}.`
}

/**
 * The `accept` list of the file chooser. It has the extensions and the media types, because an operating system can
 * match a file by either one. The chooser only filters the list, so the Server still checks each file.
 */
export function acceptedFileTypes(rules: UploadRules): string[] {
  return rules.fileTypes.flatMap((t) => [...t.extensions, t.mediaType])
}
