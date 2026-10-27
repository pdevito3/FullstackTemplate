// ============================================================================
// Quick Select Feature - Preset date shortcuts
// ============================================================================

// Helper to get a preset date relative to today
function getPresetDate(daysOffset: number): CalendarDate {
  return today(getLocalTimeZone()).add({ days: daysOffset })
}

// Helper to get week range (Sunday-based)
function getWeekRange(weeksOffset: number): { start: CalendarDate; end: CalendarDate } {
  const todayDate = today(getLocalTimeZone())
  const targetWeekStart = todayDate.add({ weeks: weeksOffset })
  const dayOfWeek = targetWeekStart.toDate(getLocalTimeZone()).getDay()
  const weekStart = targetWeekStart.subtract({ days: dayOfWeek })
  const weekEnd = weekStart.add({ days: 6 })
  return { start: weekStart, end: weekEnd }
}

// Helper to get month range
function getMonthRange(monthsOffset: number): { start: CalendarDate; end: CalendarDate } {
  const todayDate = today(getLocalTimeZone())
  const targetMonth = todayDate.add({ months: monthsOffset })
  const monthStart = new CalendarDate(targetMonth.year, targetMonth.month, 1)
  const monthEnd = monthStart.add({ months: 1 }).subtract({ days: 1 })
  return { start: monthStart, end: monthEnd }
}

// Helper to get year range
function getYearRange(yearsOffset: number): { start: CalendarDate; end: CalendarDate } {
  const todayDate = today(getLocalTimeZone())
  const targetYear = todayDate.year + yearsOffset
  const yearStart = new CalendarDate(targetYear, 1, 1)
  const yearEnd = new CalendarDate(targetYear, 12, 31)
  return { start: yearStart, end: yearEnd }
}

// Single date quick select presets
const singleDatePresets = [
  { label: "Today", getDate: () => getPresetDate(0) },
  { label: "Tomorrow", getDate: () => getPresetDate(1) },
  { label: "Yesterday", getDate: () => getPresetDate(-1) },
] as const

// Date range quick select presets
const dateRangePresets = [
  { label: "This Week", getRange: () => getWeekRange(0) },
  { label: "Last Week", getRange: () => getWeekRange(-1) },
  { label: "Next Week", getRange: () => getWeekRange(1) },
  { label: "This Month", getRange: () => getMonthRange(0) },
  { label: "Last Month", getRange: () => getMonthRange(-1) },
  { label: "Next Month", getRange: () => getMonthRange(1) },
  { label: "This Year", getRange: () => getYearRange(0) },
  { label: "Last Year", getRange: () => getYearRange(-1) },
] as const

