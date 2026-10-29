---
name: frontend-feature
description: Builds the SPA pages of a feature on a Server API. Use when you add a list page with search and a pager, a detail page with actions, a create or edit form, or a search picker over a server list, or when a page shows stale data after a save.
---

# Frontend feature

Build each page in the order of the steps. Each step ends on a check. The rules are in `docs/frontend/`, and this skill links to them.

Before you start, read [frontend.md](../../../docs/frontend.md), [server-state.md](../../../docs/frontend/server-state.md), [client-state.md](../../../docs/frontend/client-state.md), [code-style.md](../../../docs/frontend/code-style.md), [testing.md](../../../docs/frontend/testing.md), and the rules and "In the SPA" of [messages.md](../../../docs/messages.md).

## The reference feature

Copy the shape of the reference feature, not its names.

<!-- example:start -->
The reference is the workshops and venues example. Open each file of a part before you write that part.

| Part | Page and code | Tests |
|---|---|---|
| List | [`workshops/index.tsx`](<../../../frontend/src/routes/(examples)/workshops/index.tsx>), [`get-workshop-list.ts`](../../../frontend/src/examples/workshops/apis/get-workshop-list.ts), [`workshop.keys.ts`](../../../frontend/src/examples/workshops/apis/workshop.keys.ts) | [`workshop-list.test.tsx`](../../../frontend/src/examples/workshops/workshop-list.test.tsx) |
| Detail | [`$id/index.tsx`](<../../../frontend/src/routes/(examples)/workshops/$id/index.tsx>), [`workshop-actions.tsx`](../../../frontend/src/examples/workshops/components/workshop-actions.tsx), [`send-workshop-action.ts`](../../../frontend/src/examples/workshops/apis/send-workshop-action.ts) | |
| Edit form | [`$id/edit.tsx`](<../../../frontend/src/routes/(examples)/workshops/$id/edit.tsx>), [`workshop-edit-form.tsx`](../../../frontend/src/examples/workshops/components/workshop-edit-form.tsx), [`update-workshop.ts`](../../../frontend/src/examples/workshops/apis/update-workshop.ts), [`workshop-fields-schema.ts`](../../../frontend/src/examples/workshops/components/workshop-fields-schema.ts) | [`workshop-edit-form.test.tsx`](../../../frontend/src/examples/workshops/components/workshop-edit-form.test.tsx) |
| Create form | [`workshops/new.tsx`](<../../../frontend/src/routes/(examples)/workshops/new.tsx>), [`workshop-create-form.tsx`](../../../frontend/src/examples/workshops/components/workshop-create-form.tsx), [`add-workshop.ts`](../../../frontend/src/examples/workshops/apis/add-workshop.ts) | |
| Delete | The reference has no delete, because the Server has no delete for workshops or venues. Use `leaveDeletedRecord` (below) | |
| Search picker | [`venue-picker.tsx`](../../../frontend/src/examples/venues/components/venue-picker.tsx), [`get-venue-list.ts`](../../../frontend/src/examples/venues/apis/get-venue-list.ts), [`get-venue.ts`](../../../frontend/src/examples/venues/apis/get-venue.ts) | [`venue-picker.test.tsx`](../../../frontend/src/examples/venues/components/venue-picker.test.tsx) |
| Functional suite | | [`workshops.spec.ts`](../../../frontend/tests/features/examples/workshops/workshops.spec.ts), [`workshops.tasks.ts`](../../../frontend/tests/features/examples/workshops/workshops.tasks.ts), [`workshops.locators.ts`](../../../frontend/tests/features/examples/workshops/workshops.locators.ts), [`workshops-api.ts`](../../../frontend/tests/support/examples/workshops-api.ts), [`workshop-builder.ts`](../../../frontend/tests/support/examples/workshop-builder.ts) |
<!-- example:end -->

