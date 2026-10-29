"use client"

import { useContext, useState, useEffect, useCallback, useId } from "react"
import { CalendarDate } from "@internationalized/date"
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
  type RangeCalendarState,
  type RangeValue,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { browserTimeZone, todayAt, type TimeZone } from "@/lib/zoned-time"
import { buttonVariants } from "@/components/ui/button-variants"
import { layoutClassNames, type CalendarLayout } from "@/components/ui/calendar-layout"
import { dateRangePresets, singleDatePresets } from "@/components/ui/calendar-presets"
import { CalendarContent, CalendarHeadingZoomable, CalendarZoomProvider } from "@/components/ui/calendar-zoom"
import { DateField, DateInput } from "@/components/ui/datefield"
import { isTypedInFull, shortYearMessage, useShortYearCheck } from "@/components/ui/use-short-year-check"

const Calendar = AriaCalendar

const RangeCalendar = AriaRangeCalendar

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

// React Aria finds today in state.timeZone, the zone of the browser, for the "Today" name and the data-today attribute
// of a cell. It also formats whole days in that zone, which gives the same day in each zone. The grid sets the zone of
// the place in the state, in place, as keepFocusOnRangeStart does.
function setZoneOfToday(state: { readonly timeZone: string } | null, timeZone: TimeZone | undefined) {
  if (state && timeZone) Object.assign(state, { timeZone })
}

interface CalendarGridProps extends AriaCalendarGridProps {
  /** The zone of the place that the dates belong to. The cells mark today at that place. The default is the browser zone. */
  timeZone?: TimeZone
}

