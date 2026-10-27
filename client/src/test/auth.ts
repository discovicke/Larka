import {auth} from '@/lib/auth'
import {AUTH_EXPIRY_KEY, AUTH_TOKEN_KEY} from '@/lib/auth/localAuth'

/** Loggar in en låtsasanvändare i testerna, så att skyddade sidor kan renderas utan ett riktigt backend. */
export function signInForTests(): void {
  sessionStorage.setItem(AUTH_TOKEN_KEY, 'test-token')
  sessionStorage.setItem(AUTH_EXPIRY_KEY, String(Date.now() + 60 * 60 * 1000))
}

/** Loggar ut inför ett test som ska pröva den utloggade vägen. */
export function signOutForTests(): void {
  sessionStorage.clear()
  void auth.signOut()
}
