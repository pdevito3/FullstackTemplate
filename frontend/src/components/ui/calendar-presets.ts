import { CalendarDate, getDayOfWeek } from "@internationalized/date"

import { todayAt, type TimeZone } from "@/lib/zoned-time"

// ============================================================================
// Quick Select Feature - Preset date shortcuts
// ============================================================================

// Helper to get a preset date relative to today
function getPresetDate(zone: TimeZone, daysOffset: number): CalendarDate {
  return todayAt(zone, new Date()).add({ days: daysOffset })
}

// Helper to get week range (Sunday-based)
function getWeekRange(zone: TimeZone, weeksOffset: number): { start: CalendarDate; end: CalendarDate } {
  const targetWeekStart = todayAt(zone, new Date()).add({ weeks: weeksOffset })
  const weekStart = targetWeekStart.subtract({ days: getDayOfWeek(targetWeekStart, "en-US") })
  const weekEnd = weekStart.add({ days: 6 })
  return { start: weekStart, end: weekEnd }
}

// Helper to get month range
function getMonthRange(zone: TimeZone, monthsOffset: number): { start: CalendarDate; end: CalendarDate } {
  const targetMonth = todayAt(zone, new Date()).add({ months: monthsOffset })
  const monthStart = new CalendarDate(targetMonth.year, targetMonth.month, 1)
  const monthEnd = monthStart.add({ months: 1 }).subtract({ days: 1 })
  return { start: monthStart, end: monthEnd }
}

// Helper to get year range
function getYearRange(zone: TimeZone, yearsOffset: number): { start: CalendarDate; end: CalendarDate } {
  const targetYear = todayAt(zone, new Date()).year + yearsOffset
  const yearStart = new CalendarDate(targetYear, 1, 1)
  const yearEnd = new CalendarDate(targetYear, 12, 31)
  return { start: yearStart, end: yearEnd }
}

// Single date quick select presets
const singleDatePresets = [
  { label: "Today", getDate: (zone: TimeZone) => getPresetDate(zone, 0) },
  { label: "Tomorrow", getDate: (zone: TimeZone) => getPresetDate(zone, 1) },
  { label: "Yesterday", getDate: (zone: TimeZone) => getPresetDate(zone, -1) },
] as const

// Date range quick select presets
const dateRangePresets = [
  { label: "This Week", getRange: (zone: TimeZone) => getWeekRange(zone, 0) },
  { label: "Last Week", getRange: (zone: TimeZone) => getWeekRange(zone, -1) },
  { label: "Next Week", getRange: (zone: TimeZone) => getWeekRange(zone, 1) },
  { label: "This Month", getRange: (zone: TimeZone) => getMonthRange(zone, 0) },
  { label: "Last Month", getRange: (zone: TimeZone) => getMonthRange(zone, -1) },
  { label: "Next Month", getRange: (zone: TimeZone) => getMonthRange(zone, 1) },
  { label: "This Year", getRange: (zone: TimeZone) => getYearRange(zone, 0) },
  { label: "Last Year", getRange: (zone: TimeZone) => getYearRange(zone, -1) },
] as const

export { dateRangePresets, singleDatePresets }
