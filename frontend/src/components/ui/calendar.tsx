"use client"

import { createContext, useContext, useState, useMemo, useEffect } from "react"
import { CalendarDate, getLocalTimeZone, today } from "@internationalized/date"
import { ArrowLeft01Icon, ArrowRight01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"
import {
  Button as AriaButton,
  Calendar as AriaCalendar,
  CalendarCell as AriaCalendarCell,
  CalendarGrid as AriaCalendarGrid,
  CalendarGridBody as AriaCalendarGridBody,
  CalendarGridHeader as AriaCalendarGridHeader,
  CalendarHeaderCell as AriaCalendarHeaderCell,
  CalendarStateContext as AriaCalendarStateContext,
  Heading as AriaHeading,
  RangeCalendar as AriaRangeCalendar,
  RangeCalendarStateContext as AriaRangeCalendarStateContext,
  composeRenderProps,
  Text,
  useLocale,
  type CalendarCellProps as AriaCalendarCellProps,
  type CalendarGridBodyProps as AriaCalendarGridBodyProps,
  type CalendarGridHeaderProps as AriaCalendarGridHeaderProps,
  type CalendarGridProps as AriaCalendarGridProps,
  type CalendarHeaderCellProps as AriaCalendarHeaderCellProps,
  type CalendarProps as AriaCalendarProps,
  type DateValue as AriaDateValue,
  type RangeCalendarProps as AriaRangeCalendarProps,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { buttonVariants } from "@/components/ui/button"

const Calendar = AriaCalendar

const RangeCalendar = AriaRangeCalendar

// ============================================================================
// Calendar Zoom Feature - Month/Year Navigation
// ============================================================================

type CalendarView = "days" | "months" | "years"

interface CalendarZoomContextValue {
  view: CalendarView
  setView: (view: CalendarView) => void
  viewYear: number
  setViewYear: (year: number) => void
  viewDecadeStart: number
  setViewDecadeStart: (year: number) => void
}

const CalendarZoomContext = createContext<CalendarZoomContextValue | null>(null)

function useCalendarState() {
  const calendarState = useContext(AriaCalendarStateContext)
  const rangeState = useContext(AriaRangeCalendarStateContext)
  return calendarState ?? rangeState
}

interface CalendarZoomProviderProps {
  children: React.ReactNode
}

function CalendarZoomProvider({ children }: CalendarZoomProviderProps) {
  const state = useCalendarState()
  const currentYear = state?.focusedDate?.year ?? new Date().getFullYear()

  const [view, setView] = useState<CalendarView>("days")
  const [viewYear, setViewYear] = useState(currentYear)
  const [viewDecadeStart, setViewDecadeStart] = useState(
    Math.floor(currentYear / 12) * 12
  )

  // Sync viewYear when calendar's focused date changes
  useEffect(() => {
    if (state?.focusedDate) {
      setViewYear(state.focusedDate.year)
      setViewDecadeStart(Math.floor(state.focusedDate.year / 12) * 12)
    }
  }, [state?.focusedDate?.year])

  const contextValue = useMemo(
    () => ({
      view,
      setView,
      viewYear,
      setViewYear,
      viewDecadeStart,
      setViewDecadeStart,
    }),
    [view, viewYear, viewDecadeStart]
  )

  return (
    <CalendarZoomContext.Provider value={contextValue}>
      {children}
    </CalendarZoomContext.Provider>
  )
}

// Helper to get days in a month
function getDaysInMonth(year: number, month: number): number {
  return new Date(year, month, 0).getDate()
}

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

// Quick selects panel for single date calendar
interface QuickSelectsPanelProps {
  onSelect: (date: CalendarDate) => void
}

function QuickSelectsPanel({ onSelect }: QuickSelectsPanelProps) {
  return (
    <div className="flex flex-col gap-1 border-r pr-3 mr-3">
      <div className="text-xs font-medium text-muted-foreground mb-1">Quick Select</div>
      {singleDatePresets.map((preset) => (
        <button
          key={preset.label}
          type="button"
          onClick={() => onSelect(preset.getDate())}
          className={cn(
            buttonVariants({ variant: "ghost", size: "sm" }),
            "justify-start h-7 text-xs font-normal"
          )}
        >
          {preset.label}
        </button>
      ))}
    </div>
  )
}

// Quick selects panel for range calendar
interface QuickSelectsRangePanelProps {
  onSelect: (range: { start: CalendarDate; end: CalendarDate }) => void
}

function QuickSelectsRangePanel({ onSelect }: QuickSelectsRangePanelProps) {
  return (
    <div className="flex flex-col gap-1 border-r pr-3 mr-3">
      <div className="text-xs font-medium text-muted-foreground mb-1">Quick Select</div>
      {dateRangePresets.map((preset) => (
        <button
          key={preset.label}
          type="button"
          onClick={() => onSelect(preset.getRange())}
          className={cn(
            buttonVariants({ variant: "ghost", size: "sm" }),
            "justify-start h-7 text-xs font-normal"
          )}
        >
          {preset.label}
        </button>
      ))}
    </div>
  )
}

// Month Picker Grid - 4x3 grid of months
function MonthPickerGrid() {
  const zoomContext = useContext(CalendarZoomContext)
  const state = useCalendarState()
  const { locale } = useLocale()

  const months = useMemo(() => {
    const formatter = new Intl.DateTimeFormat(locale, { month: "short" })
    return Array.from({ length: 12 }, (_, i) => ({
      index: i + 1,
      name: formatter.format(new Date(2024, i, 1)),
    }))
  }, [locale])

  if (!zoomContext || !state) return null

  const handleMonthSelect = (monthIndex: number) => {
    const safeDay = Math.min(
      state.focusedDate.day,
      getDaysInMonth(zoomContext.viewYear, monthIndex)
    )
    const newDate = new CalendarDate(zoomContext.viewYear, monthIndex, safeDay)
    state.setFocusedDate(newDate)
    zoomContext.setView("days")
  }

  const currentMonth = state.focusedDate.month
  const currentYear = state.focusedDate.year

  return (
    <div className="grid grid-cols-3 gap-1 p-1" role="grid" aria-label="Month picker">
      {months.map(({ index, name }) => {
        const isSelected =
          index === currentMonth && zoomContext.viewYear === currentYear
        return (
          <button
            key={index}
            type="button"
            onClick={() => handleMonthSelect(index)}
            className={cn(
              buttonVariants({ variant: "ghost" }),
              "h-9 text-sm font-normal",
              isSelected && "bg-primary text-primary-foreground"
            )}
            aria-selected={isSelected}
          >
            {name}
          </button>
        )
      })}
    </div>
  )
}

// Year Picker Grid - 4x3 grid of years (12-year span)
function YearPickerGrid() {
  const zoomContext = useContext(CalendarZoomContext)
  const state = useCalendarState()

  if (!zoomContext || !state) return null

  const years = useMemo(() => {
    return Array.from({ length: 12 }, (_, i) => zoomContext.viewDecadeStart + i)
  }, [zoomContext.viewDecadeStart])

  const handleYearSelect = (year: number) => {
    zoomContext.setViewYear(year)
    zoomContext.setView("months")
  }

  const currentYear = state.focusedDate.year

  return (
    <div className="grid grid-cols-3 gap-1 p-1" role="grid" aria-label="Year picker">
      {years.map((year) => {
        const isSelected = year === currentYear
        return (
          <button
            key={year}
            type="button"
            onClick={() => handleYearSelect(year)}
            className={cn(
              buttonVariants({ variant: "ghost" }),
              "h-9 text-sm font-normal",
              isSelected && "bg-primary text-primary-foreground"
            )}
            aria-selected={isSelected}
          >
            {year}
          </button>
        )
      })}
    </div>
  )
}

// Content switcher - renders day grid, month picker, or year picker
interface CalendarContentProps {
  children: React.ReactNode
}

function CalendarContent({ children }: CalendarContentProps) {
  const zoomContext = useContext(CalendarZoomContext)

  if (!zoomContext) {
    return <>{children}</>
  }

  switch (zoomContext.view) {
    case "months":
      return <MonthPickerGrid />
    case "years":
      return <YearPickerGrid />
    default:
      return <>{children}</>
  }
}

// Zoomable heading with clickable month/year
const CalendarHeadingZoomable = (props: React.HTMLAttributes<HTMLElement>) => {
  const { direction } = useLocale()
  const zoomContext = useContext(CalendarZoomContext)
  const state = useCalendarState()

  const getHeadingText = () => {
    if (!state) return ""

    if (zoomContext?.view === "months") {
      return zoomContext.viewYear.toString()
    }
    if (zoomContext?.view === "years") {
      const start = zoomContext.viewDecadeStart
      return `${start} - ${start + 11}`
    }

    // Default: format month/year
    const formatter = new Intl.DateTimeFormat(undefined, {
      month: "long",
      year: "numeric",
    })
    return formatter.format(
      new Date(state.focusedDate.year, state.focusedDate.month - 1, 1)
    )
  }

  const handleHeadingClick = () => {
    if (!zoomContext) return

    if (zoomContext.view === "days") {
      zoomContext.setView("months")
    } else if (zoomContext.view === "months") {
      zoomContext.setView("years")
    }
  }

  const handlePrevious = () => {
    if (!zoomContext) return

    if (zoomContext.view === "months") {
      zoomContext.setViewYear(zoomContext.viewYear - 1)
    } else if (zoomContext.view === "years") {
      zoomContext.setViewDecadeStart(zoomContext.viewDecadeStart - 12)
    }
  }

  const handleNext = () => {
    if (!zoomContext) return

    if (zoomContext.view === "months") {
      zoomContext.setViewYear(zoomContext.viewYear + 1)
    } else if (zoomContext.view === "years") {
      zoomContext.setViewDecadeStart(zoomContext.viewDecadeStart + 12)
    }
  }

  const isZoomed = zoomContext && zoomContext.view !== "days"

  return (
    <header className="flex w-full items-center gap-1 px-1 pb-4" {...props}>
      {isZoomed ? (
        <button
          type="button"
          onClick={handlePrevious}
          className={cn(
            buttonVariants({ variant: "outline" }),
            "size-7 bg-transparent p-0 opacity-50 hover:opacity-100"
          )}
        >
          {direction === "rtl" ? (
            <HugeiconsIcon icon={ArrowRight01Icon} aria-hidden className="size-4" />
          ) : (
            <HugeiconsIcon icon={ArrowLeft01Icon} aria-hidden className="size-4" />
          )}
        </button>
      ) : (
        <AriaButton
          slot="previous"
          className={cn(
            buttonVariants({ variant: "outline" }),
            "size-7 bg-transparent p-0 opacity-50",
            "data-[hovered]:opacity-100"
          )}
        >
          {direction === "rtl" ? (
            <HugeiconsIcon icon={ArrowRight01Icon} aria-hidden className="size-4" />
          ) : (
            <HugeiconsIcon icon={ArrowLeft01Icon} aria-hidden className="size-4" />
          )}
        </AriaButton>
      )}

      <button
        type="button"
        onClick={handleHeadingClick}
        className={cn(
          "grow text-center text-sm font-medium",
          "rounded-md py-1 transition-colors",
          zoomContext?.view !== "years" && "cursor-pointer hover:bg-muted"
        )}
        disabled={zoomContext?.view === "years"}
        aria-label={
          zoomContext?.view === "days"
            ? "Select month"
            : zoomContext?.view === "months"
              ? "Select year"
              : undefined
        }
      >
        {getHeadingText()}
      </button>

      {isZoomed ? (
        <button
          type="button"
          onClick={handleNext}
          className={cn(
            buttonVariants({ variant: "outline" }),
            "size-7 bg-transparent p-0 opacity-50 hover:opacity-100"
          )}
        >
          {direction === "rtl" ? (
            <HugeiconsIcon icon={ArrowLeft01Icon} aria-hidden className="size-4" />
          ) : (
            <HugeiconsIcon icon={ArrowRight01Icon} aria-hidden className="size-4" />
          )}
        </button>
      ) : (
        <AriaButton
          slot="next"
          className={cn(
            buttonVariants({ variant: "outline" }),
            "size-7 bg-transparent p-0 opacity-50",
            "data-[hovered]:opacity-100"
          )}
        >
          {direction === "rtl" ? (
            <HugeiconsIcon icon={ArrowLeft01Icon} aria-hidden className="size-4" />
          ) : (
            <HugeiconsIcon icon={ArrowRight01Icon} aria-hidden className="size-4" />
          )}
        </AriaButton>
      )}
    </header>
  )
}

// ============================================================================
// Original CalendarHeading (kept for backward compatibility)
// ============================================================================

const CalendarHeading = (props: React.HTMLAttributes<HTMLElement>) => {
  const { direction } = useLocale()

  return (
    <header className="flex w-full items-center gap-1 px-1 pb-4" {...props}>
      <AriaButton
        slot="previous"
        className={cn(
          buttonVariants({ variant: "outline" }),
          "size-7 bg-transparent p-0 opacity-50",
          /* Hover */
          "data-[hovered]:opacity-100"
        )}
      >
        {direction === "rtl" ? (
          <HugeiconsIcon icon={ArrowRight01Icon} aria-hidden className="size-4" />
        ) : (
          <HugeiconsIcon icon={ArrowLeft01Icon} aria-hidden className="size-4" />
        )}
      </AriaButton>
      <AriaHeading className="grow text-center text-sm font-medium" />
      <AriaButton
        slot="next"
        className={cn(
          buttonVariants({ variant: "outline" }),
          "size-7 bg-transparent p-0 opacity-50",
          /* Hover */
          "data-[hovered]:opacity-100"
        )}
      >
        {direction === "rtl" ? (
          <HugeiconsIcon icon={ArrowLeft01Icon} aria-hidden className="size-4" />
        ) : (
          <HugeiconsIcon icon={ArrowRight01Icon} aria-hidden className="size-4" />
        )}
      </AriaButton>
    </header>
  )
}

const CalendarGrid = ({ className, ...props }: AriaCalendarGridProps) => (
  <AriaCalendarGrid
    className={cn(
      "border-separate border-spacing-x-0 border-spacing-y-1",
      className
    )}
    {...props}
  />
)

const CalendarGridHeader = ({ ...props }: AriaCalendarGridHeaderProps) => (
  <AriaCalendarGridHeader {...props} />
)

const CalendarHeaderCell = ({
  className,
  ...props
}: AriaCalendarHeaderCellProps) => (
  <AriaCalendarHeaderCell
    className={cn(
      "w-9 rounded-md text-[0.8rem] font-normal text-muted-foreground",
      className
    )}
    {...props}
  />
)

const CalendarGridBody = ({
  className,
  ...props
}: AriaCalendarGridBodyProps) => (
  <AriaCalendarGridBody className={cn("[&>tr>td]:p-0", className)} {...props} />
)

const CalendarCell = ({ className, ...props }: AriaCalendarCellProps) => {
  const isRange = Boolean(useContext(AriaRangeCalendarStateContext))
  return (
    <AriaCalendarCell
      className={composeRenderProps(className, (className, renderProps) =>
        cn(
          buttonVariants({ variant: "ghost" }),
          "relative flex size-9 items-center justify-center p-0 text-sm font-normal",
          /* Disabled */
          renderProps.isDisabled && "text-muted-foreground opacity-50",
          /* Selected */
          renderProps.isSelected &&
            "bg-primary text-primary-foreground data-[focused]:bg-primary data-[focused]:text-primary-foreground",
          /* Hover */
          renderProps.isHovered &&
            renderProps.isSelected &&
            (renderProps.isSelectionStart ||
              renderProps.isSelectionEnd ||
              !isRange) &&
            "data-[hovered]:bg-primary data-[hovered]:text-primary-foreground",
          /* Selection Start/End */
          renderProps.isSelected &&
            isRange &&
            !renderProps.isSelectionStart &&
            !renderProps.isSelectionEnd &&
            "rounded-none bg-accent text-accent-foreground",
          /* Outside Month */
          renderProps.isOutsideMonth &&
            "text-muted-foreground opacity-50 data-[selected]:bg-accent/50 data-[selected]:text-muted-foreground data-[selected]:opacity-30",
          /* Current Date */
          renderProps.date.compare(today(getLocalTimeZone())) === 0 &&
            !renderProps.isSelected &&
            "bg-accent text-accent-foreground",
          /* Unavailable Date */
          renderProps.isUnavailable && "cursor-default text-destructive",
          renderProps.isInvalid &&
            "bg-destructive text-destructive-foreground data-[focused]:bg-destructive data-[hovered]:bg-destructive data-[focused]:text-destructive-foreground data-[hovered]:text-destructive-foreground",
          className
        )
      )}
      {...props}
    />
  )
}

interface JollyCalendarProps<T extends AriaDateValue>
  extends AriaCalendarProps<T> {
  errorMessage?: string
  showQuickSelects?: boolean
}

function JollyCalendar<T extends AriaDateValue>({
  errorMessage,
  className,
  showQuickSelects,
  onChange,
  ...props
}: JollyCalendarProps<T>) {
  const handleQuickSelect = (date: CalendarDate) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onChange?.(date as any)
  }

  return (
    <div className={cn("flex", showQuickSelects && "gap-0")}>
      {showQuickSelects && <QuickSelectsPanel onSelect={handleQuickSelect} />}
      <Calendar
        className={composeRenderProps(className, (className) =>
          cn("w-fit", className)
        )}
        onChange={onChange}
        {...props}
      >
        <CalendarZoomProvider>
          <CalendarHeadingZoomable />
          <CalendarContent>
            <CalendarGrid>
              <CalendarGridHeader>
                {(day) => <CalendarHeaderCell>{day}</CalendarHeaderCell>}
              </CalendarGridHeader>
              <CalendarGridBody>
                {(date) => <CalendarCell date={date} />}
              </CalendarGridBody>
            </CalendarGrid>
          </CalendarContent>
        </CalendarZoomProvider>
        {errorMessage && (
          <Text className="text-sm text-destructive" slot="errorMessage">
            {errorMessage}
          </Text>
        )}
      </Calendar>
    </div>
  )
}

interface JollyRangeCalendarProps<T extends AriaDateValue>
  extends AriaRangeCalendarProps<T> {
  errorMessage?: string
  showQuickSelects?: boolean
}

function JollyRangeCalendar<T extends AriaDateValue>({
  errorMessage,
  className,
  showQuickSelects,
  onChange,
  ...props
}: JollyRangeCalendarProps<T>) {
  const handleQuickSelect = (range: { start: CalendarDate; end: CalendarDate }) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onChange?.({ start: range.start, end: range.end } as any)
  }

  return (
    <div className={cn("flex", showQuickSelects && "gap-0")}>
      {showQuickSelects && <QuickSelectsRangePanel onSelect={handleQuickSelect} />}
      <RangeCalendar
        className={composeRenderProps(className, (className) =>
          cn("w-fit", className)
        )}
        onChange={onChange}
        {...props}
      >
        <CalendarZoomProvider>
          <CalendarHeadingZoomable />
          <CalendarContent>
            <CalendarGrid>
              <CalendarGridHeader>
                {(day) => <CalendarHeaderCell>{day}</CalendarHeaderCell>}
              </CalendarGridHeader>
              <CalendarGridBody>
                {(date) => <CalendarCell date={date} />}
              </CalendarGridBody>
            </CalendarGrid>
          </CalendarContent>
        </CalendarZoomProvider>
        {errorMessage && (
          <Text slot="errorMessage" className="text-sm text-destructive">
            {errorMessage}
          </Text>
        )}
      </RangeCalendar>
    </div>
  )
}

export {
  Calendar,
  CalendarCell,
  CalendarGrid,
  CalendarGridBody,
  CalendarGridHeader,
  CalendarHeaderCell,
  CalendarHeading,
  RangeCalendar,
  JollyCalendar,
  JollyRangeCalendar,
}
export type { JollyCalendarProps, JollyRangeCalendarProps }
