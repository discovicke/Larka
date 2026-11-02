import {expect, test} from '../../fixtures'
import {createModule, createPart, createWorld, login} from '../../journeys'

// E2: eleven öppnar kursen, läser veckan och klarmarkerar en del. Markeringen finns kvar efter omladdning.
test('E2: eleven läser veckan och klarmarkerar en del', async ({apiAs, pageAs, request}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const week = await createModule(teacherApi, world.course.id, `Vecka ${world.tag}`)
  const theory = await createPart(teacherApi, world.course.id, week.id, `Teori ${world.tag}`, {body: 'Läs detta först.'})
  await createPart(teacherApi, world.course.id, week.id, `Teori två ${world.tag}`)
  const [student] = world.students

  const page = await pageAs(await login(request, student.email))
  await page.goto(`/courses/${world.course.id}/content`)

  // Veckan och dess delar syns.
  await expect(page.getByTestId('student-module').filter({hasText: week.title})).toBeVisible()
  await expect(page.getByTestId(`student-part-${theory.linkId}`)).toBeVisible()

  // Delen öppnas och kan klarmarkeras.
  await page.getByTestId(`student-open-${theory.linkId}`).click()
  await expect(page.getByTestId('student-detail')).toContainText('Läs detta först.')
  // Brödsmulan leder tillbaka till kursen.
  await expect(page.locator('header nav')).toContainText(world.course.title)
  await page.getByTestId(`complete-${theory.linkId}`).click()
  await expect(page.getByTestId(`undo-${theory.linkId}`)).toBeVisible()

  // Nästa del leder vidare utan att gå tillbaka till listan, och Föregående leder tillbaka.
  await page.getByTestId('student-next').click()
  await expect(page.getByTestId('student-detail')).toContainText(`Teori två ${world.tag}`)
  await page.getByTestId('student-previous').click()
  await expect(page.getByTestId('student-detail')).toContainText('Läs detta först.')

  // Markeringen sparades på servern.
  await page.reload()
  await expect(page.getByTestId(`undo-${theory.linkId}`)).toBeVisible()
  await page.getByTestId(`undo-${theory.linkId}`).click()
  await expect(page.getByTestId(`complete-${theory.linkId}`)).toBeVisible()
})
