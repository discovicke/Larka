export const AUTH_TOKEN_KEY = 'rebornlms.token'
export const AUTH_EXPIRY_KEY = 'rebornlms.expiresAt'

// Sessionen ligger i en egen modul som både api.ts och localAuth.ts importerar. Då slipper vi en
// importcykel (api -> auth -> localAuth -> api): localAuth anropar inloggningen via api-klienten.
//
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

/** Tokenet om sessionen finns och inte gått ut, annars null. */
export function currentToken(): string | null {
  const token = readStorage(AUTH_TOKEN_KEY) ?? memoryToken
  const expiresAt = Number(readStorage(AUTH_EXPIRY_KEY) ?? memoryExpiresAt)
  return token && Date.now() < expiresAt
    ? token
    : null
}

export function saveSession(accessToken: string, expiresAt: number): void {
  memoryToken = accessToken
  memoryExpiresAt = expiresAt
  writeStorage(AUTH_TOKEN_KEY, accessToken)
  writeStorage(AUTH_EXPIRY_KEY, String(expiresAt))
}

export function clearSession(): void {
  memoryToken = null
  memoryExpiresAt = 0
  writeStorage(AUTH_TOKEN_KEY, null)
  writeStorage(AUTH_EXPIRY_KEY, null)
}
