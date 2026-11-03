import {type APIRequestContext, type BrowserContext, type Page, test as base} from '@playwright/test'

import {expiryStorageKey, loadAdminToken, type StoredToken, tokenStorageKey} from './auth'

interface Fixtures {
  // Öppnar en ny, inloggad sida som admin eller som ett eget konto (token). Används för överlämningar mellan roller i
  // samma test, t.ex. läraren bedömer och eleven ser resultatet. Sidorna stängs när testet är klart.
  // label namnger sidans video i rapporten, t.ex. 'lärare' eller 'elev'.
  pageAs: (who: 'admin' | StoredToken, label?: string) => Promise<Page>
  // API-anrop som admin eller ett eget konto. Används för att förbereda data som testet inte handlar om.
  apiAs: (who: 'admin' | StoredToken) => Promise<APIRequestContext>
}

async function tokenOf(who: 'admin' | StoredToken): Promise<StoredToken> {
  return who === 'admin'
    ? loadAdminToken()
    : who
}

// Samma sak som localAuth gör efter inloggningen: token och utgångstid i sessionStorage, innan appen startar.
async function signInPage(page: Page, token: StoredToken) {
  await page.addInitScript(
    ({tokenKey, expiryKey, accessToken, expiry}) => {
      sessionStorage.setItem(tokenKey, accessToken)
      sessionStorage.setItem(expiryKey, expiry)
    },
    {
      tokenKey: tokenStorageKey,
      expiryKey: expiryStorageKey,
      accessToken: token.accessToken,
      expiry: String(Date.parse(token.expiresAt)),
    },
  )
}

export const test = base.extend<Fixtures>({
  pageAs: async ({browser, baseURL, viewport, isMobile, hasTouch, video}, use, testInfo) => {
    // Playwright spelar bara in video för sin egen page. Sidorna här skapas med browser.newContext, så de spelas in
    // här med samma inställning (config: video) och läggs till i rapporten. Trace och skärmbilder tar Playwright själv.
    const mode = typeof video === 'string' ? video : video.mode
    const record = mode !== 'off'
    const contexts: BrowserContext[] = []
    const pages: {page: Page; label: string}[] = []
    await use(async (who, label) => {
      const context = await browser.newContext({
        baseURL,
        viewport,
        isMobile,
        hasTouch,
        ...(record ? {recordVideo: {dir: testInfo.outputPath('videos'), size: viewport ?? undefined}} : {}),
      })
      contexts.push(context)
      const page = await context.newPage()
      await signInPage(page, await tokenOf(who))
      pages.push({page, label: label ?? (who === 'admin' ? 'admin' : 'konto')})
      return page
    })
    for (const context of contexts) {
      await context.close()
    }
    const failed = testInfo.status !== testInfo.expectedStatus
    const keep = mode === 'on' || (failed && (mode === 'retain-on-failure' || mode === 'on-first-retry'))
    if (!keep) {
      return
    }
    for (const [i, {page, label}] of pages.entries()) {
      const path = await page.video()?.path()
      if (path) {
        await testInfo.attach(`video ${i + 1} (${label})`, {path, contentType: 'video/webm'})
      }
    }
  },

  apiAs: async ({playwright, baseURL}, use) => {
    const contexts: {dispose: () => Promise<void>}[] = []
    await use(async (who) => {
      const context = await playwright.request.newContext({
        baseURL,
        extraHTTPHeaders: {Authorization: `Bearer ${(await tokenOf(who)).accessToken}`},
      })
      contexts.push(context)
      return context
    })
    for (const context of contexts) {
      await context.dispose()
    }
  },
})

// Testerna skapar och ändrar data, så de vägrar köra mot Render-adressen eller något annat som liknar produktion.
export function assertNotProduction(baseURL: string | undefined) {
  if (!baseURL || /onrender\.com/i.test(baseURL)) {
    throw new Error(`E2E får aldrig köras mot produktion (${baseURL ?? 'ingen adress'}). Starta en lokal stack.`)
  }
}

export {expect} from '@playwright/test'
