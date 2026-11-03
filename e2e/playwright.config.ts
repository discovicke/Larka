import {defineConfig, devices} from '@playwright/test'

import {assertNotProduction} from './fixtures'

// Tre användarflöden (tests/) mot en färsk lokal stack (docker-compose.e2e.yml). Testerna skriver data, så de körs
// aldrig mot produktion. Basadressen går att byta med E2E_BASE_URL, men konfigurationen vägrar Render-adressen.
const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080'
assertNotProduction(baseURL)

export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  timeout: 60_000,
  expect: {timeout: 10_000},
  // Testerna delar en databas och körs i tur och ordning.
  fullyParallel: false,
  workers: 1,
  // Varje test skapar sin egen data med unika namn, så ett nytt försök i CI krockar inte med det första.
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI
    ? [['list'], ['html', {open: 'never'}]]
    : 'list',
  use: {
    baseURL,
    // Klienten använder data-test, inte Playwrights standard data-testid.
    testIdAttribute: 'data-test',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome'],
  },
})
