import { createContext, useContext, useState, useMemo, useEffect } from "react"
import { CalendarDate, endOfMonth } from "@internationalized/date"
import { ArrowLeft01Icon, ArrowRight01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"
import {
  Button as AriaButton,
  CalendarStateContext as AriaCalendarStateContext,
  ListBox as AriaListBox,
  ListBoxItem as AriaListBoxItem,
  RangeCalendarStateContext as AriaRangeCalendarStateContext,
  useLocale,
  type RangeCalendarState,
  type Selection as AriaSelection,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { browserTimeZone, formatDate, todayAt } from "@/lib/zoned-time"
import { buttonVariants } from "@/components/ui/button-variants"
import { layoutClassNames, type CalendarLayout } from "@/components/ui/calendar-layout"

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
  fixedHeight: boolean
}

const CalendarZoomContext = createContext<CalendarZoomContextValue | null>(null)

function useCalendarState() {
  const calendarState = useContext(AriaCalendarStateContext)
  const rangeState = useContext(AriaRangeCalendarStateContext)
  return calendarState ?? rangeState
}

interface CalendarZoomProviderProps {
  children: React.ReactNode
  fixedHeight: boolean
}

function CalendarZoomProvider({ children, fixedHeight }: CalendarZoomProviderProps) {
  const state = useCalendarState()
  const currentYear = state?.focusedDate?.year ?? todayAt(browserTimeZone(), new Date()).year

  const [view, setView] = useState<CalendarView>("days")
  // eslint-disable-next-line server-state/no-copied-state -- the user moves the shown year, and the effect below follows the focused date
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
      fixedHeight,
    }),
    [view, viewYear, viewDecadeStart, fixedHeight]
  )

  return (
    <CalendarZoomContext.Provider value={contextValue}>
      {children}
    </CalendarZoomContext.Provider>
  )
}

// Helper to get days in a month
function getDaysInMonth(year: number, month: number): number {
  return endOfMonth(new CalendarDate(year, month, 1)).day
}

// The month and year pickers are listboxes in a grid layout: the arrow keys move in two dimensions, and Enter or a
// click picks an item. Each picker is at least w-63, the width of the day grid (7 cells of w-9), so that the popover
// keeps its size. In the narrow layout, the picker fills the column, as the day grid does.
// With a fixed height, the four rows of a picker share the height of the days view.
function pickerClassName(fixedHeight: boolean) {
  return cn("grid w-full min-w-63 grid-cols-3 gap-1 p-1 outline-none", fixedHeight && "h-full auto-rows-fr")
}

function pickerItemClassName(fixedHeight: boolean) {
  return ({ isSelected }: { isSelected: boolean }) =>
    cn(
      buttonVariants({ variant: "ghost" }),
      "h-9 cursor-default text-sm font-normal",
      fixedHeight && "h-auto",
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
    return Array.from({ length: 12 }, (_, i) => ({
      index: i + 1,
      name: formatDate(new CalendarDate(2024, i + 1, 1), { month: "short" }, locale),
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
      className={pickerClassName(zoomContext.fixedHeight)}
    >
      {months.map(({ index, name }) => (
        <AriaListBoxItem key={index} id={index} textValue={name} className={pickerItemClassName(zoomContext.fixedHeight)}>
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
      className={pickerClassName(zoomContext.fixedHeight)}
    >
      {years.map((year) => (
        <AriaListBoxItem key={year} id={year} textValue={String(year)} className={pickerItemClassName(zoomContext.fixedHeight)}>
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
  const rangeState = useContext(AriaRangeCalendarStateContext)

  if (!zoomContext || zoomContext.view === "days") {
    return <>{children}</>
  }

  keepRangeOpenInPicker(rangeState)
  const picker = zoomContext.view === "months" ? <MonthPickerGrid /> : <YearPickerGrid />
  if (!zoomContext.fixedHeight) {
    return picker
  }

  // The hidden day grid gives the picker the height of the days view. The day grid mounts again after a pick, so the
  // focus moves to its cell.
  return (
    <div className="grid">
      <div inert aria-hidden className="invisible col-start-1 row-start-1">
        {children}
      </div>
      <div className="col-start-1 row-start-1">{picker}</div>
    </div>
  )
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
    return formatDate(new CalendarDate(state.focusedDate.year, state.focusedDate.month, 1), {
      month: "long",
      year: "numeric",
    })
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

// React Aria commits a range on a pointer release inside the calendar that is not on a button (useRangeCalendar,
// commitBehavior). Month and year items are options, so a pick would end the range. The range state is a new object
// on each render, so the no-op lasts only while a picker shows. It also skips the commit on blur in that time.
function keepRangeOpenInPicker(state: RangeCalendarState | null) {
  if (!state) return
  state.commitSelection = () => {}
}

export { CalendarContent, CalendarHeadingZoomable, CalendarZoomProvider }
