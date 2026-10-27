import {afterEach, describe, expect, it, vi} from 'vitest'

import {api} from './api'
import {devHeaders, devUserId, setDevUser} from './devUser'

const fetchMock = vi.fn<(request: Request) => Promise<Response>>()

function respond(status: number, body: string) {
  fetchMock.mockResolvedValueOnce(
    new Response(body || null, {status, headers: {'Content-Type': 'application/json'}}),
  )
}

function sentRequest(): Request {
  return fetchMock.mock.calls[fetchMock.mock.calls.length - 1]![0]
}

describe('devUser', () => {
  afterEach(() => {
    setDevUser(null)
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
    fetchMock.mockReset()
  })

  it('sparar valet i localStorage och rensar det igen', () => {
    setDevUser('u2')
    expect(devUserId()).toBe('u2')
    expect(devHeaders()).toEqual({'X-Dev-User': 'u2'})

    setDevUser(null)
    expect(devUserId()).toBeNull()
    expect(devHeaders()).toEqual({})
  })

  it('skickar X-Dev-User med nästa API-anrop i utvecklingsläget', async () => {
    vi.stubEnv('DEV', true)
    vi.stubGlobal('fetch', fetchMock)
    setDevUser('u2')
    respond(200, '[]')

    await api.GET('/api/users')

    expect(sentRequest().headers.get('X-Dev-User')).toBe('u2')
  })

  it('skickar ingen header utan vald användare', async () => {
    vi.stubEnv('DEV', true)
    vi.stubGlobal('fetch', fetchMock)
    respond(200, '[]')

    await api.GET('/api/users')

    expect(sentRequest().headers.get('X-Dev-User')).toBeNull()
  })

  it('skickar ingen header i ett produktionsbygge', async () => {
    vi.stubEnv('DEV', false)
    vi.stubGlobal('fetch', fetchMock)
    setDevUser('u2')
    respond(200, '[]')

    await api.GET('/api/users')

    expect(sentRequest().headers.get('X-Dev-User')).toBeNull()
  })
})
