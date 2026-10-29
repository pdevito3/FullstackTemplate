// The page of the functional tests of the shared components: the date picker, the overlays, and the toasts. It is not
// an example, so these tests stay when scripts/remove-examples.cs deletes the components page. It shows the components
// in the layout of the app, with the router devtools. Only the Vite dev server serves it, not the production build.
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRootRoute, createRoute, createRouter, RouterProvider } from '@tanstack/react-router'
import { Notification } from '@/components/notification'
import { ThemeProvider } from '@/components/theme-provider'
import { Toaster } from '@/components/toaster'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { JollyDatePicker } from '@/components/ui/date-picker'
import { Drawer, DrawerClose, DrawerFooter, DrawerHeader, DrawerTitle, DrawerTrigger } from '@/components/ui/drawer'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'
import { SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { Route as AppRoot } from '@/routes/__root'
import '@/lib/api-client'
import '@/index.css'

export function SharedComponents() {
  return (
    <div className="grid max-w-sm gap-6">
      <h1 className="text-2xl font-semibold">Shared components</h1>
      <JollyDatePicker label="Start date" />
      <Checkbox>Accept terms and conditions</Checkbox>
      <div className="flex flex-wrap gap-4">
        <ResponsiveDialogTrigger>
          <Button variant="outline">Open Dialog</Button>
          <ResponsiveDialog>
            <ResponsiveDialogHeader>
              <ResponsiveDialogTitle>Dialog Title</ResponsiveDialogTitle>
              <ResponsiveDialogDescription>Centered on a wide screen. On a phone, it slides up from the bottom.</ResponsiveDialogDescription>
            </ResponsiveDialogHeader>
            <p className="py-4">The content of the dialog.</p>
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
            </SheetHeader>
          </SheetContent>
        </SheetTrigger>
        <DrawerTrigger>
          <Button variant="outline">Open Drawer</Button>
          <Drawer>
            <DrawerHeader>
              <DrawerTitle>Drawer Title</DrawerTitle>
            </DrawerHeader>
            <DrawerFooter>
              <DrawerClose>Done</DrawerClose>
            </DrawerFooter>
          </Drawer>
        </DrawerTrigger>
      </div>
      <div className="flex flex-wrap gap-4">
        <Button variant="outline" onPress={() => Notification.success('Changes saved')}>Success</Button>
        <Button variant="outline" onPress={() => Notification.error('Something went wrong')}>Error</Button>
        <Button variant="outline" onPress={() => Notification.info('A new version is available')}>Info</Button>
        <Button variant="outline" onPress={() => Notification.warning('Your session ends in 5 minutes')}>Warning</Button>
      </div>
    </div>
  )
}

const root = createRootRoute({ component: AppRoot.options.component })
const page = createRoute({
  getParentRoute: () => root,
  path: '/',
  component: SharedComponents,
  staticData: { breadcrumb: 'Shared components' },
})
const router = createRouter({
  routeTree: root.addChildren([page]),
  history: createMemoryHistory({ initialEntries: ['/'] }),
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider defaultTheme="system" storageKey="ui-theme">
      <QueryClientProvider client={new QueryClient()}>
        <RouterProvider router={router} />
        <Toaster />
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
)
