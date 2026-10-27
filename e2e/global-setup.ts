import type {FullConfig} from '@playwright/test'

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
