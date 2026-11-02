import {beforeEach, describe, expect, it, vi} from 'vitest'

import {mockApi, sent} from '@/test/mockApi'

import {finishMicrosoftSignIn, startMicrosoftSignIn} from './microsoft'
import {clearSession, currentToken} from './session'
import {AuthError} from './types'

const msal = vi.hoisted(() => ({
  config: null as unknown,
  loginRedirect: vi.fn(),
  handleRedirectPromise: vi.fn(),
}))

vi.mock('@azure/msal-browser', () => ({
  PublicClientApplication: class {
    constructor(config: unknown) {
      msal.config = config
    }

    initialize = vi.fn(async () => {})
    loginRedirect = msal.loginRedirect
    handleRedirectPromise = msal.handleRedirectPromise
  },
}))

const settings = {clientId: 'client-1', tenantId: 'tenant-1'}

beforeEach(() => {
  msal.loginRedirect.mockReset()
  msal.handleRedirectPromise.mockReset()
  clearSession()
})

describe('Microsoft-inloggning', () => {
  it('skickar till Microsoft med skolans tenant, tillbaka till /login och med sidan i state', async () => {
    await startMicrosoftSignIn(settings, '/courses/c1/content')

    expect(msal.config).toMatchObject({
      auth: {
        clientId: 'client-1',
        authority: 'https://login.microsoftonline.com/tenant-1',
        redirectUri: `${window.location.origin}/login`,
      },
    })
    expect(msal.loginRedirect).toHaveBeenCalledWith(expect.objectContaining({
      scopes: ['openid', 'profile', 'email'],
      state: '/courses/c1/content',
    }))
  })

  it('byter ID-tokenet mot Lärkas token och sparar sessionen', async () => {
    msal.handleRedirectPromise.mockResolvedValue({idToken: 'ms-id-token', state: '/inbox'})
    mockApi(() => ({
      status: 200,
      body: {accessToken: 'larka-token', expiresAt: new Date(Date.now() + 60_000).toISOString(), firstSignIn: true},
    }))

    const result = await finishMicrosoftSignIn(settings)

    expect(sent('POST', '/api/auth/microsoft')).toEqual({idToken: 'ms-id-token'})
    expect(result).toEqual({firstSignIn: true, returnTo: '/inbox'})
    expect(currentToken()).toBe('larka-token')
  })

  it('gör ingenting när sidan inte öppnades av Microsoft', async () => {
    msal.handleRedirectPromise.mockResolvedValue(null)
    mockApi(() => ({status: 500}))

    expect(await finishMicrosoftSignIn(settings)).toBeNull()
    expect(sent('POST', '/api/auth/microsoft')).toBeUndefined()
  })

  it('kastar AuthError med API:ets text när Lärka säger nej', async () => {
    msal.handleRedirectPromise.mockResolvedValue({idToken: 'ms-id-token', state: '/'})
    mockApi(() => ({status: 403, body: {detail: 'Ditt konto är spärrat.'}}))

    const error = await finishMicrosoftSignIn(settings).catch((exception: unknown) => exception)

    expect(error).toBeInstanceOf(AuthError)
    expect((error as AuthError).status).toBe(403)
    expect((error as AuthError).detail).toBe('Ditt konto är spärrat.')
    expect(currentToken()).toBeNull()
  })
})
