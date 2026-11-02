import {expect, test} from '../../fixtures'
import {createModule, createPart, createWorld, login, submitAs} from '../../journeys'

// L3: läraren bedömer från Att bedöma. Spara och nästa går vidare genom kön, in i nästa uppgift, och till sist
// tillbaka till Att bedöma. IG kräver en kommentar.
test('L3: läraren bedömer och går vidare i kön', async ({apiAs, pageAs, request}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin, {students: 2})
  const [first, second] = world.students
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const week = await createModule(teacherApi, world.course.id, 'Vecka 1')
  const partA = await createPart(teacherApi, world.course.id, week.id, `Uppgift A ${world.tag}`, {type: 'Assignment', dueInDays: 3})
  const partB = await createPart(teacherApi, world.course.id, week.id, `Uppgift B ${world.tag}`, {type: 'Assignment', dueInDays: 4})
  const firstApi = await apiAs(await login(request, first.email))
  const secondApi = await apiAs(await login(request, second.email))
  await submitAs(firstApi, partA.linkId, 'A från första')
  await submitAs(secondApi, partA.linkId, 'A från andra')
  await submitAs(firstApi, partB.linkId, 'B från första')

  const page = await pageAs(await login(request, world.teacher.email))
  await page.goto('/grading')
  await expect(page.getByTestId('grading-row')).toHaveCount(3)
  await page.getByTestId('grading-row').first().getByRole('link').click()

  const reviewed = new Set<string>()
  const radio = (index: number) => page.getByRole('radiogroup').getByRole('radio').nth(index)

  // Första inlämningen: IG går inte att spara utan kommentar.
  await expect(page.getByTestId('review-save')).toBeVisible()
  reviewed.add(page.url())
  await radio(0).click()
  await expect(page.getByTestId('review-save')).toBeDisabled()
  await page.getByLabel('Sammanfattande kommentar').fill('Saknar tester')
  await expect(page.getByTestId('review-save')).toBeEnabled()
  await page.getByTestId('review-save').click()

  // Resten av kön: G på varje inlämning tills läraren landar på Att bedöma igen.
  for (let step = 0; step < 2; step++) {
    await expect(page).toHaveURL(/\/submissions\//)
    await expect.poll(() => reviewed.has(page.url())).toBe(false)
    reviewed.add(page.url())
    await radio(1).click()
    await page.getByTestId('review-save').click()
  }

  await expect(page).toHaveURL(/\/grading$/)
  expect(reviewed.size).toBe(3)
  await expect(page.getByTestId('grading-row')).toHaveCount(0)
})
