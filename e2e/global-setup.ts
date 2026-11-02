import type {FullConfig} from '@playwright/test'

import {credentialsFor, roles, saveToken, type Role} from './auth'

const HEALTH_TIMEOUT_MS = 3 * 60 * 1000
const RETRY_DELAY_MS = 5_000

function sleep(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

// Render free tier somnar efter 15 minuter utan trafik och första anropet kan ta en minut.
// Vänta tills appen svarar på /health innan testerna börjar, annars faller de på kallstarten.
export default async function globalSetup(config: FullConfig) {
  const baseURL = config.projects[0]?.use.baseURL
  if (!baseURL) {
    throw new Error('E2E_BASE_URL saknas')
  }

  await waitForHealth(baseURL)
  for (const role of roles) {
    await signIn(baseURL, role)
  }
}

async function waitForHealth(baseURL: string) {
  const healthUrl = `${baseURL}/health`
  const deadline = Date.now() + HEALTH_TIMEOUT_MS
  let lastError = 'inget svar'

  while (Date.now() < deadline) {
    try {
      const response = await fetch(healthUrl)
      if (response.status === 200) {
        return
      }
      lastError = `HTTP ${response.status}`
    } catch (error) {
      lastError = error instanceof Error
        ? error.message
        : String(error)
    }
    await sleep(RETRY_DELAY_MS)
  }

  throw new Error(`Appen svarade inte på /health inom 3 minuter (${healthUrl}, senast: ${lastError})`)
}

// Loggar in som varje roll med samma endpoint som klienten, och sparar tokenen för testerna. Lösenordet loggas aldrig.
// Kontona är demokontona elev, larare och admin, eller E2E_<ROLL>_USERNAME och E2E_<ROLL>_PASSWORD.
async function signIn(baseURL: string, role: Role) {
  const {username, password} = credentialsFor(role)
  const response = await fetch(`${baseURL}/api/auth/login`, {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({username, password}),
  })
  if (!response.ok) {
    throw new Error(`Inloggningen som ${role} misslyckades (HTTP ${response.status}). Kontrollera E2E_${role.toUpperCase()}_USERNAME och E2E_${role.toUpperCase()}_PASSWORD.`)
  }

  const result = await response.json() as {accessToken: string, expiresAt: string}
  await saveToken(role, {accessToken: result.accessToken, expiresAt: result.expiresAt})
}