If `frontend/src/examples/` does not exist, `scripts/remove-examples.cs` deleted the reference feature from this app. Read it in the template repository, in [`frontend/src/examples/`](https://github.com/pdevito3/FullstackTemplate/tree/main/frontend/src/examples), [`frontend/src/routes/(examples)/`](<https://github.com/pdevito3/FullstackTemplate/tree/main/frontend/src/routes/(examples)>), the `examples` folders of [`frontend/tests/`](https://github.com/pdevito3/FullstackTemplate/tree/main/frontend/tests), and the `examples` folders of the [`verify-fullstack-template`](https://github.com/pdevito3/FullstackTemplate/tree/main/.agents/skills/verify-fullstack-template) skill. Copy no file from it into a folder named `examples`.

The shared parts of a page below are in the app, also after the removal. Use them. Do not write a second copy. After the removal, the steps use these parts, the code samples of the rule docs, and the [patterns](#patterns) at the end of this skill:

| Part | Use |
|---|---|
| A query that failed, with a retry | `QueryError` in [`components/query-error.tsx`](../../../frontend/src/components/query-error.tsx) |
| The page heading. The root layout moves the focus to the `h1` of a page after a navigation. A page moves the focus to it after an action | `PageHeading` in [`components/page-heading.tsx`](../../../frontend/src/components/page-heading.tsx) |
| The refusals of a save, on the fields and above the buttons | `useServerErrors` in [`hooks/use-server-errors.ts`](../../../frontend/src/hooks/use-server-errors.ts) and `RefusedSaveAlert` in [`components/refused-save-alert.tsx`](../../../frontend/src/components/refused-save-alert.tsx) |
| The focus on the first invalid field, in the order of the form | `focusFirstError` in [`lib/focus-first-error.ts`](../../../frontend/src/lib/focus-first-error.ts) |
| The search box of a list, with the debounce, and the text that follows Back | `SearchBox` in [`components/search-box.tsx`](../../../frontend/src/components/search-box.tsx) |
| The debounce of the search of a picker | `useDebouncedCallback` in [`hooks/use-debounced-callback.ts`](../../../frontend/src/hooks/use-debounced-callback.ts) |
| The pager of a list, and the load of the next page of a picker | `Pager` in `components/ui/pager.tsx`, and `ComboboxLoadMore` in `components/ui/combobox.tsx` |
| The limits of a field | `stringWithMaxLengthOf`, `maxLengthOf`, `minimumOf`, and `maximumOf` in [`lib/api-contract.ts`](../../../frontend/src/lib/api-contract.ts) |
| A stale save, and the reload that keeps the edits | `StaleVersionAlert` and `LatestLoadedNote` in [`components/stale-version-alert.tsx`](../../../frontend/src/components/stale-version-alert.tsx), `useLoadLatest` in [`hooks/use-load-latest.ts`](../../../frontend/src/hooks/use-load-latest.ts), and `isStaleVersionError` in [`lib/problem-details.ts`](../../../frontend/src/lib/problem-details.ts) |
| The cache and the next page after a delete | `leaveDeletedRecord` in [`lib/leave-deleted-record.ts`](../../../frontend/src/lib/leave-deleted-record.ts) |
| A button for each action in `allowedActions`, with a label from a `Record` of the generated union | `UserActions` in [`domain/users/components/user-actions.tsx`](../../../frontend/src/domain/users/components/user-actions.tsx) |
| The header of a list answer in a mock | `paginationHeaders` in [`tests/support/api/pagination.ts`](../../../frontend/tests/support/api/pagination.ts) |

## Steps

Put the code of an area in `frontend/src/domain/<area>/`: the hooks and the keys in `apis/`, and the components in `components/`. Put the pages in the route folder of their URL, such as a new folder `frontend/src/routes/rooms/` for the URL `/rooms`. If the area already has keys, components, a builder, or an API file, add to them, and run their tests after each change.

1. **The contract.** The Server endpoints exist, and the generated client is current. Use the `backend-feature` skill for the Server and the `api-contract` skill for the client. The list endpoint takes a page number, a page size, and a search or a filter. The DTO has `allowedActions` and `version`. Each text field and number field of a request DTO has a limit in the contract, and so does the `search` query parameter (`[MaxLength]` on the handler parameter, frontend.md). Done when `src/api/generated/<tag>/clients/` has a client for each call, and `maxLengthOf` and `maximumOf` do not throw for the fields of the form.
2. **The keys and the hooks.** Write `<area>.keys.ts` (server-state.md, "Key factory"). Give all the params of the list to the list key. When the area has a list and a picker, give each its own key under `lists()`, because one is a `useQuery` and one a `useInfiniteQuery` and they cannot share a cache entry. Write one hook for each call: the list with `keepPreviousData` and `parsePagination`, the detail as `queryOptions`, the picker search with `useInfiniteQuery`, and one mutation for each write. Make each mutation update the cache as the table below says. Done when `pnpm lint` passes. The lint finds the common forms of a value that the query function reads and the key does not have, and of a key factory that drops a field. It does not find all of them: read "Check" in server-state.md for the forms that it does not find, and check those forms in your hooks by reading them.
3. **The list page.** Read `search` and `page` from the URL with `validateSearch`. Build the schema from the generated query schemas with `.catch(undefined)`, so that a bad URL falls back to the default (frontend.md, "Routes"). If the list takes QueryKit `filters` and no `search`, write the `search` schema by hand (`z.string().optional().catch(undefined)`), and turn the text into a filter in a pure function with a unit test. Use the operator `@=*` (contains, ignore case). QueryKit has no escape in a quoted value: put text with a `"` in a raw string (`"""text"""`). Give the unit test a text with a `"`, and search for such a text on the stack in step 9. Show each state with an early return: loading (`role="status"`), error (`QueryError`), empty on a page after page 1 (a link to page 1), empty for a search (the search in quotes), and empty (how to add one, if the area has a create page). Use `SearchBox` for the search, and take its `maxLength` from the contract. The limit of `filters` is for the whole filter text, not for the text that the user types, so take the `maxLength` of the box from the field that the filter matches, such as `maxLengthOf(roomForCreationDtoSchema.shape.name)`. In its `onSearch`, navigate with `replace: true`, and change the URL only when it still has the search that the box started from (`base`), as the list pattern below does. Give the table `aria-label`, and give `whitespace-nowrap` to each cell that is not prose (code-style.md, "Styles"). On a phone (`useIsMobile`), drop the columns that do not wrap, and show their values under the name. A list of tenant data waits for `useMembership`: it gives `enabled: false` to the list query while the status is `loading` or `no-tenant`, and it shows a link to `/start-tenant` when the user has no tenant. Done when the page in the URL survives a reload, and Back from a detail page shows the list with its search in the box. The search uses `replace: true`, so Back does not step through the typed searches.
4. **The detail page.** Return early for loading and error. Show the title in `PageHeading`. Show a button only for each entry of `allowedActions` (server-state.md, rule 8). An action that needs input, such as a role, asks for it in a form in the dialog. Give each option of that form its label in a `Record` too (messages.md). Give each action its label in a `Record` of the generated union, so that a new action on the Server fails the build until it has a label. Ask for a confirm in a `ResponsiveDialog` before an action that cannot be undone. Close the dialog when the user confirms: make the confirm button a `ResponsiveDialogClose` that calls the action. A dialog that is still open keeps the focus inside it, and it gives the focus back to its trigger when it closes, so a focus move while it is open has no effect. Read the JSDoc of `components/ui/responsive-dialog.tsx` for the gap under the header. After an action, move the focus to the heading. For a delete, ask first, then call `leaveDeletedRecord` in the `onSuccess` of `mutate`, as the detail pattern below does: show the toast with `Notification.success`, go to the list, and let the root layout move the focus to the heading of the list. After a failed delete, the user stays on the page, and the focus goes to the heading. Do not remove the detail in the mutation hook, because the page that is still on the screen reads it again and gets a 404. Done when a user with no permission sees no button, and, for a delete, a functional test sees one toast (count it one time, as testing.md, "Facts about this app", says), one `GET` of the record, and the focus on the heading of the list.
5. **The edit form.** The page renders the form only after the detail query has data, and only when `allowedActions` has `Update`. Otherwise it says why. The form schema takes each limit from the generated Zod schema of the request DTO (frontend.md, "Forms"). Put the `version` in `defaultValues` and in the PUT body (server-state.md, "Forms with server data"). Use `shouldFocusError: false` with `focusFirstError`. Before a save, clear the refusals. After a refusal, show it with `useServerErrors`. For a stale save (a 409 with the stale-version type), keep the edits in the form, and show `StaleVersionAlert` with the name of the record. Its button calls `load` of `useLoadLatest` with a function that refetches the detail query and gives the form values of the new record. Each field that the new record changed takes the saved value, each other field keeps the edit of the user, and the focus goes to the first changed field. `LatestLoadedNote` names the fields that changed. Clear the refusals after the load, and clear the note before a save. Do not send the PUT again with the new version. After a save, show a toast with `Notification.success` and go to the detail page. Done when the component test of the stale save passes.
6. **The create form.** Do step 5 with no `version` and no stale reload. If the entity has a `ForStaleVersion` index, show the create race in its own alert, because the text of `StaleVersionAlert` says that the record changed. A create that gets a 409 keeps the input of the user. After a save, go to the detail page of the new record.
7. **The search picker.** Build it on `Combobox` with the `items` prop, so that React Aria does not filter again what the Server filtered. Load the picked record by its ID with the detail hook. Put the pick first in the options when no loaded page has it. The text of the input after a pick is the name of the pick, so it is not a search. On a pick, put the record in the detail cache. Take the pick from the detail query also when a loaded page has it, so that a rename shows. Load the next page with `ComboboxLoadMore`. When the search fails, the empty state says so and what to do (messages.md, rules 3 and 7), not that no record matches, and the list searches again when it opens again. In a form, wrap the picker in a `Controller`, and give `field.ref` to the input. Done when component tests show that a pick from page 2 and a pick from a link show their names, that a rename of a pick from a link and of a pick from page 1 shows after the detail query reads the record again, and that a failed search shows its message and searches again on the next open.
8. **The tests.** Use the `writing-tests` skill. Write a test for each limit of a form schema, in the component test of the form or in a unit test of the schema. Put a schema that a test imports in its own `*-schema.ts` file, as `workshop-fields-schema.ts` does, because `react-refresh/only-export-components` refuses it in a component file. Write component tests for each part that the area has: the stale save of the edit form, the picker (step 7), and the search and the page of the list in the URL. Write a builder in `tests/support/builders/` and an API file in `tests/support/api/` for the area, or add to the ones that exist, then a functional spec that goes from the list to the detail and to each form of the area. Read back the list query and the PUT body with `api.last`. Done when `pnpm lint`, `pnpm test`, `pnpm build`, and `pnpm test:e2e` pass, and the new spec passes 10 times (`pnpm test:e2e <spec> --repeat-each=10`).
9. **The proof.** Add a link to the list in `components/app-sidebar.tsx`. Add a feature file and a recipe to the feature map of the `verify-fullstack-template` skill. Start the tenant as [`recipes/start-tenant.mjs`](../verify-fullstack-template/recipes/start-tenant.mjs) does, and delete it through the API at the end in a `finally` block, so that the next run starts with no tenant. Give one record a long name with no spaces. Run the recipe on a stack, then run `sweep.sh` on it. The sweep puts a 100-character value into each name and text field of each API answer (`nameKeys` in `sweep.mjs`), so a form opens with those values, and a save sends them back to the Server. A field with a limit under 100 then gets a 422, and the saved long value stays in the database for the next run. So in a recipe that saves, fill each text field of the form with a value of the recipe before the save, and compare the screen with the API answer, not with a literal. Done when the sweep prints `sweep: PASSED`, and you read each screenshot and find no text that is cut off: the sweep does not find all of it.

## The cache after a write

| Write | In `onMutate` | In `onSuccess` | In `onError` |
|---|---|---|---|
| Create | | Invalidate `lists()` | |
| Update | Cancel `detail(id)` | Put the answer in `detail(id)`. Invalidate `lists()` | |
| An action, such as cancel | Cancel `detail(id)` | Put the answer in `detail(id)`. Invalidate `lists()` | Invalidate `detail(id)` and `lists()`, because another user can have changed the record |
| Delete | | Nothing in the hook. The page calls `leaveDeletedRecord` in the `onSuccess` of `mutate`: it invalidates `lists()`, goes to the list, then removes `detail(id)` | |

A write that changes the records of another area also invalidates the lists and the details of that area.

## Patterns

Use these when the reference feature is not in the app. Change the names. The rule docs have the key factory, the query hook, and the mutation hook (server-state.md), the search schema of a route, and the form schema (frontend.md).

The list page, in `routes/<url>/index.tsx`:

```tsx
const listSearchSchema = z.object({
  search: getRoomListQuerySearchSchema.catch(undefined),
  page: getRoomListQueryPageNumberSchema.catch(undefined),
})
const searchMaxLength = maxLengthOf(getRoomListQuerySearchSchema.unwrap())

export const Route = createFileRoute('/rooms/')({ validateSearch: listSearchSchema, component: RoomsPage, staticData: { breadcrumb: 'Rooms' } })

function RoomsPage() {
  const { search = '', page = 1 } = Route.useSearch()
  const navigate = Route.useNavigate()
  const membership = useMembership().status
  const list = useRoomList({ search, page }, { enabled: membership !== 'loading' && membership !== 'no-tenant' })
  // A search that the user typed before Back does not replace the search that Back opened
  const handleSearch = (text: string, base: string) =>
    navigate({ search: (prev) => ((prev.search ?? '') === base ? { search: text || undefined } : prev), replace: true })
  const handlePageChange = (pageNumber: number) =>
    navigate({ search: (prev) => ({ ...prev, page: pageNumber === 1 ? undefined : pageNumber }) })

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8">
      <h1 className="text-3xl font-bold tracking-tight">Rooms</h1>
      <SearchBox label="Search rooms" placeholder="Name contains" search={search} maxLength={searchMaxLength} onSearch={handleSearch} />
      {/* RoomRows returns early for each state of step 3, and for a user with no tenant, then shows the Table and the Pager */}
      <RoomRows list={list} search={search} page={page} onPageChange={handlePageChange} />
    </div>
  )
}
```

The detail page with a delete, in `routes/<url>/$id/index.tsx`. The delete hook is a `useMutation` with only a `mutationFn`:

```tsx
function RoomPage() {
  const { id } = Route.useParams()
  const room = useRoom(id)
  const remove = useDeleteRoom()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const headingRef = useRef<HTMLHeadingElement>(null)

  if (room.isPending) return <p role="status" aria-label="Loading room">Loading room</p>
  if (room.isError) return <QueryError title="The room did not load." error={room.error} onRetry={() => room.refetch()} />

  const handleDelete = () =>
    remove.mutate(id, {
      onSuccess: () => {
        Notification.success('Room deleted')
        return leaveDeletedRecord(queryClient, { detail: RoomKeys.detail(id), lists: RoomKeys.lists(), leave: () => navigate({ to: '/rooms' }) })
      },
      onError: () => headingRef.current?.focus(),
    })

  return (
    <>
      <PageHeading ref={headingRef}>{room.data.name}</PageHeading>
      {/* The other actions, as UserActions shows them */}
      <ResponsiveDialogTrigger>
        <Button variant="destructive" isPending={remove.isPending}>Delete</Button>
        <ResponsiveDialog>
          <ResponsiveDialogHeader>
            <ResponsiveDialogTitle>Delete this room?</ResponsiveDialogTitle>
            <ResponsiveDialogDescription>You cannot undo a delete.</ResponsiveDialogDescription>
          </ResponsiveDialogHeader>
          <ResponsiveDialogFooter>
            <ResponsiveDialogClose>Keep room</ResponsiveDialogClose>
            <ResponsiveDialogClose variant="destructive" onPress={handleDelete}>Delete room</ResponsiveDialogClose>
          </ResponsiveDialogFooter>
        </ResponsiveDialog>
      </ResponsiveDialogTrigger>
    </>
  )
}
```

The stale save in the edit form:

```tsx
const latest = useLoadLatest(form, ROOM_FIELD_NAMES)

const handleLoadLatest = async () => {
  // onLoadLatest refetches the detail query and gives the new record, or undefined when the refetch failed
  const loaded = await latest.load(async () => {
    const record = await onLoadLatest()
    return record && valuesOf(record)
  })
  if (loaded) serverErrors.clear()
}

// In the form, after RefusedSaveAlert:
<StaleVersionAlert noun="room" problem={serverErrors.problem} isLoadingLatest={latest.isLoading} onLoadLatest={handleLoadLatest} />
<LatestLoadedNote noun="room" changed={latest.changed} labels={FIELD_LABELS} />
```
