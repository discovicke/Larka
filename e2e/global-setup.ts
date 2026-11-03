import type {FullConfig} from '@playwright/test'

import {saveAdminToken} from './auth'

const HEALTH_TIMEOUT_MS = 3 * 60 * 1000
const RETRY_DELAY_MS = 2_000

function sleep(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

// Vänta tills stacken svarar på /health, och logga sedan in som demokontot admin. Det är det enda konto testerna
// behöver från start: admin skapar lärare och elever med unika namn i varje test.
export default async function globalSetup(config: FullConfig) {
  const baseURL = config.projects[0]?.use.baseURL
  if (!baseURL) {
    throw new Error('E2E_BASE_URL saknas')
  }

  await waitForHealth(baseURL)
  await signInAsAdmin(baseURL)
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

  throw new Error(`Appen svarade inte på /health inom 3 minuter (${healthUrl}, senast: ${lastError}). Är stacken startad?`)
}

async function signInAsAdmin(baseURL: string) {
  const response = await fetch(`${baseURL}/api/auth/login`, {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({username: 'admin', password: 'admin'}),
  })
  if (!response.ok) {
    throw new Error(`Inloggningen som admin misslyckades (HTTP ${response.status}). Startades stacken med demodata?`)
  }

  const result = await response.json() as {accessToken: string, expiresAt: string}
  await saveAdminToken({accessToken: result.accessToken, expiresAt: result.expiresAt})
}
