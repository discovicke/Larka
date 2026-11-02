import {type APIRequestContext, type Page, test as base} from '@playwright/test'

import {expiryStorageKey, loadToken, type Role, type StoredToken, tokenStorageKey} from './auth'

interface Options {
  // Rollen som sidan är inloggad som. Eleven om inget annat anges.
  role: Role
  // false för ett test som ska se appen utan inloggning.
  signedIn: boolean
}

interface Fixtures {
  // Öppnar en ny, inloggad sida för en annan roll eller ett eget konto (token). Används för överlämningar mellan roller i
  // samma test, t.ex. läraren bedömer och eleven ser resultatet. Sidorna stängs när testet är klart.
  pageAs: (who: Role | StoredToken) => Promise<Page>
  // API-anrop som en roll eller ett eget konto. Används för att förbereda data i journeys.
  apiAs: (who: Role | StoredToken) => Promise<APIRequestContext>
  // Stoppar journeys från att köras mot fel miljö.
  journeyGuard: void
}

async function tokenOf(who: Role | StoredToken): Promise<StoredToken> {
  return typeof who === 'string'
    ? loadToken(who)
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

export const test = base.extend<Options & Fixtures>({
  role: ['elev', {option: true}],
  signedIn: [true, {option: true}],

  page: async ({page, signedIn, role}, use) => {
    if (signedIn) {
      await signInPage(page, await loadToken(role))
    }
    await use(page)
  },

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

  // Anrop till API:et skickar tokenen som Bearer, så att de prövas som en inloggad användare.
  request: async ({playwright, baseURL, signedIn, role}, use) => {
    const headers: Record<string, string> = signedIn
      ? {Authorization: `Bearer ${(await loadToken(role)).accessToken}`}
      : {}
    const context = await playwright.request.newContext({baseURL, extraHTTPHeaders: headers})
    await use(context)
    await context.dispose()
  },

  // Journeys skriver data. De körs bara när E2E_JOURNEYS=1 och aldrig mot den deployade produktionen.
  journeyGuard: [
    async ({baseURL}, use, testInfo) => {
      if (testInfo.file.includes('/journeys/')) {
        if (process.env.E2E_JOURNEYS !== '1') {
          throw new Error('Journeys skriver data och kräver E2E_JOURNEYS=1.')
        }
        assertNotProduction(baseURL)
      }
      await use()
    },
    {auto: true},
  ],
})

// Journeys skapar och ändrar data, så de vägrar köra mot Render-adressen eller något annat som liknar produktion.
export function assertNotProduction(baseURL: string | undefined) {
  if (!baseURL || /onrender\.com/i.test(baseURL)) {
    throw new Error(`Journeys får aldrig köras mot produktion (${baseURL ?? 'ingen adress'}). Starta en lokal stack.`)
  }
}

export {expect} from '@playwright/test'
