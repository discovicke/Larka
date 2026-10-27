// Inloggningen bakom ett interface, så att Microsoft-inloggning (#80) kan bli en andra implementation.
// Den har en annan signIn (redirect till Microsoft), så håll interfacet minimalt.
export interface AuthProvider {
  signIn(credentials: {username: string, password: string}): Promise<void>
  signOut(): Promise<void>
  accessToken(): string | null
  isSignedIn(): boolean
}

/** Fel från inloggningen, med HTTP-status så att vyn kan skilja fel lösenord från andra fel. */
export class AuthError extends Error {
  readonly status: number

  constructor(status: number) {
    super(`Inloggningen misslyckades (${status}).`)
    this.name = 'AuthError'
    this.status = status
  }
}
