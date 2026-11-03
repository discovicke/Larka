import {mkdir, readFile, writeFile} from 'node:fs/promises'

// Admin-tokenet som global-setup hämtar och testerna använder. Filen ligger i e2e/.auth och checkas aldrig in.
// Övriga konton (lärare, elever) skapar varje test själv och loggar in med login() i journeys.ts.
export interface StoredToken {
  accessToken: string
  expiresAt: string
}

// Samma nycklar som client/src/lib/auth/localAuth.ts använder i sessionStorage.
export const tokenStorageKey = 'larka.token'
export const expiryStorageKey = 'larka.expiresAt'

const authDir = new URL('./.auth/', import.meta.url)
const adminTokenFile = new URL('./.auth/token-admin.json', import.meta.url)

export async function saveAdminToken(token: StoredToken): Promise<void> {
  await mkdir(authDir, {recursive: true})
  await writeFile(adminTokenFile, JSON.stringify(token))
}

export async function loadAdminToken(): Promise<StoredToken> {
  return JSON.parse(await readFile(adminTokenFile, 'utf8')) as StoredToken
}
