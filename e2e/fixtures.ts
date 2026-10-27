import {test as base} from '@playwright/test'

import {expiryStorageKey, loadToken, tokenStorageKey} from './auth'

// Testerna är inloggade som eleven (E2E_USERNAME och E2E_PASSWORD), precis som i klienten. Sätt signedIn: false
// för ett test som ska se appen utan inloggning.
export const test = base.extend<{signedIn: boolean}>({
  signedIn: [true, {option: true}],

  page: async ({page, signedIn}, use) => {
    if (signedIn) {
      const {accessToken, expiresAt} = await loadToken()
      // Samma sak som localAuth gör efter inloggningen: token och utgångstid i sessionStorage, innan appen startar.
      await page.addInitScript(
        ({tokenKey, expiryKey, token, expiry}) => {
          sessionStorage.setItem(tokenKey, token)
          sessionStorage.setItem(expiryKey, expiry)
        },
        {
          tokenKey: tokenStorageKey,
          expiryKey: expiryStorageKey,
          token: accessToken,
          expiry: String(Date.parse(expiresAt)),
        },
      )
    }
    await use(page)
  },

  // Anrop till API:et skickar tokenet som Bearer, så att de prövas som en inloggad användare.
  request: async ({playwright, baseURL, signedIn}, use) => {
    const headers: Record<string, string> = signedIn
      ? {Authorization: `Bearer ${(await loadToken()).accessToken}`}
      : {}
    const context = await playwright.request.newContext({baseURL, extraHTTPHeaders: headers})
    await use(context)
    await context.dispose()
  },
})

export {expect} from '@playwright/test'
