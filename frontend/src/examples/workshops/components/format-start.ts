/** The start of a workshop in the locale and the time zone of the browser, for example `Mar 14, 2026, 9:00 AM`. */
export function formatStart(startsAt: string): string {
  return new Date(startsAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}
