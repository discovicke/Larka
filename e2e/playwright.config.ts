import {defineConfig, devices} from '@playwright/test'

// Basadressen går att byta med E2E_BASE_URL, t.ex. mot den lokala dev-instansen.
const baseURL = process.env.E2E_BASE_URL ?? 'https://net25-devops-grupp-4.onrender.com'

// Testerna läser bara appen. De skapar, ändrar och tar aldrig bort något i produktion.
export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  timeout: 30_000,
  expect: {timeout: 10_000},
  // Render free tier kan svara långsamt vid kallstart, så CI får försöka igen.
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI
    ? [['list'], ['html', {open: 'never'}]]
    : 'list',
  use: {
    baseURL,
    trace: 'retain-on-failure',
  },
  // Både desktop och mobil: sidomenyn beter sig olika på smala skärmar.
  projects: [
    {name: 'chromium', use: {...devices['Desktop Chrome']}},
    {name: 'mobile', use: {...devices['Pixel 7']}},
  ],
})
