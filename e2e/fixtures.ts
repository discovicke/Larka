import {type APIRequestContext, type Page, test as base} from '@playwright/test'

import {expiryStorageKey, loadAdminToken, type StoredToken, tokenStorageKey} from './auth'

interface Fixtures {
  // Öppnar en ny, inloggad sida som admin eller som ett eget konto (token). Används för överlämningar mellan roller i
  // samma test, t.ex. läraren bedömer och eleven ser resultatet. Sidorna stängs när testet är klart.
  pageAs: (who: 'admin' | StoredToken) => Promise<Page>
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
  pageAs: async ({browser, baseURL, viewport, isMobile, hasTouch}, use) => {
    const contexts: {close: () => Promise<void>}[] = []
    await use(async (who) => {
      const context = await browser.newContext({baseURL, viewport, isMobile, hasTouch})
      contexts.push(context)
      const page = await context.newPage()
      await signInPage(page, await tokenOf(who))
      return page
    })
    for (const context of contexts) {
      await context.close()
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