const CalendarGrid = ({ className, timeZone, ...props }: CalendarGridProps) => {
  const calendarState = useContext(AriaCalendarStateContext)
  const rangeState = useContext(AriaRangeCalendarStateContext)
  setZoneOfToday(calendarState ?? rangeState, timeZone)
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
          renderProps.isSelected &&
            isRange &&
            !renderProps.isSelectionStart &&
            !renderProps.isSelectionEnd &&
            "rounded-none bg-accent text-accent-foreground",
          /* Range: paint to the cell edges, so that the days join */
          renderProps.isSelected && isRange && "bg-clip-border",
          /* Outside Month */
          renderProps.isOutsideMonth &&
            "text-muted-foreground opacity-50 data-[selected]:bg-accent/50 data-[selected]:text-muted-foreground data-[selected]:opacity-30",
          /* Current Date */
          renderProps.isToday &&
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

type TypedRange = Partial<RangeValue<AriaDateValue | null>>

// The range that the date fields make: null while an end is empty, still being typed, or before the start.
function rangeOf({ start, end }: TypedRange): RangeValue<AriaDateValue> | null {
  if (!isTypedInFull(start) || !isTypedInFull(end) || end.compare(start) < 0) return null
  return { start, end }
}

// An end before the start needs two full years, so it never shows with a short year.
function rangeFieldError(isEndBeforeStart: boolean, hasShortYear: boolean): string | null {
  if (isEndBeforeStart) return "The end date is before the start date."
  if (hasShortYear) return shortYearMessage
  return null
}

interface JollyCalendarProps<T extends AriaDateValue>
  extends Omit<AriaCalendarProps<T>, "onChange"> {
  /** Gets null when the typed date is cleared or is not complete. */
  onChange?: (value: T | null) => void
  errorMessage?: string
  showQuickSelects?: boolean
  showDateInput?: boolean
  layout?: CalendarLayout
  /** The zone of the place that the date belongs to. It decides which day is today. The default is the browser zone. */
  timeZone?: TimeZone
  /**
   * Keeps the same height for every month and for the month and year pickers, so that a popover does not move when
   * the month or the view changes.
   */
  fixedHeight?: boolean
}

// With showDateInput, autoFocus focuses the date input, because it is the first field.
// Without it, autoFocus focuses the calendar grid.
function JollyCalendar<T extends AriaDateValue>({
  errorMessage,
  className,
  showQuickSelects,
  showDateInput,
  layout = "wide",
  fixedHeight = false,
  onChange,
  value,
  autoFocus,
  timeZone = browserTimeZone(),
  ...props
}: JollyCalendarProps<T>) {
  // Track focused value to navigate calendar view when quick select is used
  // Initialize with today if no value to avoid uncontrolled-to-controlled warning
  const [focusedValue, setFocusedValue] = useState<CalendarDate>(() => {
    if (value) {
      const calDate = value as CalendarDate
      return new CalendarDate(calDate.year, calDate.month, calDate.day)
    }
    return todayAt(timeZone, new Date())
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

  // A date that the user is still typing, or a cleared field. It shows in the field while the value is null.
  const [typedDate, setTypedDate] = useState<AriaDateValue | null>(null)

  const handleDateInputChange = useCallback((newValue: AriaDateValue | null) => {
    if (isTypedInFull(newValue)) {
      setTypedDate(null)
      setFocusedValue(newValue as CalendarDate)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      onChange?.(newValue as any)
    } else {
      setTypedDate(newValue)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      if (value) onChange?.(null as any)
    }
  }, [onChange, value])

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleCalendarChange = useCallback((newValue: any) => {
    const calDate = newValue as CalendarDate
    setFocusedValue(calDate)
    onChange?.(newValue)
  }, [onChange])

  const fieldDate = value ?? typedDate
  const yearCheck = useShortYearCheck(fieldDate)
  const errorId = useId()

  return (
    <div className="flex flex-col gap-2">
      {showDateInput && (
        <DateField
          value={fieldDate}
          onChange={handleDateInputChange}
          onBlur={yearCheck.onBlur}
          isInvalid={yearCheck.isShortYear}
          aria-label="Date"
          aria-describedby={yearCheck.isShortYear ? errorId : undefined}
          autoFocus={autoFocus}
        >
          <DateInput className="h-9" />
        </DateField>
      )}
      {showDateInput && yearCheck.isShortYear && (
        <p id={errorId} role="alert" className="text-destructive text-sm">
          {shortYearMessage}
        </p>
      )}
      <div className={layoutClassNames[layout].body}>
        {showQuickSelects && (
          <QuickSelectsPanel
            presets={singleDatePresets}
            layout={layout}
            onSelect={(preset) => handleQuickSelect(preset.getDate(timeZone))}
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
          weeksInMonth={fixedHeight ? 6 : undefined}
          {...props}
        >
          <CalendarZoomProvider fixedHeight={fixedHeight}>
            <CalendarHeadingZoomable layout={layout} />
            <CalendarContent>
              <CalendarGrid timeZone={timeZone}>
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
  extends Omit<AriaRangeCalendarProps<T>, "onChange"> {
  /** Gets null when a typed date is cleared, is not complete, or ends the range before its start. */
  onChange?: (value: RangeValue<T> | null) => void
  errorMessage?: string
  showQuickSelects?: boolean
  showDateInput?: boolean
  layout?: CalendarLayout
  /** The zone of the place that the date belongs to. It decides which day is today. The default is the browser zone. */
  timeZone?: TimeZone
  /**
   * Keeps the same height for every month and for the month and year pickers, so that a popover does not move when
   * the month or the view changes.
   */
  fixedHeight?: boolean
}

// With showDateInput, autoFocus focuses the start date input, because it is the first field.
// Without it, autoFocus focuses the calendar grid.
function JollyRangeCalendar<T extends AriaDateValue>({
  errorMessage,
  className,
  showQuickSelects,
  showDateInput,
  layout = "wide",
  fixedHeight = false,
  onChange,
  value,
  autoFocus,
  timeZone = browserTimeZone(),
  ...props
}: JollyRangeCalendarProps<T>) {
  // The view moves to the range start on mount, on a quick select, and on a typed start date. A pick in the grid
  // keeps the focus on the picked day, which can be in another month. A value that the parent sets does not move it.
  // Initialize with today if no value to avoid uncontrolled-to-controlled warning
  const [focusedValue, setFocusedValue] = useState<CalendarDate>(() => {
    if (value?.start) {
      const start = value.start as CalendarDate
      return new CalendarDate(start.year, start.month, start.day)
    }
    return todayAt(timeZone, new Date())
  })

  const handleQuickSelect = useCallback((range: { start: CalendarDate; end: CalendarDate }) => {
    // Update focused value to navigate calendar view to selected range start
    setFocusedValue(range.start)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onChange?.({ start: range.start, end: range.end } as any)
  }, [onChange])

  // The typed dates while they do not make a range. They show in the fields while the value is null.
  const [draft, setDraft] = useState<TypedRange>({})

  const handleFieldChange = useCallback((edit: TypedRange) => {
    const fields = { ...(value ?? draft), ...edit }
    const range = rangeOf(fields)
    setDraft(range ? {} : fields)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    if (range || value) onChange?.(range as any)
  }, [onChange, value, draft])

  const handleStartDateChange = useCallback((newValue: AriaDateValue | null) => {
    if (isTypedInFull(newValue)) setFocusedValue(newValue as CalendarDate)
    handleFieldChange({ start: newValue })
  }, [handleFieldChange])

  const handleEndDateChange = useCallback((newValue: AriaDateValue | null) => {
    handleFieldChange({ end: newValue })
  }, [handleFieldChange])

  const startDate = value?.start ?? draft.start ?? null
  const endDate = value?.end ?? draft.end ?? null
  const isEndBeforeStart = isTypedInFull(startDate) && isTypedInFull(endDate) && endDate.compare(startDate) < 0
  const startYearCheck = useShortYearCheck(startDate)
  const endYearCheck = useShortYearCheck(endDate)
  const isEndInvalid = isEndBeforeStart || endYearCheck.isShortYear
  const fieldError = rangeFieldError(isEndBeforeStart, startYearCheck.isShortYear || endYearCheck.isShortYear)
  const errorId = useId()

  return (
    <div className="flex flex-col gap-2">
      {showDateInput && (
        <div className="flex gap-2 items-center">
          <DateField
            value={startDate}
            onChange={handleStartDateChange}
            onBlur={startYearCheck.onBlur}
            isInvalid={startYearCheck.isShortYear}
            aria-label="Start date"
            aria-describedby={startYearCheck.isShortYear ? errorId : undefined}
            autoFocus={autoFocus}
            className="min-w-0 flex-1"
          >
            <DateInput className="h-9" />
          </DateField>
          <span className="text-muted-foreground text-sm">to</span>
          <DateField
            value={endDate}
            onChange={handleEndDateChange}
            onBlur={endYearCheck.onBlur}
            isInvalid={isEndInvalid}
            aria-label="End date"
            aria-describedby={isEndInvalid ? errorId : undefined}
            className="min-w-0 flex-1"
          >
            <DateInput className="h-9" />
          </DateField>
        </div>
      )}
      {showDateInput && fieldError && (
        <p id={errorId} role="alert" className="text-destructive text-sm">
          {fieldError}
        </p>
      )}
      <div className={layoutClassNames[layout].body}>
        {showQuickSelects && (
          <QuickSelectsPanel
            presets={dateRangePresets}
            layout={layout}
            onSelect={(preset) => handleQuickSelect(preset.getRange(timeZone))}
          />
        )}
        <RangeCalendar
          className={composeRenderProps(className, (className) =>
            cn("grow", className)
          )}
          value={value}
          onChange={onChange as AriaRangeCalendarProps<T>["onChange"]}
          focusedValue={focusedValue}
          onFocusChange={setFocusedValue}
          autoFocus={autoFocus && !showDateInput}
          weeksInMonth={fixedHeight ? 6 : undefined}
          {...props}
        >
          <CalendarZoomProvider fixedHeight={fixedHeight}>
            <CalendarHeadingZoomable layout={layout} />
            <CalendarContent>
              <CalendarGrid timeZone={timeZone}>
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
