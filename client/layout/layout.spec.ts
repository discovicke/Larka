import {expect, type Page, test} from '@playwright/test'

import {routes, signInAndMockApi} from './mocks'

// Vitest kör i jsdom, som saknar layout. Här mäts sidorna i en riktig webbläsare.
// Måtten kommer från mockupen (docs/design.md): header minst 56 px, sidomeny 256 px, innehåll högst 1120 px.

const phone = {width: 390, height: 844}
const desktop = {width: 1440, height: 900}

test.beforeEach(async ({page}) => {
  await signInAndMockApi(page)
})

async function open(page: Page, path: string, ready: string) {
  await page.goto(path)
  await expect(page.getByText(ready, {exact: false}).first()).toBeVisible()
  // Fonter och lazy-laddade delar (editorn) ska hunnit lägga sig innan vi mäter.
  await page.waitForLoadState('networkidle')
}

async function expectNoHorizontalScroll(page: Page) {
  const {scrollWidth, innerWidth} = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }))
  expect(scrollWidth, `sidan är ${scrollWidth} px bred i ett fönster på ${innerWidth} px`).toBeLessThanOrEqual(innerWidth)
}

const pages = [
  {name: 'Idag', path: routes.today, ready: 'Idag'},
  {name: 'kursens innehåll', path: routes.courseContent, ready: 'Introduktion till continuous delivery'},
  {name: 'skapa innehåll', path: routes.createContent, ready: 'Typ av del'},
  {name: '404', path: routes.notFound, ready: 'Sidan finns inte'},
]

for (const [label, viewport] of [['390 px', phone], ['1440 px', desktop]] as const) {
  test.describe(`utan horisontell scroll vid ${label}`, () => {
    test.use({viewport})

    for (const {name, path, ready} of pages) {
      test(name, async ({page}) => {
        await open(page, path, ready)
        await expectNoHorizontalScroll(page)
      })
    }

    test('kopiera veckor-dialogen ryms i fönstret', async ({page}) => {
      await open(page, routes.courseContent, 'Introduktion till continuous delivery')
      await page.locator('[data-test="add-part-menu"]').first().click()
      await page.locator('[data-test="copy-weeks"]').click()
      const dialog = page.getByRole('dialog')
      await dialog.locator('select').selectOption({index: 1})
      await dialog.locator('[data-test^="copy-week-"]').first().click()
      await expect(dialog.locator('[data-test="copy-preview"]')).toBeVisible()
      await page.waitForTimeout(300)

      const overflow = await dialog.evaluate((element) => {
        const box = element.getBoundingClientRect()
        const outside = Array.from(element.querySelectorAll('*'))
          .filter((child) => child.getBoundingClientRect().right > box.right + 0.5)
          .map((child) => `${child.tagName}${child.getAttribute('data-test') ?? ''} ${Math.round(child.getBoundingClientRect().right)}/${Math.round(box.right)}`)
        return {scroll: element.scrollWidth - element.clientWidth, outside}
      })
      expect(overflow.outside).toEqual([])
      expect(overflow.scroll).toBeLessThanOrEqual(0)
      await expectNoHorizontalScroll(page)
    })

    test('ny kursomgång-dialogen ryms i fönstret', async ({page}) => {
      await open(page, routes.courseContent, 'Introduktion till continuous delivery')
      await page.locator('[data-test="course-menu"]').click()
      await page.locator('[data-test="new-run"]').click()
      const dialog = page.getByRole('dialog')
      await dialog.locator('[data-test="run-code"]').fill('NET26-DEVOPS-MED-LÅNG-KOD')
      await dialog.locator('select').selectOption({index: 2})
      await dialog.locator('[data-test="run-starts"]').fill('2026-10-12')
      await dialog.locator('[data-test="run-ends"]').fill('2026-11-13')
      await expect(dialog.locator('[data-test="run-copy-label"]')).toContainText('2 veckor, 3 delar')
      await page.waitForTimeout(300)

      const overflow = await dialog.evaluate((element) => {
        const box = element.getBoundingClientRect()
        const outside = Array.from(element.querySelectorAll('*'))
          .filter((child) => child.getBoundingClientRect().right > box.right + 0.5)
          .map((child) => `${child.tagName}${child.getAttribute('data-test') ?? ''} ${Math.round(child.getBoundingClientRect().right)}/${Math.round(box.right)}`)
        return {scroll: element.scrollWidth - element.clientWidth, outside, bottom: box.bottom}
      })
      expect(overflow.outside).toEqual([])
      expect(overflow.scroll).toBeLessThanOrEqual(0)
      await expectNoHorizontalScroll(page)
      await expect(dialog.getByRole('button', {name: 'Skapa kursomgång'})).toBeEnabled()
    })

    test('skapa innehåll med varje typ av del', async ({page}) => {
      await open(page, routes.createContent, 'Typ av del')
      for (const partType of ['Theory', 'Exercise', 'Submission']) {
        await page.locator(`#part-${partType}`).click()
        await expect(page.locator(`#part-${partType}`)).toHaveAttribute('data-state', 'checked')
        await expectNoHorizontalScroll(page)
      }
    })
  })
}

test.describe('mått från mockupen vid 1440 px', () => {
  test.use({viewport: desktop})

  test('headern är minst 56 px hög', async ({page}) => {
    await open(page, routes.today, 'Idag')
    const box = await page.locator('header').first().boundingBox()
    expect(box!.height).toBeGreaterThanOrEqual(56)
  })

  test('sidomenyn är 256 px bred när den är öppen', async ({page}) => {
    await open(page, routes.today, 'Idag')
    const box = await page.locator('[data-slot="sidebar-container"]').boundingBox()
    expect(box!.width).toBe(256)
  })

  test('innehållet är högst 1120 px brett', async ({page}) => {
    await open(page, routes.courseContent, 'Introduktion till continuous delivery')
    const box = await page.locator('[data-test="main-padded"] > div').boundingBox()
    expect(box!.width).toBeGreaterThan(0)
    expect(box!.width).toBeLessThanOrEqual(1120)
  })
})

test.describe('smal skärm', () => {
  for (const width of [390, 767]) {
    test(`sidomenyn är en drawer vid ${width} px`, async ({page}) => {
      await page.setViewportSize({width, height: 844})
      await open(page, routes.today, 'Idag')

      // Stängd tar menyn ingen plats i sidan och ingen fast kolumn finns.
      await expect(page.locator('[data-slot="sidebar-container"]')).toHaveCount(0)
      await expect(page.locator('[data-mobile="true"]')).toHaveCount(0)

      await page.getByRole('button', {name: 'Visa/dölj sidomeny'}).click()
      const drawer = page.locator('[data-mobile="true"]')
      await expect(drawer).toBeVisible()
      // Drawern glider in, så vänta tills den har lagt sig.
      await expect(async () => {
        const box = await drawer.boundingBox()
        expect(box!.x).toBeGreaterThanOrEqual(0)
        expect(box!.x + box!.width).toBeLessThanOrEqual(width)
      }).toPass()
    })
  }

  test('sidornas knappar i headern ligger inom skärmen', async ({page}) => {
    await page.setViewportSize(phone)
    await open(page, routes.createContent, 'Typ av del')
    const buttons = page.locator('#page-actions button')
    await expect(buttons.first()).toBeVisible()
    for (const button of await buttons.all()) {
      const box = await button.boundingBox()
      expect(box!.x).toBeGreaterThanOrEqual(0)
      expect(box!.x + box!.width).toBeLessThanOrEqual(phone.width)
    }
  })
})
