import {mkdir, readFile, writeFile} from 'node:fs/promises'

// Tokenet som global-setup hämtar och testerna använder. Filen ligger i e2e/.auth och checkas aldrig in.
export interface StoredToken {
  accessToken: string
  expiresAt: string
}

// Samma nycklar som client/src/lib/auth/localAuth.ts använder i sessionStorage.
export const tokenStorageKey = 'larka.token'
export const expiryStorageKey = 'larka.expiresAt'

const authDir = new URL('./.auth/', import.meta.url)
const tokenFile = new URL('./.auth/token.json', import.meta.url)

export async function saveToken(token: StoredToken): Promise<void> {
  await mkdir(authDir, {recursive: true})
  await writeFile(tokenFile, JSON.stringify(token))
}

export async function loadToken(): Promise<StoredToken> {
  return JSON.parse(await readFile(tokenFile, 'utf8')) as StoredToken
}
