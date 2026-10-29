import { describe, expect, it } from 'vitest'
import { checkStack, type DescribeRun } from './stack-description.ts'

const appHost = '../FullstackTemplate.AppHost'
const startCommand = 'dotnet aspire start --isolated --non-interactive --apphost ../FullstackTemplate.AppHost'

function resource(displayName: string, overrides: Record<string, unknown> = {}) {
  return { displayName, state: 'Running', healthStatus: 'Healthy', urls: [], environment: {}, ...overrides }
}

const healthyResources = [
  resource('server', { urls: [{ name: 'https', url: 'https://localhost:7001' }] }),
  resource('bff', { environment: { Auth__Authority: 'http://localhost:24730/realms/app' } }),
  resource('webfrontend', { urls: [{ name: 'http', url: 'http://localhost:24710' }] }),
  resource('webfrontend-installer', { state: 'Finished', healthStatus: null }),
]

function described(resources: unknown[]): DescribeRun {
  return { status: 0, stdout: JSON.stringify({ resources }), error: undefined }
}

function messageOf(run: DescribeRun): string {
  const check = checkStack({ run, appHost })
  if (check.kind !== 'not-ready') {
    throw new Error(`Expected a not-ready stack, got ${check.kind}`)
  }
  return check.message
}

describe('checkStack', () => {
  it('tells the user to start the stack when describe writes nothing and exits 0', () => {
    const message = messageOf({ status: 0, stdout: '', error: undefined })

    expect(message).toContain('No Aspire stack runs for this checkout.')
    expect(message).toContain(startCommand)
  })

  it('treats output of only white space as no stack', () => {
    expect(messageOf({ status: 0, stdout: '\n  \n', error: undefined })).toContain(startCommand)
  })

  it('reports output that is not JSON', () => {
    const message = messageOf({ status: 0, stdout: 'Finding AppHosts...', error: undefined })

    expect(message).toContain('dotnet aspire describe gave output that the script cannot read')
    expect(message).toContain('dotnet aspire describe --format Json --apphost ../FullstackTemplate.AppHost')
  })

  it('reports JSON that has no list of resources', () => {
    const message = messageOf({ status: 0, stdout: '{"apphost":"x"}', error: undefined })

    expect(message).toContain('dotnet aspire describe gave output that the script cannot read')
    expect(message).toContain('dotnet aspire describe --format Json --apphost ../FullstackTemplate.AppHost')
  })

  it('reports the exit code and the output when describe fails', () => {
    const message = messageOf({ status: 1, stdout: 'Possible reasons for this include:\n', error: undefined })

    expect(message).toContain('exit code 1')
    expect(message).toContain('Possible reasons for this include:')
  })

  it('reports when dotnet cannot run', () => {
    const message = messageOf({ status: null, stdout: '', error: new Error('spawnSync dotnet ENOENT') })

    expect(message).toContain('spawnSync dotnet ENOENT')
  })

  it('names a resource that the stack tests need and the stack does not have', () => {
    const message = messageOf(described(healthyResources.filter((r) => r.displayName !== 'bff')))

    expect(message).toContain('The stack has no bff resource.')
    expect(message).toContain(startCommand)
  })

  it('names each needed resource that the stack does not have', () => {
    const message = messageOf(described([resource('server')]))

    expect(message).toContain('The stack has no bff or webfrontend resource.')
  })

  it('names each needed resource that is not healthy and gives the wait command', () => {
    const message = messageOf(
      described([
        resource('server', { healthStatus: 'Unhealthy' }),
        resource('bff'),
        resource('webfrontend', { state: 'Starting', healthStatus: null }),
      ]),
    )

    expect(message).toContain('server (state Running, health Unhealthy)')
    expect(message).toContain('webfrontend (state Starting, health unknown)')
    expect(message).not.toContain('bff (')
    expect(message).toContain(
      'for resource in server webfrontend; do dotnet aspire wait "$resource" --status healthy --timeout 900 --apphost ../FullstackTemplate.AppHost; done',
    )
  })

  it('gives the resources of a healthy stack', () => {
    const check = checkStack({ run: described(healthyResources), appHost })

    expect(check).toEqual({ kind: 'ready', resources: healthyResources })
  })
})
