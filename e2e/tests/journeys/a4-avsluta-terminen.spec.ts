import {expect, test} from '../../fixtures'
import {createWorld} from '../../journeys'

// A4: admin avslutar en kurs genom att sätta slutdatum bakåt. Kursen flyttar från Pågår till Avslutade, både i
// sidomenyn och på klassens sida, och finns kvar med allt innehåll.
test('A4: admin avslutar kursen och den hamnar under Avslutade', async ({apiAs, pageAs}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin, {courseStartsInDays: -1000})
  const page = await pageAs('admin')
  const sidebar = page.locator('[data-slot="sidebar"]')

  await page.goto(`/courses/${world.course.id}/about`)
  // Kursen pågår: den ligger bland de vanliga kurserna, inte under Avslutade.
  await expect(sidebar.getByRole('link', {name: world.course.title})).toBeVisible()
  await expect(page.getByTestId('finished-courses').getByRole('link', {name: world.course.title})).toHaveCount(0)

  // Kalendern öppnas vid startdatumets månad, som ligger långt före idag. Slutdatumet sätts en månad senare.
  await page.getByTestId('edit-course').click()
  const dialog = page.locator('[data-slot="dialog-content"]')
  await dialog.locator('#course-edit-end').click()
  await page.getByRole('button', {name: /next|nästa/i}).click()
  await page.locator('[data-reka-calendar-cell-trigger]:not([data-outside-view])').getByText('15', {exact: true}).click()
  await page.keyboard.press('Escape')
  await dialog.locator('button[type="submit"]').click()
  await expect(dialog).toBeHidden()

  // Nu ligger kursen under Avslutade i menyn och i klassens kurslista.
  const finished = page.getByTestId('finished-courses')
  await expect(finished).toBeVisible()
  await expect(finished.getByRole('link', {name: world.course.title})).toBeVisible()

  // På klassens sida är Avslutade hopfälld tills admin öppnar den. Kursen finns kvar där.
  await page.goto(`/groups/${world.group.id}`)
  const finishedGroup = page.locator('[data-test="course-group"][aria-label="Avslutade"]')
  const finishedRow = finishedGroup.getByTestId('course-row').filter({hasText: world.course.title})
  await expect(finishedRow).toBeHidden()
  await page.getByTestId('finished-courses-toggle').click()
  await expect(finishedRow).toBeVisible()
})
