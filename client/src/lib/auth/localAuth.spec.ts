import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import {AuthError} from './types'
import {AUTH_EXPIRY_KEY, AUTH_TOKEN_KEY, localAuth} from './localAuth'

const fetchMock = vi.fn<(input: string, init?: RequestInit) => Promise<Response>>()

function respond(status: number, body: string): void {
  fetchMock.mockResolvedValueOnce(new Response(body || null, {
    status,
    headers: {'Content-Type': 'application/json'},
  }))
}

function loginResponse(expiresAt: string, accessToken = 'abc') {
  return JSON.stringify({accessToken, expiresAt})
}

describe('localAuth', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    void localAuth.signOut()
  })

  afterEach(() => {
    fetchMock.mockReset()
    vi.unstubAllGlobals()
    sessionStorage.clear()
  })

  it('sparar token och utgångstid i sessionStorage vid inloggning', async () => {
    const expiresAt = new Date(Date.now() + 60_000).toISOString()
    respond(200, loginResponse(expiresAt))

    await localAuth.signIn({username: 'larare', password: 'larare'})

    expect(localAuth.accessToken()).toBe('abc')
    expect(localAuth.isSignedIn()).toBe(true)
    expect(sessionStorage.getItem(AUTH_TOKEN_KEY)).toBe('abc')
    expect(sessionStorage.getItem(AUTH_EXPIRY_KEY)).toBe(String(Date.parse(expiresAt)))

    const [url, init] = fetchMock.mock.calls[0]!
    expect(new URL(url, 'http://localhost').pathname).toBe('/api/auth/login')
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual({
      username: 'larare',
      password: 'larare',
    })
  })

  it('accessToken är null efter utgångstiden', async () => {
    respond(200, loginResponse(new Date(Date.now() - 1_000).toISOString()))

    await localAuth.signIn({username: 'elev', password: 'elev'})

    expect(localAuth.accessToken()).toBeNull()
    expect(localAuth.isSignedIn()).toBe(false)
  })

  it('signOut rensar sessionen', async () => {
    respond(200, loginResponse(new Date(Date.now() + 60_000).toISOString()))
    await localAuth.signIn({username: 'elev', password: 'elev'})

    await localAuth.signOut()

    expect(localAuth.accessToken()).toBeNull()
    expect(localAuth.isSignedIn()).toBe(false)
    expect(sessionStorage.getItem(AUTH_TOKEN_KEY)).toBeNull()
    expect(sessionStorage.getItem(AUTH_EXPIRY_KEY)).toBeNull()
  })

  it('kastar AuthError med status när inloggningen misslyckas', async () => {
    respond(401, JSON.stringify({detail: 'Fel e-post eller lösenord.'}))

    const error = await localAuth.signIn({username: 'elev', password: 'fel'})
      .catch((thrown: unknown) => thrown)

    expect(error).toBeInstanceOf(AuthError)
    expect((error as AuthError).status).toBe(401)
  })
})
