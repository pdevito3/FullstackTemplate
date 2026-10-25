"use client"

import { createContext, useContext, useState, useMemo, useEffect, useCallback } from "react"
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
  ListBox as AriaListBox,
  ListBoxItem as AriaListBoxItem,
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
  type RangeCalendarState,
  type Selection as AriaSelection,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { buttonVariants } from "@/components/ui/button-variants"
import { DateField, DateInput } from "@/components/ui/datefield"

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
  const focusedYear = state?.focusedDate?.year
  useEffect(() => {
    if (focusedYear !== undefined) {
      setViewYear(focusedYear)
      setViewDecadeStart(Math.floor(focusedYear / 12) * 12)
    }
  }, [focusedYear])

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

type CalendarLayout = "wide" | "narrow"

const layoutClassNames = {
  wide: {
    body: "flex",
    quickSelects: "mr-3 border-r pr-3",
    presets: "flex flex-col gap-1",
    preset: buttonVariants({ variant: "ghost", size: "sm", className: "h-7 justify-start text-xs font-normal" }),
    header: "px-1",
    navButton: "size-7",
    headingButton: "",
  },
  narrow: {
    body: "flex flex-col",
    quickSelects: "mb-3 border-b pb-3",
    presets: "grid grid-cols-3 gap-1",
    preset: buttonVariants({
      variant: "outline",
      size: "sm",
      className: "h-auto min-h-8 px-1 text-xs leading-tight font-normal whitespace-normal",
    }),
    header: "",
    navButton: "size-8",
    headingButton: "h-8",
  },
} satisfies Record<CalendarLayout, Record<string, string>>

interface QuickSelectsPanelProps<P extends { label: string }> {
  presets: readonly P[]
  layout: CalendarLayout
  onSelect: (preset: P) => void
}

function QuickSelectsPanel<P extends { label: string }>({ presets, layout, onSelect }: QuickSelectsPanelProps<P>) {
  const classNames = layoutClassNames[layout]
  return (
    <div className={cn("flex flex-col gap-1", classNames.quickSelects)}>
      <div className="mb-1 text-xs font-medium text-muted-foreground">Quick Select</div>
      <div className={classNames.presets}>
        {presets.map((preset) => (
          <AriaButton key={preset.label} onPress={() => onSelect(preset)} className={classNames.preset}>
            {preset.label}
          </AriaButton>
        ))}
      </div>
    </div>
  )
}

// The month and year pickers are listboxes in a grid layout: the arrow keys move in two dimensions, and Enter or a
// click picks an item. Each picker is at least w-63, the width of the day grid (7 cells of w-9), so that the popover
// keeps its size. In the narrow layout, the picker fills the column, as the day grid does.
const pickerClassName = "grid w-full min-w-63 grid-cols-3 gap-1 p-1 outline-none"

function pickerItemClassName({ isSelected }: { isSelected: boolean }) {
  return cn(
    buttonVariants({ variant: "ghost" }),
    "h-9 cursor-default text-sm font-normal",
    isSelected && "bg-primary text-primary-foreground"
  )
}

// A press on the selected item toggles the single selection off. That press still picks the item.
function pickedKey(keys: AriaSelection, selectedKey: number | null): number | null {
  if (keys === "all") return selectedKey
  const [key] = keys
  return key === undefined ? selectedKey : Number(key)
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
    // The month button unmounts. Move focus to the day cell, so that the keyboard and Escape still work.
    state.setFocused(true)
    zoomContext.setView("days")
  }

  // The focused month is selected only in its own year.
  const selectedMonth =
    zoomContext.viewYear === state.focusedDate.year ? state.focusedDate.month : null

  const handleSelectionChange = (keys: AriaSelection) => {
    const month = pickedKey(keys, selectedMonth)
    if (month !== null) handleMonthSelect(month)
  }

  return (
    <AriaListBox
      aria-label="Month picker"
      layout="grid"
      selectionMode="single"
      selectedKeys={selectedMonth === null ? [] : [selectedMonth]}
      onSelectionChange={handleSelectionChange}
      // The button that opened this view unmounts or is disabled. Keep focus in the calendar:
      // on the selected month, or on January when no month is selected.
      autoFocus="first"
      // Escape closes the popover, as it does in the day grid.
      escapeKeyBehavior="none"
      shouldSelectOnPressUp
      className={pickerClassName}
    >
      {months.map(({ index, name }) => (
        <AriaListBoxItem key={index} id={index} textValue={name} className={pickerItemClassName}>
          {name}
        </AriaListBoxItem>
      ))}
    </AriaListBox>
  )
}

