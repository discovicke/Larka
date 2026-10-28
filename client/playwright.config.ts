import {defineConfig, devices} from '@playwright/test'

// Layoutkontroller i en riktig webbläsare, mot `vite preview` av det byggda klientpaketet.
// API:et är mockat i testerna (layout/mocks.ts), så varken backend eller databas behövs.
const port = Number(process.env.LAYOUT_PORT ?? 4173)

export default defineConfig({
  testDir: './layout',
  testMatch: '*.spec.ts',
  outputDir: './layout/test-results',
  timeout: 30_000,
  expect: {timeout: 10_000},
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI
    ? 1
    : 0,
  reporter: process.env.CI
    ? [['list'], ['html', {open: 'never', outputFolder: 'layout/playwright-report'}]]
    : 'list',
  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'retain-on-failure',
  },
  projects: [
    {name: 'chromium', use: {...devices['Desktop Chrome']}},
  ],
  webServer: {
    // Bygget görs av `npm run build` (test:layout och CI gör det först).
    command: `npx vite preview --port ${port} --strictPort`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env.CI,
  },
})
