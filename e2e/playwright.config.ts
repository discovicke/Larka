import {defineConfig, devices} from '@playwright/test'

import {assertNotProduction} from './fixtures'

// Basadressen går att byta med E2E_BASE_URL, t.ex. mot den lokala dev-instansen.
const baseURL = process.env.E2E_BASE_URL ?? 'https://net25-devops-grupp-4.onrender.com'

// Två sviter i samma uppsättning:
// - smoke läser bara appen och körs mot produktion varje dag (e2e.yml).
// - journeys går igenom användarflödena och skriver data. De körs bara när E2E_JOURNEYS=1, mot en färsk lokal
//   stack (e2e-journeys.yml), och vägrar produktionsadressen.
const journeys = process.env.E2E_JOURNEYS === '1'
if (journeys) {
  assertNotProduction(baseURL)
}

export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  timeout: journeys ? 60_000 : 30_000,
  expect: {timeout: 10_000},
  // Journeys delar en databas och körs i tur och ordning.
  fullyParallel: !journeys,
  workers: journeys ? 1 : undefined,
  // Render free tier kan svara långsamt vid kallstart, så CI får försöka igen. Journeys skriver data och körs om
  // bara en gång, eftersom varje test skapar sin egen data med unika namn.
  retries: process.env.CI ? (journeys ? 1 : 2) : 0,
  reporter: process.env.CI
    ? [['list'], ['html', {open: 'never'}]]
    : 'list',
  use: {
    baseURL,
    // Klienten använder data-test, inte Playwrights standard data-testid.
    testIdAttribute: 'data-test',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    // Både desktop och mobil: sidomenyn beter sig olika på smala skärmar.
    {name: 'chromium', testMatch: 'smoke.spec.ts', use: {...devices['Desktop Chrome']}},
    {name: 'mobile', testMatch: 'smoke.spec.ts', use: {...devices['Pixel 7']}},
    ...(journeys
      ? [
          {name: 'journeys', testMatch: 'journeys/**/*.spec.ts', use: {...devices['Desktop Chrome']}},
          // Bara E1 körs också på mobil: Idag och Kalender ser olika ut på smala skärmar.
          {name: 'journeys-mobile', testMatch: 'journeys/e1-*.spec.ts', use: {...devices['Pixel 7']}},
        ]
      : []),
  ],
})
