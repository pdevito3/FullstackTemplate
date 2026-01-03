import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import {
  FilterBuilder,
  toQueryKitString,
  type FilterConfig,
  type FilterPreset,
  type FilterState,
} from '@/components/filter-builder'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

export const Route = createFileRoute('/filter-demo')({
  component: FilterDemoPage,
})

function FilterDemoPage() {
  const [filterState, setFilterState] = useState<FilterState>({
    filters: [],
    rootLogicalOperator: 'AND',
  })

  const filterOptions: FilterConfig[] = [
    {
      propertyKey: 'status',
      propertyLabel: 'Status',
      controlType: 'multiselect',
      options: [
        { value: 'open', label: 'Open' },
        { value: 'in-progress', label: 'In Progress' },
        { value: 'closed', label: 'Closed' },
        { value: 'done', label: 'Done' },
        { value: 'blocked', label: 'Blocked' },
      ],
      defaultOperator: '^^',
    },
    {
      propertyKey: 'assignee',
      propertyLabel: 'Assignee',
      controlType: 'multiselect',
      options: [
        { value: '00000000-0000-0000-0000-000000000001', label: 'Current User' },
        { value: '480c3396-bb7c-4ed7-a7a2-310c0cd95edc', label: 'Paul DeVito' },
        { value: 'ee1c5169-07fa-45a4-ab07-7c1602f612bf', label: 'John Smith' },
        { value: 'a3b8c9d0-e1f2-4a5b-8c7d-9e0f1a2b3c4d', label: 'Alice Johnson' },
        { value: 'f5e6d7c8-b9a0-4321-fedc-ba9876543210', label: 'Bob Williams' },
      ],
      defaultOperator: '^^',
    },
    {
      propertyKey: 'labels',
      propertyLabel: 'Labels',
      controlType: 'multiselect',
      options: [
        { value: 'bug', label: 'Bug' },
        { value: 'feature', label: 'Feature' },
        { value: 'enhancement', label: 'Enhancement' },
        { value: 'documentation', label: 'Documentation' },
        { value: 'urgent', label: 'Urgent' },
      ],
      operators: ['^^', '!^^', '^$', '!^$'],
    },
    {
      propertyKey: 'firstName',
      propertyLabel: 'First Name',
      controlType: 'text',
      operators: ['==', '!=', '@=', '!@=', '_=', '!_=', '_-=', '!_-='],
      defaultOperator: '==',
    },
    {
      propertyKey: 'lastName',
      propertyLabel: 'Last Name',
      controlType: 'text',
      operators: ['==', '!=', '@=', '!@=', '_=', '!_=', '_-=', '!_-='],
      defaultOperator: '==',
    },
    {
      propertyKey: 'email',
      propertyLabel: 'Email',
      controlType: 'text',
      operators: ['==', '!=', '@=', '!@=', '_=', '!_=', '_-=', '!_-='],
      defaultOperator: '@=',
    },
    {
      propertyKey: 'createdAt',
      propertyLabel: 'Created Date',
      controlType: 'date',
    },
    {
      propertyKey: 'updatedAt',
      propertyLabel: 'Updated Date',
      controlType: 'date',
    },
    {
      propertyKey: 'dueDate',
      propertyLabel: 'Due Date',
      controlType: 'date',
    },
    {
      propertyKey: 'comments',
      propertyLabel: 'Comment Count',
      controlType: 'number',
      operators: ['#==', '#!=', '#>', '#<', '#>=', '#<='],
      defaultOperator: '#>=',
    },
    {
      propertyKey: 'priority',
      propertyLabel: 'Priority',
      controlType: 'number',
      operators: ['==', '!=', '>', '<', '>=', '<='],
      defaultOperator: '==',
    },
    {
      propertyKey: 'isBlocked',
      propertyLabel: 'Is Blocked',
      controlType: 'boolean',
    },
    {
      propertyKey: 'isArchived',
      propertyLabel: 'Is Archived',
      controlType: 'boolean',
    },
  ]

  const presets: FilterPreset[] = [
    {
      label: 'My Open Issues',
      filter: {
        filters: [
          {
            id: crypto.randomUUID(),
            propertyKey: 'assignee',
            propertyLabel: 'Assignee',
            controlType: 'multiselect',
            operator: '^^',
            value: ['00000000-0000-0000-0000-000000000001'],
            selectedLabels: ['Current User'],
          },
          {
            id: crypto.randomUUID(),
            propertyKey: 'status',
            propertyLabel: 'Status',
            controlType: 'multiselect',
            operator: '^^',
            value: ['open', 'in-progress'],
            selectedLabels: ['Open', 'In Progress'],
          },
        ],
        rootLogicalOperator: 'AND',
      },
    },
    {
      label: 'Urgent Bugs',
      filter: {
        filters: [
          {
            id: crypto.randomUUID(),
            propertyKey: 'labels',
            propertyLabel: 'Labels',
            controlType: 'multiselect',
            operator: '^$',
            value: ['bug'],
            selectedLabels: ['Bug'],
          },
          {
            id: crypto.randomUUID(),
            propertyKey: 'labels',
            propertyLabel: 'Labels',
            controlType: 'multiselect',
            operator: '^$',
            value: ['urgent'],
            selectedLabels: ['Urgent'],
          },
        ],
        rootLogicalOperator: 'AND',
      },
    },
    {
      label: 'Recently Updated',
      filter: {
        filters: [
          {
            id: crypto.randomUUID(),
            propertyKey: 'updatedAt',
            propertyLabel: 'Updated Date',
            controlType: 'date',
            operator: '==',
            value: {
              mode: 'after',
              startDate: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000), // 7 days ago
            },
          },
        ],
        rootLogicalOperator: 'AND',
      },
    },
    {
      label: 'Complex Nested Query',
      filter: {
        filters: [
          {
            id: crypto.randomUUID(),
            type: 'group',
            filters: [
              {
                id: crypto.randomUUID(),
                propertyKey: 'firstName',
                propertyLabel: 'First Name',
                controlType: 'text',
                operator: '==',
                value: 'Jane',
                caseSensitive: true,
              },
              {
                id: crypto.randomUUID(),
                propertyKey: 'comments',
                propertyLabel: 'Comment Count',
                controlType: 'number',
                operator: '#<',
                value: 10,
              },
            ],
            logicalOperator: 'AND',
          },
          {
            id: crypto.randomUUID(),
            propertyKey: 'firstName',
            propertyLabel: 'First Name',
            controlType: 'text',
            operator: '==',
            value: 'John',
            caseSensitive: true,
          },
        ],
        rootLogicalOperator: 'OR',
      },
    },
  ]

  const queryString = toQueryKitString(filterState)

  return (
    <div className="container mx-auto py-8 space-y-8">
      <div>
        <h1 className="text-3xl font-bold mb-2">Filter Builder Demo</h1>
        <p className="text-muted-foreground">
          Test the dynamic filter component with various property types,
          operators, and nested grouping.
        </p>
        <Card className="mt-4 bg-blue-50 dark:bg-blue-950/20 border-blue-200 dark:border-blue-800">
          <CardContent className="pt-4">
            <h3 className="font-semibold mb-2">How to Use Grouping:</h3>
            <ul className="text-sm space-y-1 list-disc list-inside">
              <li>Add multiple filters using the "Add Filter" button</li>
              <li>Click "Group Filters" to enter grouping mode</li>
              <li>Use checkboxes to select 2 or more filters</li>
              <li>Click "Group Selected" to group the filters</li>
              <li>Groups can be nested up to 3 levels deep</li>
              <li>Each group has its own AND/OR operator</li>
              <li>Click "Ungroup" to move filters back to parent level</li>
              <li>
                Try the "Complex Nested Query" preset to see grouping in action
              </li>
            </ul>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Filter Builder</CardTitle>
          <CardDescription>
            Add filters to build complex queries. Try different property types
            and operators.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <FilterBuilder
            filterOptions={filterOptions}
            presets={presets}
            onChange={setFilterState}
            initialState={filterState}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>QueryKit String</CardTitle>
          <CardDescription>
            The generated QueryKit filter string to send to the backend
          </CardDescription>
        </CardHeader>
        <CardContent>
          {queryString ? (
            <div className="bg-muted p-4 rounded-md font-mono text-sm break-all">
              {queryString}
            </div>
          ) : (
            <div className="text-muted-foreground text-sm">
              No filters applied yet
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Filter State (JSON)</CardTitle>
          <CardDescription>
            The internal state representation of the filters
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="bg-muted p-4 rounded-md font-mono text-xs max-h-96 overflow-auto">
            <pre>{JSON.stringify(filterState, null, 2)}</pre>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