// Year Picker Grid - 4x3 grid of years (12-year span)
function YearPickerGrid() {
  const zoomContext = useContext(CalendarZoomContext)
  const state = useCalendarState()

  // useMemo must be called before any early returns (Rules of Hooks)
  const viewDecadeStart = zoomContext?.viewDecadeStart ?? 0
  const years = useMemo(() => {
    return Array.from({ length: 12 }, (_, i) => viewDecadeStart + i)
  }, [viewDecadeStart])

  if (!zoomContext || !state) return null

  const handleYearSelect = (year: number) => {
    zoomContext.setViewYear(year)
    zoomContext.setView("months")
  }

  const selectedYear = years.includes(state.focusedDate.year) ? state.focusedDate.year : null

  const handleSelectionChange = (keys: AriaSelection) => {
    const year = pickedKey(keys, selectedYear)
    if (year !== null) handleYearSelect(year)
  }

  return (
    <AriaListBox
      aria-label="Year picker"
      layout="grid"
      selectionMode="single"
      selectedKeys={selectedYear === null ? [] : [selectedYear]}
      onSelectionChange={handleSelectionChange}
      // The button that opened this view unmounts or is disabled. Keep focus in the calendar:
      // on the selected year, or on the first year of the span when no year is selected.
      autoFocus="first"
      // Escape closes the popover, as it does in the day grid.
      escapeKeyBehavior="none"
      shouldSelectOnPressUp
      className={pickerClassName}
    >
      {years.map((year) => (
        <AriaListBoxItem key={year} id={year} textValue={String(year)} className={pickerItemClassName}>
          {year}
        </AriaListBoxItem>
      ))}
    </AriaListBox>
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

interface CalendarHeadingZoomableProps extends React.HTMLAttributes<HTMLElement> {
  layout: CalendarLayout
}

// Zoomable heading with clickable month/year
const CalendarHeadingZoomable = ({ layout, ...props }: CalendarHeadingZoomableProps) => {
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
  const classNames = layoutClassNames[layout]

  return (
    <header className={cn("flex w-full items-center gap-1 pb-4", classNames.header)} {...props}>
      {isZoomed ? (
        <AriaButton
          slot={null}
          aria-label="Previous"
          onPress={handlePrevious}
          className={cn(
            buttonVariants({ variant: "outline" }),
            classNames.navButton,
            "bg-transparent p-0 opacity-50 hover:opacity-100"
          )}
        >
          {direction === "rtl" ? (
            <HugeiconsIcon icon={ArrowRight01Icon} aria-hidden className="size-4" />
          ) : (
            <HugeiconsIcon icon={ArrowLeft01Icon} aria-hidden className="size-4" />
          )}
        </AriaButton>
      ) : (
        <AriaButton
          slot="previous"
          className={cn(
            buttonVariants({ variant: "outline" }),
            classNames.navButton,
            "bg-transparent p-0 opacity-50",
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

      <AriaButton
        slot={null}
        onPress={handleHeadingClick}
        className={cn(
          "grow text-center text-sm font-medium",
          "rounded-md py-1 transition-colors",
          classNames.headingButton,
          zoomContext?.view !== "years" && "cursor-pointer hover:bg-muted"
        )}
        isDisabled={zoomContext?.view === "years"}
        aria-label={
          zoomContext?.view === "days"
            ? "Select month"
            : zoomContext?.view === "months"
              ? "Select year"
              : undefined
        }
      >
        {getHeadingText()}
      </AriaButton>

      {isZoomed ? (
        <AriaButton
          slot={null}
          aria-label="Next"
          onPress={handleNext}
          className={cn(
            buttonVariants({ variant: "outline" }),
            classNames.navButton,
            "bg-transparent p-0 opacity-50 hover:opacity-100"
          )}
        >
          {direction === "rtl" ? (
            <HugeiconsIcon icon={ArrowLeft01Icon} aria-hidden className="size-4" />
          ) : (
            <HugeiconsIcon icon={ArrowRight01Icon} aria-hidden className="size-4" />
          )}
        </AriaButton>
      ) : (
        <AriaButton
          slot="next"
          className={cn(
            buttonVariants({ variant: "outline" }),
            classNames.navButton,
            "bg-transparent p-0 opacity-50",
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

// React Aria moves the focus one day forward after Enter or Space on the first day of a range
// (focusNearestAvailableDate, called from useCalendarCell). The grid keeps the focus on the day
// that the user picked. React Aria finds its data by the identity of the state, so the grid
// changes the state in place and does not copy it.
function keepFocusOnRangeStart(state: RangeCalendarState | null) {
  if (!state) return
  state.focusNearestAvailableDate = (date) => {
    state.setFocusedDate(date)
    state.setFocused(true)
  }
}

const CalendarGrid = ({ className, ...props }: AriaCalendarGridProps) => {
  const rangeState = useContext(AriaRangeCalendarStateContext)
  keepFocusOnRangeStart(rangeState)
  return (
    <>
      <AriaCalendarGrid
        className={cn(
          "w-full border-separate border-spacing-x-0 border-spacing-y-1",
          className
        )}
        {...props}
      />
      {/* The focus stays on the start day, so a screen reader does not read a new cell */}
      {rangeState && (
        <span className="sr-only" aria-live="polite">
          {rangeState.anchorDate ? "Choose an end date." : ""}
        </span>
      )}
    </>
  )
}

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
          "relative flex h-9 w-full min-w-9 items-center justify-center p-0 text-sm font-normal",
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
  showDateInput?: boolean
  layout?: CalendarLayout
}

// With showDateInput, autoFocus focuses the date input, because it is the first field.
// Without it, autoFocus focuses the calendar grid.
function JollyCalendar<T extends AriaDateValue>({
  errorMessage,
  className,
  showQuickSelects,
  showDateInput,
  layout = "wide",
  onChange,
  value,
  autoFocus,
  ...props
}: JollyCalendarProps<T>) {
  // Track focused value to navigate calendar view when quick select is used
  // Initialize with today if no value to avoid uncontrolled-to-controlled warning
  const [focusedValue, setFocusedValue] = useState<CalendarDate>(() => {
    if (value) {
      const calDate = value as CalendarDate
      return new CalendarDate(calDate.year, calDate.month, calDate.day)
    }
    return today(getLocalTimeZone())
  })

  // Sync focusedValue when value changes externally
  useEffect(() => {
    if (value) {
      const calDate = value as CalendarDate
      setFocusedValue(new CalendarDate(calDate.year, calDate.month, calDate.day))
    }
  }, [value])

  const handleQuickSelect = useCallback((date: CalendarDate) => {
    // Update focused value to navigate calendar view to selected date
    setFocusedValue(date)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onChange?.(date as any)
  }, [onChange])

  const handleDateInputChange = useCallback((newValue: AriaDateValue | null) => {
    if (newValue) {
      const calDate = newValue as CalendarDate
      setFocusedValue(calDate)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      onChange?.(calDate as any)
    }
  }, [onChange])

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleCalendarChange = useCallback((newValue: any) => {
    const calDate = newValue as CalendarDate
    setFocusedValue(calDate)
    onChange?.(newValue)
  }, [onChange])

  return (
    <div className="flex flex-col gap-2">
      {showDateInput && (
        <DateField
          value={value ? (value as AriaDateValue) : null}
          onChange={handleDateInputChange}
          aria-label="Date"
          autoFocus={autoFocus}
        >
          <DateInput className="h-9" />
        </DateField>
      )}
      <div className={layoutClassNames[layout].body}>
        {showQuickSelects && (
          <QuickSelectsPanel
            presets={singleDatePresets}
            layout={layout}
            onSelect={(preset) => handleQuickSelect(preset.getDate())}
          />
        )}
        <Calendar
          className={composeRenderProps(className, (className) =>
            cn("grow", className)
          )}
          value={value}
          onChange={handleCalendarChange}
          focusedValue={focusedValue}
          onFocusChange={setFocusedValue}
          autoFocus={autoFocus && !showDateInput}
          {...props}
        >
          <CalendarZoomProvider>
            <CalendarHeadingZoomable layout={layout} />
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
    </div>
  )
}

interface JollyRangeCalendarProps<T extends AriaDateValue>
  extends AriaRangeCalendarProps<T> {
  errorMessage?: string
  showQuickSelects?: boolean
  showDateInput?: boolean
  layout?: CalendarLayout
}

// With showDateInput, autoFocus focuses the start date input, because it is the first field.
// Without it, autoFocus focuses the calendar grid.
function JollyRangeCalendar<T extends AriaDateValue>({
  errorMessage,
  className,
  showQuickSelects,
  showDateInput,
  layout = "wide",
  onChange,
  value,
  autoFocus,
  ...props
}: JollyRangeCalendarProps<T>) {
  // Track focused value to navigate calendar view when quick select is used
  // Initialize with today if no value to avoid uncontrolled-to-controlled warning
  const [focusedValue, setFocusedValue] = useState<CalendarDate>(() => {
    if (value?.start) {
      const start = value.start as CalendarDate
      return new CalendarDate(start.year, start.month, start.day)
    }
    return today(getLocalTimeZone())
  })

  // Sync focusedValue when value changes externally
  useEffect(() => {
    if (value?.start) {
      const start = value.start as CalendarDate
      setFocusedValue(new CalendarDate(start.year, start.month, start.day))
    }
  }, [value?.start])

  const handleQuickSelect = useCallback((range: { start: CalendarDate; end: CalendarDate }) => {
    // Update focused value to navigate calendar view to selected range start
    setFocusedValue(range.start)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onChange?.({ start: range.start, end: range.end } as any)
  }, [onChange])

  const handleStartDateChange = useCallback((newValue: AriaDateValue | null) => {
    if (newValue && value) {
      const calDate = newValue as CalendarDate
      setFocusedValue(calDate)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      onChange?.({ start: calDate, end: value.end } as any)
    }
  }, [onChange, value])

  const handleEndDateChange = useCallback((newValue: AriaDateValue | null) => {
    if (newValue && value) {
      const calDate = newValue as CalendarDate
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      onChange?.({ start: value.start, end: calDate } as any)
    }
  }, [onChange, value])

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleCalendarChange = useCallback((newValue: any) => {
    if (newValue?.start) {
      setFocusedValue(newValue.start)
    }
    onChange?.(newValue)
  }, [onChange])

  return (
    <div className="flex flex-col gap-2">
      {showDateInput && (
        <div className="flex gap-2 items-center">
          <DateField
            value={value?.start ? (value.start as AriaDateValue) : null}
            onChange={handleStartDateChange}
            aria-label="Start date"
            autoFocus={autoFocus}
            className="min-w-0 flex-1"
          >
            <DateInput className="h-9" />
          </DateField>
          <span className="text-muted-foreground text-sm">to</span>
          <DateField
            value={value?.end ? (value.end as AriaDateValue) : null}
            onChange={handleEndDateChange}
            aria-label="End date"
            className="min-w-0 flex-1"
          >
            <DateInput className="h-9" />
          </DateField>
        </div>
      )}
      <div className={layoutClassNames[layout].body}>
        {showQuickSelects && (
          <QuickSelectsPanel
            presets={dateRangePresets}
            layout={layout}
            onSelect={(preset) => handleQuickSelect(preset.getRange())}
          />
        )}
        <RangeCalendar
          className={composeRenderProps(className, (className) =>
            cn("grow", className)
          )}
          value={value}
          onChange={handleCalendarChange}
          focusedValue={focusedValue}
          onFocusChange={setFocusedValue}
          autoFocus={autoFocus && !showDateInput}
          {...props}
        >
          <CalendarZoomProvider>
            <CalendarHeadingZoomable layout={layout} />
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
