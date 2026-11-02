import {mkdir, readFile, writeFile} from 'node:fs/promises'

// Tokenen som global-setup hämtar och testerna använder, ett per roll. Filerna ligger i e2e/.auth och checkas aldrig in.
export interface StoredToken {
  accessToken: string
  expiresAt: string
}

// Demokontona. elev är standard, precis som förut.
export const roles = ['elev', 'larare', 'admin'] as const
export type Role = (typeof roles)[number]

// Samma nycklar som client/src/lib/auth/localAuth.ts använder i sessionStorage.
export const tokenStorageKey = 'larka.token'
export const expiryStorageKey = 'larka.expiresAt'

const authDir = new URL('./.auth/', import.meta.url)

function tokenFile(role: Role): URL {
  return new URL(`./.auth/token-${role}.json`, import.meta.url)
}

// Inloggningsuppgifterna för en roll: demokontots namn och lösenord, eller E2E_<ROLL>_USERNAME och E2E_<ROLL>_PASSWORD.
// För eleven gäller också de gamla E2E_USERNAME och E2E_PASSWORD.
export function credentialsFor(role: Role): {username: string, password: string} {
  const prefix = `E2E_${role.toUpperCase()}`
  const legacy = role === 'elev'
  return {
    username: process.env[`${prefix}_USERNAME`] ?? (legacy ? process.env.E2E_USERNAME : undefined) ?? role,
    password: process.env[`${prefix}_PASSWORD`] ?? (legacy ? process.env.E2E_PASSWORD : undefined) ?? role,
  }
}

export async function saveToken(role: Role, token: StoredToken): Promise<void> {
  await mkdir(authDir, {recursive: true})
  await writeFile(tokenFile(role), JSON.stringify(token))
}

export async function loadToken(role: Role): Promise<StoredToken> {
  return JSON.parse(await readFile(tokenFile(role), 'utf8')) as StoredToken
}
