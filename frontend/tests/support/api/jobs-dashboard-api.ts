import type { MockRoute } from './mock-route'

export const jobsDashboardPath = '/backwave'

export const jobsDashboardApi = {
  /** The jobs dashboard is a page of the Server. The mock answers with an empty page. */
  opens(): MockRoute {
    return { method: 'get', path: jobsDashboardPath, status: 200 }
  },
}
