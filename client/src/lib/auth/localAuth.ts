import {AuthError, type AuthProvider} from './types'

export const AUTH_TOKEN_KEY = 'rebornlms.token'
export const AUTH_EXPIRY_KEY = 'rebornlms.expiresAt'

// Sessionen finns både i minnet och i sessionStorage: en omladdning behåller den, men en ny flik börjar
// utan. Storage läses först, så att testerna kan sätta en session direkt.
let memoryToken: string | null = null
let memoryExpiresAt = 0

function readStorage(key: string): string | null {
  try {
    return sessionStorage.getItem(key)
  } catch {
    // Session storage kan vara avstängt (t.ex. i en inbäddad webview). Då räcker minnet.
    return null
  }
}

function writeStorage(key: string, value: string | null): void {
  try {
    if (value === null) {
      sessionStorage.removeItem(key)
    } else {
      sessionStorage.setItem(key, value)
    }
  } catch {
    // Se readStorage: sessionen finns kvar i minnet.
  }
}

function currentToken(): string | null {
  const token = readStorage(AUTH_TOKEN_KEY) ?? memoryToken
  const expiresAt = Number(readStorage(AUTH_EXPIRY_KEY) ?? memoryExpiresAt)
  return token && Date.now() < expiresAt
    ? token
    : null
}

async function signIn(credentials: {username: string, password: string}): Promise<void> {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify(credentials),
  })
  if (!response.ok) {
    throw new AuthError(response.status)
  }

  const result = await response.json() as {accessToken: string, expiresAt: string}
  const expiresAt = Date.parse(result.expiresAt)
  memoryToken = result.accessToken
  memoryExpiresAt = expiresAt
  writeStorage(AUTH_TOKEN_KEY, result.accessToken)
  writeStorage(AUTH_EXPIRY_KEY, String(expiresAt))
}

async function signOut(): Promise<void> {
  memoryToken = null
  memoryExpiresAt = 0
  writeStorage(AUTH_TOKEN_KEY, null)
  writeStorage(AUTH_EXPIRY_KEY, null)
}

export const localAuth: AuthProvider = {
  signIn,
  signOut,
  accessToken: currentToken,
  isSignedIn: () => currentToken() !== null,
}
