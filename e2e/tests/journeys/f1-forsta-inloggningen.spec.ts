import type {Page} from '@playwright/test'

import type {StoredToken} from '../../auth'
import {expect, test} from '../../fixtures'
import {createUser, createWorld, json, login} from '../../journeys'

// F1: första inloggningen med Microsoft. Den riktiga Microsoft-inloggningen går inte att köra i CI, så testet byter
// ut de två yttre delarna och låter resten vara äkta:
// - MSAL-biblioteket (en egen chunk i bygget) ersätts med en stub som skickar tillbaka till /login med en kod.
// - Svaret från POST /api/auth/microsoft ersätts med en token från vanlig inloggning, med firstSignIn satt.
// Klientens inloggningssida, välkomststeget, profilen och sidorna efter är de riktiga. Det som INTE täcks är
// kontrollen av ID-tokenet på servern (det har backendtesterna med lokalt signerade tokens) och själva Microsoft.
async function mockMicrosoft(page: Page, token: StoredToken) {
  await page.route('**/api/auth/providers', (route) => route.fulfill({
    json: {microsoft: {clientId: '00000000-0000-0000-0000-000000000001', tenantId: '00000000-0000-0000-0000-000000000002'}},
  }))
  await page.route(/\/assets\/msal-[^/]*\.js$/, (route) => route.fulfill({
    contentType: 'text/javascript',
    body: `
      export class PublicClientApplication {
        async initialize() {}
        async loginRedirect(request) {
          // Microsoft svarar med en riktig sidladdning. Frågetecknet gör att den blir det även i stubben.
          window.location.assign('/login?microsoft=1#code=stub&state=' + encodeURIComponent(request.state ?? '/'))
        }
        async handleRedirectPromise() {
          const hash = new URLSearchParams(window.location.hash.slice(1))
          return hash.get('code') ? {idToken: 'stub', state: hash.get('state')} : null
        }
      }`,
  }))
  await page.route('**/api/auth/microsoft', (route) => route.fulfill({json: {...token, firstSignIn: true}}))
}

// Sidan startar utloggad: det är inloggningen som testas.
test.use({signedIn: false})

test('F1: första inloggningen med Microsoft, elev utan klass hamnar på sidan utan klass', async ({page, apiAs, request}) => {
  const admin = await apiAs('admin')
  const tag = Date.now().toString(36)
  const user = await createUser(admin, `Ny Elev ${tag}`, 'Student')
  await mockMicrosoft(page, await login(request, user.email))

  await page.goto('/login')
  await page.getByTestId('microsoft-sign-in').click()

  // Välkomststeget: namnet kan rättas, sedan går det vidare.
  await expect(page).toHaveURL(/\/valkommen/)
  const nameField = page.getByTestId('welcome-name')
  await expect(nameField).toHaveValue(user.name)
  await nameField.fill(`Rättat Namn ${tag}`)
  await page.getByTestId('welcome-continue').click()

  // Eleven har ingen klass än, så Idag förklarar det i stället för att vara tom.
  await expect(page.getByTestId('today-no-class')).toBeVisible()
  const me = await json<{name: string}>(await (await apiAs(await login(request, user.email))).get('/api/me'))
  expect(me.name).toBe(`Rättat Namn ${tag}`)
})

test('F1: första inloggningen med Microsoft, elev i en klass ser sina kurser', async ({page, apiAs, request}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const [student] = world.students
  await mockMicrosoft(page, await login(request, student.email))

  await page.goto('/login')
  await page.getByTestId('microsoft-sign-in').click()
  await expect(page).toHaveURL(/\/valkommen/)
  await page.getByTestId('welcome-continue').click()

  // Rätt klass: kursen finns i menyn och den tomma sidan visas inte.
  await expect(page.getByTestId('today-no-class')).toHaveCount(0)
  await expect(page.locator('[data-slot="sidebar"]').getByRole('link', {name: world.course.title})).toBeVisible()
})
