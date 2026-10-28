import {mkdirSync} from 'node:fs'
import {join} from 'node:path'

import {test} from '@playwright/test'

import {routes, signInAndMockApi} from './mocks'

// Sparar helsidesbilder av sidorna vid 390 och 1440 px, t.ex. till ett galleri i en PR. Körs bara med SCREENSHOTS=1:
//   SCREENSHOTS=1 npm run test:layout -- screenshots
// SCREENSHOT_DIR byter målmapp (standard layout/screenshots) och SCREENSHOT_ROUTES en kommaseparerad lista med
// sökvägar (standard: sidorna i layout/mocks.ts).
const outDir = process.env.SCREENSHOT_DIR ?? 'layout/screenshots'
const paths = process.env.SCREENSHOT_ROUTES?.split(',').map((path) => path.trim()).filter(Boolean)
  ?? Object.values(routes)
const widths = [390, 1440]

test.skip(!process.env.SCREENSHOTS, 'Sätt SCREENSHOTS=1 för att spara bilder')

test.beforeEach(async ({page}) => {
  await signInAndMockApi(page)
})

for (const width of widths) {
  for (const path of paths) {
    test(`${path} vid ${width} px`, async ({page}) => {
      await page.setViewportSize({width, height: 900})
      await page.goto(path)
      await page.waitForLoadState('networkidle')
      mkdirSync(outDir, {recursive: true})
      const name = path === '/'
        ? 'idag'
        : path.replace(/^\//, '').replace(/[^a-z0-9]+/gi, '-')
      await page.screenshot({path: join(outDir, `${name}-${width}.png`), fullPage: true})
    })
  }
}
