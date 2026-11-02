// Inloggningen med lösenord bakom ett interface. Microsoft-inloggningen (microsoft.ts) går via en redirect och har
// egna funktioner, men sparar samma session, så resten av appen ser ingen skillnad.
export interface AuthProvider {
  signIn(credentials: {username: string, password: string}): Promise<void>
  signOut(): Promise<void>
  accessToken(): string | null
  isSignedIn(): boolean
}

/**
 * Fel från inloggningen, med HTTP-status så att vyn kan skilja fel lösenord från andra fel. detail är API:ets text,
 * till exempel varför ett 403 nekades.
 */
export class AuthError extends Error {
  readonly status: number
  readonly detail: string

  constructor(status: number, detail = '') {
    super(`Inloggningen misslyckades (${status}).`)
    this.name = 'AuthError'
    this.status = status
    this.detail = detail
  }
}
