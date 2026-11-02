import type {PublicClientApplication} from '@azure/msal-browser'

import {api, ApiError, type Schema} from '../api'
import {saveSession} from './session'
import {AuthError} from './types'

// Inloggning med skolans Microsoft-konto. MSAL skickar webbläsaren till Microsoft (auth code med PKCE) och tillbaka
// till /login. Där byts ID-tokenet en gång mot Lärkas eget token (POST /api/auth/microsoft), som sedan används
// precis som efter inloggning med lösenord. Inställningarna kommer från API:et, så ingen build behöver dem.
export type MicrosoftSettings = Schema<'MicrosoftProviderDto'>

const scopes = ['openid', 'profile', 'email']

let client: Promise<PublicClientApplication> | null = null
let clientKey = ''

function msal(settings: MicrosoftSettings): Promise<PublicClientApplication> {
  const key = `${settings.tenantId}/${settings.clientId}`
  if (!client || clientKey !== key) {
    clientKey = key
    client = (async () => {
      // MSAL laddas först när någon loggar in med Microsoft, så att den inte hamnar i resten av appen.
      const {PublicClientApplication} = await import('@azure/msal-browser')
      const app = new PublicClientApplication({
        auth: {
          clientId: settings.clientId,
          authority: `https://login.microsoftonline.com/${settings.tenantId}`,
          redirectUri: `${window.location.origin}/login`,
          navigateToLoginRequestUrl: false,
        },
        cache: {cacheLocation: 'sessionStorage'},
      })
      await app.initialize()
      return app
    })()
  }
  return client
}

/** Microsoft-inloggningens inställningar, eller null när skolan bara har lösenord. */
export async function fetchMicrosoftSettings(): Promise<MicrosoftSettings | null> {
  const {data} = await api.GET('/api/auth/providers')
  return data?.microsoft ?? null
}

/** Skickar webbläsaren till Microsoft. returnTo är sidan som ska öppnas efter inloggningen. */
export async function startMicrosoftSignIn(settings: MicrosoftSettings, returnTo: string): Promise<void> {
  const app = await msal(settings)
  await app.loginRedirect({scopes, state: returnTo, prompt: 'select_account'})
}

export interface MicrosoftSignInResult {
  firstSignIn: boolean
  returnTo: string
}

/**
 * Tar emot svaret när Microsoft skickar tillbaka till /login. Null när sidan inte öppnades av Microsoft.
 * Kastar AuthError med API:ets status när Lärka inte släpper in användaren.
 */
export async function finishMicrosoftSignIn(settings: MicrosoftSettings): Promise<MicrosoftSignInResult | null> {
  const app = await msal(settings)
  const response = await app.handleRedirectPromise()
  if (!response) {
    return null
  }

  try {
    const {data} = await api.POST('/api/auth/microsoft', {body: {idToken: response.idToken}})
    if (!data) {
      throw new AuthError(0)
    }
    saveSession(data.accessToken, Date.parse(data.expiresAt))
    return {firstSignIn: data.firstSignIn, returnTo: response.state ?? '/'}
  } catch (exception) {
    throw exception instanceof ApiError
      ? new AuthError(exception.status, exception.message)
      : exception
  }
}
