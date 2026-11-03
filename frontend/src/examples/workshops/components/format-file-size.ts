const KILOBYTE = 1024
const MEGABYTE = 1024 * KILOBYTE

/**
 * Formats a size in bytes, for example `12 kB` or `3.4 MB`. A size that rounds to 1000 or more of a unit shows in the
 * next unit, so a size never shows as `1,000 kB`.
 */
export function formatFileSize(bytes: number, locale?: string): string {
  if (bytes < 1000) {
    return new Intl.NumberFormat(locale, { style: 'unit', unit: 'byte', unitDisplay: 'long' }).format(bytes)
  }
  const kilobytes = bytes / KILOBYTE
  const [value, unit] = Math.round(kilobytes * 10) < 10_000 ? [kilobytes, 'kilobyte'] : [bytes / MEGABYTE, 'megabyte']
  return new Intl.NumberFormat(locale, { style: 'unit', unit, maximumFractionDigits: 1 }).format(value)
}
