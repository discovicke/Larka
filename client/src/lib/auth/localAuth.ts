import {api, ApiError} from '../api'
import {clearSession, currentToken, saveSession} from './session'
import {AuthError, type AuthProvider} from './types'

export {AUTH_EXPIRY_KEY, AUTH_TOKEN_KEY} from './session'

async function signIn(credentials: {username: string, password: string}): Promise<void> {
  try {
    const {data} = await api.POST('/api/auth/login', {body: credentials})
    if (!data) {
      throw new AuthError(0)
    }
    saveSession(data.accessToken, Date.parse(data.expiresAt))
  } catch (exception) {
    // api-klienten kastar ApiError för svar som inte är 2xx. Vyn vill ha AuthError med samma status.
    throw exception instanceof ApiError
      ? new AuthError(exception.status)
      : exception
  }
}

async function signOut(): Promise<void> {
  clearSession()
}

export const localAuth: AuthProvider = {
  signIn,
  signOut,
  accessToken: currentToken,
  isSignedIn: () => currentToken() !== null,
}
