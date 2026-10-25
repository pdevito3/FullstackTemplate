import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { Dialog, type Key } from 'react-aria-components'
import { today, getLocalTimeZone, type DateValue } from '@internationalized/date'
import { Notification03Icon, Calendar03Icon, ArrowUpDownIcon, Mail01Icon, Add01Icon, Settings01Icon, UserIcon, Calculator01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Notification } from '@/components/notification'
import {
  Command,
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from '@/components/ui/command'
import {
  DropdownMenu,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import {
  Drawer,
  DrawerClose,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from '@/components/ui/drawer'
import { Tooltip, TooltipTrigger } from '@/components/ui/tooltip'
import { Popover, PopoverTrigger } from '@/components/ui/popover'
import { JollyCalendar } from '@/components/ui/calendar'
import { JollyDatePicker } from '@/components/ui/date-picker'
import { Collapsible, CollapsibleContent } from '@/components/ui/collapsible'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
} from '@/components/ui/breadcrumb'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
  SelectGroup,
  SelectLabel,
  SelectSeparator,
} from '@/components/ui/select'
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from '@/components/ui/combobox'
import { MultiSelect, type MultiSelectOption } from '@/components/ui/multi-select'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'

export const Route = createFileRoute('/components')({
  component: ComponentsPage,
})

interface Framework {
  value: string
  label: string
}

const frameworks: Framework[] = [
  { value: 'react', label: 'React' },
  { value: 'vue', label: 'Vue' },
  { value: 'angular', label: 'Angular' },
  { value: 'svelte', label: 'Svelte' },
  { value: 'solid', label: 'Solid' },
  { value: 'qwik', label: 'Qwik' },
  { value: 'preact', label: 'Preact' },
]

const languages: MultiSelectOption[] = [
  { value: 'typescript', label: 'TypeScript' },
  { value: 'javascript', label: 'JavaScript' },
  { value: 'python', label: 'Python' },
  { value: 'rust', label: 'Rust' },
  { value: 'go', label: 'Go' },
  { value: 'java', label: 'Java' },
  { value: 'csharp', label: 'C#' },
]

const countries = [
  { value: 'us', label: 'United States' },
  { value: 'uk', label: 'United Kingdom' },
  { value: 'ca', label: 'Canada' },
  { value: 'au', label: 'Australia' },
  { value: 'de', label: 'Germany' },
  { value: 'fr', label: 'France' },
  { value: 'jp', label: 'Japan' },
]

function ComponentsPage() {
  const [date, setDate] = useState<DateValue>(today(getLocalTimeZone()))
  const [isCollapsibleOpen, setIsCollapsibleOpen] = useState(false)
  const [isCommandOpen, setIsCommandOpen] = useState(false)
  const [selectedFramework, setSelectedFramework] = useState<Key | null>(null)
  const [selectedLanguages, setSelectedLanguages] = useState<Key[]>([])
  const [comboboxValue, setComboboxValue] = useState<Key | null>(null)
  const [autocompleteValue, setAutocompleteValue] = useState('')
  const [autocompleteKey, setAutocompleteKey] = useState<Key | null>(null)

  const frameworkLabel = (key: Key | null) =>
    frameworks.find((framework) => framework.value === key)?.label

  const tableData = [
    { id: 1, name: 'John Doe', email: 'john@example.com', status: 'Active' },
    { id: 2, name: 'Jane Smith', email: 'jane@example.com', status: 'Pending' },
    { id: 3, name: 'Bob Johnson', email: 'bob@example.com', status: 'Inactive' },
  ]

  return (
    <>
      <div className="w-full">
        <div className="max-w-[1200px] mx-auto px-8 py-10 max-md:px-4">
          {/* Header */}
          <div className="mb-10">
            <h1 className="text-4xl font-bold mb-2">Component Library</h1>
            <p className="text-text-secondary text-lg">
              React Aria components with sky accent theme and Nunito Sans font
            </p>
          </div>

          {/* Breadcrumb */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Breadcrumb</h2>
            <Card>
              <CardContent>
                <Breadcrumb aria-label="Example breadcrumb">
                  <BreadcrumbList>
                    <BreadcrumbItem>
                      <BreadcrumbLink href="/">Home</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbItem>
                      <BreadcrumbLink href="/components">Components</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbItem>
                      <BreadcrumbPage>Demo</BreadcrumbPage>
                    </BreadcrumbItem>
                  </BreadcrumbList>
                </Breadcrumb>
              </CardContent>
            </Card>
          </section>

          {/* Buttons */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Buttons</h2>
            <Card>
              <CardContent>
                <div className="flex flex-wrap gap-4">
                  <Button>Default</Button>
                  <Button variant="secondary">Secondary</Button>
                  <Button variant="destructive">Destructive</Button>
                  <Button variant="outline">Outline</Button>
                  <Button variant="ghost">Ghost</Button>
                  <Button variant="link">Link</Button>
                </div>
                <Separator className="my-4" />
                <div className="flex flex-wrap items-center gap-4">
                  <Button size="sm">Small</Button>
                  <Button size="default">Default</Button>
                  <Button size="lg">Large</Button>
                  <Button size="icon" aria-label="Add"><HugeiconsIcon icon={Add01Icon} className="size-4" /></Button>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Badges */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Badges</h2>
            <Card>
              <CardContent>
                <div className="flex flex-wrap gap-4">
                  <Badge>Default</Badge>
                  <Badge variant="secondary">Secondary</Badge>
                  <Badge variant="destructive">Destructive</Badge>
                  <Badge variant="outline">Outline</Badge>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Form Inputs */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Form Inputs</h2>
            <Card>
              <CardContent className="space-y-6">
                <div className="grid gap-4 max-w-sm">
                  <div className="space-y-2">
                    <Label htmlFor="email">Email</Label>
                    <Input id="email" type="email" placeholder="Enter your email" />
                  </div>
                  <JollyDatePicker label="Start date" />
                  <Checkbox>Accept terms and conditions</Checkbox>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Select, Combobox & Autocomplete */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Select, Combobox & Autocomplete</h2>
            <Card>
              <CardContent>
                <div className="grid gap-6 max-w-sm">
                  {/* Select */}
                  <Select
                    value={selectedFramework}
                    onChange={setSelectedFramework}
                    placeholder="Select a framework..."
                    className="flex w-full flex-col gap-2"
                  >
                    <Label>Select (Dropdown)</Label>
                    <SelectTrigger className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectGroup>
                        <SelectLabel>Popular Frameworks</SelectLabel>
                        {frameworks.slice(0, 4).map((framework) => (
                          <SelectItem key={framework.value} id={framework.value}>
                            {framework.label}
                          </SelectItem>
                        ))}
                      </SelectGroup>
                      <SelectSeparator />
                      <SelectGroup>
                        <SelectLabel>Other Frameworks</SelectLabel>
                        {frameworks.slice(4).map((framework) => (
                          <SelectItem key={framework.value} id={framework.value}>
                            {framework.label}
                          </SelectItem>
                        ))}
                      </SelectGroup>
                    </SelectContent>
                    {selectedFramework && (
                      <p className="text-sm text-muted-foreground">
                        Selected: {frameworkLabel(selectedFramework)}
                      </p>
                    )}
                  </Select>

                  {/* Combobox (single select with search) */}
                  <Combobox
                    value={comboboxValue}
                    onChange={setComboboxValue}
                    allowsEmptyCollection
                    className="flex flex-col gap-2"
                  >
                    <Label>Combobox (Searchable Single Select)</Label>
                    <ComboboxInput placeholder="Search frameworks..." className="w-full" showClear />
                    <ComboboxContent>
                      <ComboboxList
                        items={frameworks}
                        renderEmptyState={() => <ComboboxEmpty>No framework found.</ComboboxEmpty>}
                      >
                        {(framework) => (
                          <ComboboxItem id={framework.value} textValue={framework.label}>
                            {framework.label}
                          </ComboboxItem>
                        )}
                      </ComboboxList>
                    </ComboboxContent>
                    {comboboxValue && (
                      <p className="text-sm text-muted-foreground">
                        Selected: {frameworkLabel(comboboxValue)}
                      </p>
                    )}
                  </Combobox>

                  {/* Autocomplete (free-form text with suggestions) */}
                  {/* The text is the value. When the text no longer matches the picked country, the
                      key is cleared, because Escape puts the label of a selected key back over the text. */}
                  <Combobox
                    inputValue={autocompleteValue}
                    onInputChange={(value) => {
                      setAutocompleteValue(value)
                      setAutocompleteKey((key) => (countries.find((c) => c.value === key)?.label === value ? key : null))
                    }}
                    selectedKey={autocompleteKey}
                    onSelectionChange={(key) => {
                      setAutocompleteKey(key)
                      const country = countries.find((c) => c.value === key)
                      if (country) setAutocompleteValue(country.label)
                    }}
                    allowsCustomValue
                    allowsEmptyCollection
                    menuTrigger="focus"
                    className="flex flex-col gap-2"
                  >
                    <Label>Autocomplete (Free-form with Suggestions)</Label>
                    <ComboboxInput
                      placeholder="Type a country..."
                      showTrigger={false}
                      showClear
                      className="w-full"
                    />
                    <ComboboxContent>
                      <ComboboxList
                        items={countries}
                        renderEmptyState={() => <ComboboxEmpty>No countries found.</ComboboxEmpty>}
                      >
                        {(country) => (
                          <ComboboxItem id={country.value} textValue={country.label}>
                            {country.label}
                          </ComboboxItem>
                        )}
                      </ComboboxList>
                    </ComboboxContent>
                    {autocompleteValue && (
                      <p className="text-sm text-muted-foreground">
                        Value: {autocompleteValue}
                      </p>
                    )}
                  </Combobox>

                  {/* Multi-Select */}
                  <div className="space-y-2">
                    <Label>Multi-Select (Multiple with Chips)</Label>
                    <MultiSelect
                      options={languages}
                      value={selectedLanguages}
                      onChange={setSelectedLanguages}
                      placeholder="Select languages..."
                    />
                    {selectedLanguages.length > 0 && (
                      <p className="text-sm text-muted-foreground">
                        Selected: {languages.filter(l => selectedLanguages.includes(l.value)).map(l => l.label).join(', ')}
                      </p>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Cards & Avatars */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Cards & Avatars</h2>
            <div className="grid gap-6 md:grid-cols-2">
              <Card>
                <CardHeader>
                  <CardTitle>Card Title</CardTitle>
                  <CardDescription>Card description goes here</CardDescription>
                </CardHeader>
                <CardContent>
                  <p>Card content with some example text.</p>
                </CardContent>
                <CardFooter>
                  <Button>Action</Button>
                </CardFooter>
              </Card>
              <Card>
                <CardHeader>
                  <CardTitle>Team Members</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="flex -space-x-2">
                    <Avatar className="border-2 border-background">
                      <AvatarImage src="/avatar.png" />
                      <AvatarFallback>CN</AvatarFallback>
                    </Avatar>
                    <Avatar className="border-2 border-background">
                      <AvatarFallback>JD</AvatarFallback>
                    </Avatar>
                    <Avatar className="border-2 border-background">
                      <AvatarFallback>AB</AvatarFallback>
                    </Avatar>
                    <Avatar className="border-2 border-background">
                      <AvatarFallback>+3</AvatarFallback>
                    </Avatar>
                  </div>
                </CardContent>
              </Card>
            </div>
          </section>

          {/* Dialog, Sheet & Drawer */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Dialog, Sheet & Drawer</h2>
            <Card>
              <CardContent>
                <div className="flex flex-wrap gap-4">
                  <ResponsiveDialogTrigger>
                    <Button variant="outline">Open Dialog</Button>
                    <ResponsiveDialog>
                      <ResponsiveDialogHeader>
                        <ResponsiveDialogTitle>Dialog Title</ResponsiveDialogTitle>
                        <ResponsiveDialogDescription>
                          Centered on desktop, slides up from bottom on mobile.
                        </ResponsiveDialogDescription>
                      </ResponsiveDialogHeader>
                      <div className="py-4">
                        <p>Try resizing your window while this is open!</p>
                      </div>
                      <ResponsiveDialogFooter>
                        <ResponsiveDialogClose>Continue</ResponsiveDialogClose>
                      </ResponsiveDialogFooter>
                    </ResponsiveDialog>
                  </ResponsiveDialogTrigger>

                  <SheetTrigger>
                    <Button variant="outline">Open Sheet</Button>
                    <SheetContent>
                      <SheetHeader>
                        <SheetTitle>Sheet Title</SheetTitle>
                        <SheetDescription>
                          This is a sheet panel that slides in from the side.
                        </SheetDescription>
                      </SheetHeader>
                      <div className="px-4">
                        <p>Sheet content goes here.</p>
                      </div>
                    </SheetContent>
                  </SheetTrigger>

                  <DrawerTrigger>
                    <Button variant="outline">Open Drawer</Button>
                    <Drawer>
                      <div className="mx-auto w-full max-w-sm">
                        <DrawerHeader>
                          <DrawerTitle>Drawer Title</DrawerTitle>
                          <DrawerDescription>
                            Swipe down or press Escape to close the drawer.
                          </DrawerDescription>
                        </DrawerHeader>
                        <div className="px-4">
                          <p>Drawer content goes here.</p>
                        </div>
                        <DrawerFooter>
                          <DrawerClose>Done</DrawerClose>
                        </DrawerFooter>
                      </div>
                    </Drawer>
                  </DrawerTrigger>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Dropdown & Popover */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Dropdown Menu & Popover</h2>
            <Card>
              <CardContent>
                <div className="flex flex-wrap gap-4">
                  <DropdownMenuTrigger>
                    <Button variant="outline">
                      <HugeiconsIcon icon={Settings01Icon} className="size-4" />
                      Options
                    </Button>
                    <DropdownMenu>
                      <DropdownMenuGroup>
                        <DropdownMenuLabel>My Account</DropdownMenuLabel>
                      </DropdownMenuGroup>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem>
                        <HugeiconsIcon icon={UserIcon} className="size-4" />
                        Profile
                      </DropdownMenuItem>
                      <DropdownMenuItem>
                        <HugeiconsIcon icon={Mail01Icon} className="size-4" />
                        Messages
                      </DropdownMenuItem>
                      <DropdownMenuItem>
                        <HugeiconsIcon icon={Settings01Icon} className="size-4" />
                        Settings
                      </DropdownMenuItem>
                    </DropdownMenu>
                  </DropdownMenuTrigger>

                  <PopoverTrigger>
                    <Button variant="outline">
                      <HugeiconsIcon icon={Calendar03Icon} className="size-4" />
                      Pick a date
                    </Button>
                    <Popover className="w-auto p-3" placement="bottom start">
                      <Dialog aria-label="Pick a date" className="outline-none">
                        <JollyCalendar
                          value={date}
                          onChange={setDate}
                        />
                      </Dialog>
                    </Popover>
                  </PopoverTrigger>

                  <TooltipTrigger>
                    <Button variant="outline" size="icon" aria-label="Notifications">
                      <HugeiconsIcon icon={Notification03Icon} className="size-4" />
                    </Button>
                    <Tooltip>You have 3 notifications</Tooltip>
                  </TooltipTrigger>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Command */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Command</h2>
            <Card>
              <CardContent>
                <div className="grid gap-6 md:grid-cols-2">
                  <Command className="rounded-lg border">
                    <CommandInput placeholder="Type a command or search..." />
                    <CommandList renderEmptyState={() => <CommandEmpty>No results found.</CommandEmpty>}>
                      <CommandGroup heading="Suggestions">
                        <CommandItem textValue="Calendar">
                          <HugeiconsIcon icon={Calendar03Icon} />
                          Calendar
                        </CommandItem>
                        <CommandItem textValue="Calculator">
                          <HugeiconsIcon icon={Calculator01Icon} />
                          Calculator
                        </CommandItem>
                      </CommandGroup>
                      <CommandSeparator />
                      <CommandGroup heading="Settings">
                        <CommandItem textValue="Profile">
                          <HugeiconsIcon icon={UserIcon} />
                          Profile
                          <CommandShortcut>⌘P</CommandShortcut>
                        </CommandItem>
                        <CommandItem textValue="Settings">
                          <HugeiconsIcon icon={Settings01Icon} />
                          Settings
                          <CommandShortcut>⌘S</CommandShortcut>
                        </CommandItem>
                      </CommandGroup>
                    </CommandList>
                  </Command>
                  <div>
                    <Button variant="outline" onPress={() => setIsCommandOpen(true)}>
                      Open Command Menu
                    </Button>
                    <CommandDialog isOpen={isCommandOpen} onOpenChange={setIsCommandOpen}>
                      <Command>
                        <CommandInput autoFocus placeholder="Type a command or search..." />
                        <CommandList renderEmptyState={() => <CommandEmpty>No results found.</CommandEmpty>}>
                          <CommandGroup heading="Suggestions">
                            <CommandItem onAction={() => setIsCommandOpen(false)}>Calendar</CommandItem>
                            <CommandItem onAction={() => setIsCommandOpen(false)}>Calculator</CommandItem>
                            <CommandItem onAction={() => setIsCommandOpen(false)}>Settings</CommandItem>
                          </CommandGroup>
                        </CommandList>
                      </Command>
                    </CommandDialog>
                  </div>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Toast */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Toast</h2>
            <Card>
              <CardContent>
                <div className="flex flex-wrap gap-4">
                  <Button variant="outline" onPress={() => Notification.success('Changes saved')}>
                    Success
                  </Button>
                  <Button variant="outline" onPress={() => Notification.error('Something went wrong')}>
                    Error
                  </Button>
                  <Button variant="outline" onPress={() => Notification.info('A new version is available')}>
                    Info
                  </Button>
                  <Button variant="outline" onPress={() => Notification.warning('Your session ends in 5 minutes')}>
                    Warning
                  </Button>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Collapsible */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Collapsible</h2>
            <Card>
              <CardContent>
                <Collapsible isExpanded={isCollapsibleOpen} onExpandedChange={setIsCollapsibleOpen}>
                  <div className="flex items-center justify-between">
                    <h4 className="text-sm font-semibold">
                      @peduarte starred 3 repositories
                    </h4>
                    <Button slot="trigger" variant="ghost" size="sm" aria-label="Toggle">
                      <HugeiconsIcon icon={ArrowUpDownIcon} className="size-4" />
                    </Button>
                  </div>
                  <div className="rounded-md border px-4 py-2 font-mono text-sm mt-2">
                    @radix-ui/primitives
                  </div>
                  {/* The closed panel keeps its box, so the margin is inside it. */}
                  <CollapsibleContent>
                    <div className="space-y-2 pt-2">
                      <div className="rounded-md border px-4 py-2 font-mono text-sm">
                        @radix-ui/colors
                      </div>
                      <div className="rounded-md border px-4 py-2 font-mono text-sm">
                        @stitches/react
                      </div>
                    </div>
                  </CollapsibleContent>
                </Collapsible>
              </CardContent>
            </Card>
          </section>

          {/* Table */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Table</h2>
            <Card>
              <CardContent>
                <Table aria-label="Users">
                  <TableHeader>
                    <TableHead isRowHeader>Name</TableHead>
                    <TableHead>Email</TableHead>
                    <TableHead>Status</TableHead>
                  </TableHeader>
                  <TableBody>
                    {tableData.map((row) => (
                      <TableRow key={row.id}>
                        <TableCell className="font-medium">{row.name}</TableCell>
                        <TableCell>{row.email}</TableCell>
                        <TableCell>
                          <Badge variant={row.status === 'Active' ? 'default' : row.status === 'Pending' ? 'secondary' : 'outline'}>
                            {row.status}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          </section>

          {/* Skeleton */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Skeleton</h2>
            <Card>
              <CardContent>
                <div className="flex items-center space-x-4">
                  <Skeleton className="h-12 w-12 rounded-full" />
                  <div className="space-y-2">
                    <Skeleton className="h-4 w-[250px]" />
                    <Skeleton className="h-4 w-[200px]" />
                  </div>
                </div>
              </CardContent>
            </Card>
          </section>

        </div>
      </div>
    </>
  )
}
