import {type Locator, type Page} from '@playwright/test'

import {expect, test} from '../fixtures'

// Testerna är bara läsning: de öppnar sidor och tittar, och klickar aldrig på något som sparar.
// Med inloggning, som eleven, om inget annat anges (fixtures.ts).

const mobileSidebar = (page: Page) => page.locator('[data-slot="sidebar"][data-mobile="true"]')

function sidebarLink(page: Page, name: string): Locator {
  return page.locator('[data-slot="sidebar"]').getByRole('link', {name, exact: true})
}

// På mobil ligger sidomenyn i en sheet som är stängd tills man trycker på knappen i headern.
// På desktop ligger menyn alltid framme.
async function openSidebar(page: Page) {
  const trigger = page.getByRole('button', {name: 'Visa/dölj sidomeny'})
  if (!(await trigger.isVisible())) {
    return
  }
  const alreadyOpen = await page.locator('[data-slot="sidebar"]').getByRole('link').first().isVisible()
  if (!alreadyOpen) {
    await trigger.click()
  }
}

// Sheeten ligger kvar över sidan efter navigeringen och måste stängas innan man kan klicka vidare.
async function closeSidebar(page: Page) {
  if (!(await mobileSidebar(page).isVisible())) {
    return
  }
  await page.keyboard.press('Escape')
  await expect(mobileSidebar(page)).toBeHidden()
}

test('hälsa och version svarar', async ({request}) => {
  const health = await request.get('/health')
  expect(health.status()).toBe(200)

  const version = await request.get('/version')
  expect(version.status()).toBe(200)
  const body: {version?: unknown} = await version.json()
  expect(typeof body.version).toBe('string')
})

test('startsidan laddar med sidomenyn', async ({page}) => {
  await page.goto('/')

  await expect(page).toHaveTitle(/· RebornLMS$/)
  await openSidebar(page)

  for (const name of ['Idag', 'Kalender', 'Inkorg']) {
    await expect(sidebarLink(page, name)).toBeVisible()
  }
})

test('en kurs går att öppna och nyhetsfliken fungerar', async ({page, request}) => {
  const response = await request.get('/api/courses')
  expect(response.status()).toBe(200)
  const courses: {id: string, title: string}[] = await response.json()
  // Eleven ligger i en klass som är kopplad till kurser, så det ska alltid finnas en kurs att öppna.
  expect(courses.length).toBeGreaterThan(0)

  const [course] = courses
  await page.goto('/')
  await openSidebar(page)

  const courseLink = sidebarLink(page, course.title)
  await expect(courseLink).toBeVisible()
  await courseLink.click()
  await closeSidebar(page)

  await expect(page).toHaveURL(new RegExp(`/courses/${course.id}/content$`))
  await expect(page.getByRole('heading', {name: course.title})).toBeVisible()

  await page.getByRole('link', {name: 'Nyheter'}).click()
  await expect(page).toHaveURL(/\/news$/)
  await expect(page).toHaveTitle(/Nyheter · RebornLMS$/)
})

test('alla grupper visar en tabell utan sidledes scroll', async ({page}) => {
  await page.goto('/groups')

  await expect(page.getByRole('heading', {name: 'Alla grupper'})).toBeVisible()
  // Sista raden är "Inga grupper matchar sökningen." när det inte finns något att visa.
  await expect(page.locator('table tbody tr').first()).toBeVisible()

  const noOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth <= window.innerWidth,
  )
  expect(noOverflow).toBe(true)
})

test('okänd sökväg visar sidan finns inte', async ({page}) => {
  await page.goto('/finns-inte')

  await expect(page.getByRole('heading', {name: 'Sidan finns inte'})).toBeVisible()
})

test.describe('utan inloggning', () => {
  test.use({signedIn: false})

  test('utloggad skickas från /groups till inloggningen', async ({page}) => {
    await page.goto('/groups')

    await expect(page).toHaveURL(/\/login\?redirect=/)
    await expect(page.getByRole('button', {name: 'Logga in', exact: true})).toBeVisible()
  })
})
