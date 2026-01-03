import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { today, getLocalTimeZone, type DateValue } from '@internationalized/date'
import { Bell, Calendar as CalendarIcon, ChevronsUpDown, Mail, Plus, Settings, User } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { JollyCalendar } from '@/components/ui/calendar'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Autocomplete } from '@/components/ui/autocomplete'
import { MultiSelect } from '@/components/ui/multi-select'
import {
  ResponsiveDialog,
  ResponsiveDialogContent,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'

export const Route = createFileRoute('/components')({
  component: ComponentsPage,
})

function ComponentsPage() {
  const [date, setDate] = useState<DateValue>(today(getLocalTimeZone()))
  const [isCollapsibleOpen, setIsCollapsibleOpen] = useState(false)
  const [autocompleteValue, setAutocompleteValue] = useState('')
  const [multiSelectValue, setMultiSelectValue] = useState<string[]>([])

  const autocompleteOptions = [
    { value: 'react', label: 'React' },
    { value: 'vue', label: 'Vue' },
    { value: 'angular', label: 'Angular' },
    { value: 'svelte', label: 'Svelte' },
    { value: 'solid', label: 'Solid' },
  ]

  const multiSelectOptions = [
    { value: 'typescript', label: 'TypeScript' },
    { value: 'javascript', label: 'JavaScript' },
    { value: 'python', label: 'Python' },
    { value: 'rust', label: 'Rust' },
    { value: 'go', label: 'Go' },
  ]

  const tableData = [
    { id: 1, name: 'John Doe', email: 'john@example.com', status: 'Active' },
    { id: 2, name: 'Jane Smith', email: 'jane@example.com', status: 'Pending' },
    { id: 3, name: 'Bob Johnson', email: 'bob@example.com', status: 'Inactive' },
  ]

  return (
    <TooltipProvider>
      <div className="w-full min-h-screen bg-linear-to-br from-bg-gradient-start to-bg-gradient-end text-text-primary">
        <div className="max-w-[1200px] mx-auto px-8 py-10 max-md:px-4">
          {/* Header */}
          <div className="mb-10">
            <h1 className="text-4xl font-bold mb-2">Component Library</h1>
            <p className="text-text-secondary text-lg">
              shadcn/ui components with sky accent theme and Nunito Sans font
            </p>
          </div>

          {/* Breadcrumb */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Breadcrumb</h2>
            <Card>
              <CardContent className="pt-6">
                <Breadcrumb>
                  <BreadcrumbList>
                    <BreadcrumbItem>
                      <BreadcrumbLink href="/">Home</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                      <BreadcrumbLink href="/components">Components</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
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
              <CardContent className="pt-6">
                <div className="flex flex-wrap gap-4">
                  <Button>Default</Button>
                  <Button variant="secondary">Secondary</Button>
                  <Button variant="destructive">Destructive</Button>
                  <Button variant="outline">Outline</Button>
                  <Button variant="ghost">Ghost</Button>
                  <Button variant="link">Link</Button>
                </div>
                <Separator className="my-4" />
                <div className="flex flex-wrap gap-4">
                  <Button size="sm">Small</Button>
                  <Button size="default">Default</Button>
                  <Button size="lg">Large</Button>
                  <Button size="icon"><Plus className="h-4 w-4" /></Button>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Badges */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Badges</h2>
            <Card>
              <CardContent className="pt-6">
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
              <CardContent className="pt-6 space-y-6">
                <div className="grid gap-4 max-w-sm">
                  <div className="space-y-2">
                    <Label htmlFor="email">Email</Label>
                    <Input id="email" type="email" placeholder="Enter your email" />
                  </div>
                  <Checkbox>Accept terms and conditions</Checkbox>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Autocomplete & Multi-Select */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Autocomplete & Multi-Select</h2>
            <Card>
              <CardContent className="pt-6">
                <div className="grid gap-6 max-w-sm">
                  <div className="space-y-2">
                    <Label>Autocomplete (Single Select)</Label>
                    <Autocomplete
                      options={autocompleteOptions}
                      value={autocompleteValue}
                      onValueChange={setAutocompleteValue}
                      placeholder="Select a framework..."
                      searchPlaceholder="Search frameworks..."
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Multi-Select</Label>
                    <MultiSelect
                      options={multiSelectOptions}
                      selected={multiSelectValue}
                      onChange={setMultiSelectValue}
                      placeholder="Select languages..."
                    />
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
                      <AvatarImage src="https://github.com/shadcn.png" />
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

          {/* Dialogs & Sheets */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Dialogs & Sheets</h2>
            <Card>
              <CardContent className="pt-6">
                <div className="flex flex-wrap gap-4">
                  <Dialog>
                    <DialogTrigger render={<Button variant="outline" />}>
                      Open Dialog
                    </DialogTrigger>
                    <DialogContent>
                      <DialogHeader>
                        <DialogTitle>Dialog Title</DialogTitle>
                        <DialogDescription>
                          This is a dialog description. Make changes here.
                        </DialogDescription>
                      </DialogHeader>
                      <div className="py-4">
                        <p>Dialog content goes here.</p>
                      </div>
                      <DialogFooter>
                        <Button>Save changes</Button>
                      </DialogFooter>
                    </DialogContent>
                  </Dialog>

                  <Sheet>
                    <SheetTrigger render={<Button variant="outline" />}>
                      Open Sheet
                    </SheetTrigger>
                    <SheetContent>
                      <SheetHeader>
                        <SheetTitle>Sheet Title</SheetTitle>
                        <SheetDescription>
                          This is a sheet panel that slides in from the side.
                        </SheetDescription>
                      </SheetHeader>
                      <div className="py-4">
                        <p>Sheet content goes here.</p>
                      </div>
                    </SheetContent>
                  </Sheet>

                  <ResponsiveDialog>
                    <ResponsiveDialogTrigger render={<Button variant="outline" />}>
                      Responsive Dialog
                    </ResponsiveDialogTrigger>
                    <ResponsiveDialogContent>
                      <ResponsiveDialogHeader>
                        <ResponsiveDialogTitle>Responsive Dialog</ResponsiveDialogTitle>
                        <ResponsiveDialogDescription>
                          This shows as a dialog on desktop and drawer on mobile.
                        </ResponsiveDialogDescription>
                      </ResponsiveDialogHeader>
                      <div className="py-4">
                        <p>Try resizing your window!</p>
                      </div>
                      <ResponsiveDialogFooter>
                        <Button>Continue</Button>
                      </ResponsiveDialogFooter>
                    </ResponsiveDialogContent>
                  </ResponsiveDialog>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Dropdown & Popover */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Dropdown Menu & Popover</h2>
            <Card>
              <CardContent className="pt-6">
                <div className="flex flex-wrap gap-4">
                  <DropdownMenu>
                    <DropdownMenuTrigger render={<Button variant="outline" />}>
                      <Settings className="mr-2 h-4 w-4" />
                      Options
                    </DropdownMenuTrigger>
                    <DropdownMenuContent>
                      <DropdownMenuLabel>My Account</DropdownMenuLabel>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem>
                        <User className="mr-2 h-4 w-4" />
                        Profile
                      </DropdownMenuItem>
                      <DropdownMenuItem>
                        <Mail className="mr-2 h-4 w-4" />
                        Messages
                      </DropdownMenuItem>
                      <DropdownMenuItem>
                        <Settings className="mr-2 h-4 w-4" />
                        Settings
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>

                  <Popover>
                    <PopoverTrigger render={<Button variant="outline" />}>
                      <CalendarIcon className="mr-2 h-4 w-4" />
                      Pick a date
                    </PopoverTrigger>
                    <PopoverContent className="w-auto p-3" align="start">
                      <JollyCalendar
                        value={date}
                        onChange={setDate}
                      />
                    </PopoverContent>
                  </Popover>

                  <Tooltip>
                    <TooltipTrigger render={<Button variant="outline" size="icon" />}>
                      <Bell className="h-4 w-4" />
                    </TooltipTrigger>
                    <TooltipContent>
                      <p>You have 3 notifications</p>
                    </TooltipContent>
                  </Tooltip>
                </div>
              </CardContent>
            </Card>
          </section>

          {/* Collapsible */}
          <section className="mb-10">
            <h2 className="text-2xl font-semibold mb-4">Collapsible</h2>
            <Card>
              <CardContent className="pt-6">
                <Collapsible open={isCollapsibleOpen} onOpenChange={setIsCollapsibleOpen}>
                  <div className="flex items-center justify-between">
                    <h4 className="text-sm font-semibold">
                      @peduarte starred 3 repositories
                    </h4>
                    <CollapsibleTrigger render={<Button variant="ghost" size="sm" />}>
                      <ChevronsUpDown className="h-4 w-4" />
                      <span className="sr-only">Toggle</span>
                    </CollapsibleTrigger>
                  </div>
                  <div className="rounded-md border px-4 py-2 font-mono text-sm mt-2">
                    @radix-ui/primitives
                  </div>
                  <CollapsibleContent className="space-y-2 mt-2">
                    <div className="rounded-md border px-4 py-2 font-mono text-sm">
                      @radix-ui/colors
                    </div>
                    <div className="rounded-md border px-4 py-2 font-mono text-sm">
                      @stitches/react
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
              <CardContent className="pt-6">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Name</TableHead>
                      <TableHead>Email</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
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
              <CardContent className="pt-6">
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
    </TooltipProvider>
  )
}
