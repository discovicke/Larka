import {expect, test} from '../fixtures'
import {createModule, createPart, createWorld, login, submitAs, typeInEditor} from '../journeys'

// 3. Läraren begär komplettering från Att bedöma. Eleven ser återkopplingen ovanför formuläret och lämnar in igen.
// Läraren bedömer den nya inlämningen, och eleven ser betyget och kommentaren under Studieplan och betyg och på
// uppgiften själv.
test('3: läraren begär komplettering, bedömer och eleven ser betyget', async ({apiAs, pageAs, request}) => {
  const world = await createWorld(await apiAs('admin'))
  const [student] = world.students
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const week = await createModule(teacherApi, world.course.id, 'Vecka 1')
  const assignment = await createPart(teacherApi, world.course.id, week.id, `Uppgift ${world.tag}`, {
    type: 'Assignment',
    dueInDays: 3,
  })
  await submitAs(await apiAs(await login(request, student.email)), assignment.linkId, 'Här är min redovisning')

  // Läraren öppnar inlämningen och begär komplettering. Kön är sedan tom.
  const teacherPage = await pageAs(await login(request, world.teacher.email), 'lärare')
  await teacherPage.goto('/grading')
  await teacherPage.getByTestId('grading-row').filter({hasText: student.name}).getByRole('link').click()
  await expect(teacherPage.getByTestId('review-state')).toContainText('Inlämnad')
  await teacherPage.getByLabel('Sammanfattande kommentar').fill('Lägg till en slutsats.')
  await teacherPage.getByTestId('review-request-work').click()
  await expect(teacherPage).toHaveURL(/\/grading$/)
  await expect(teacherPage.getByTestId('grading-row')).toHaveCount(0)

  // Eleven ser lärarens återkoppling ovanför formuläret, och till när kompletteringen ska in, och lämnar in igen.
  const studentPage = await pageAs(await login(request, student.email), 'elev')
  await studentPage.goto(`/courses/${world.course.id}/parts/${assignment.linkId}`)
  await expect(studentPage.getByTestId('submission-needs-work')).toContainText('Lägg till en slutsats.')
  await expect(studentPage.getByTestId('submission-open-until')).toContainText('Komplettera senast')
  const form = studentPage.getByTestId('submission-form')
  await typeInEditor(studentPage, ' Slutsats: det fungerar.', form.locator('[contenteditable="true"]'))
  await studentPage.getByTestId('submission-submit').click()
  await expect(studentPage.getByTestId('submission-view')).toContainText('Slutsats: det fungerar.')

  // Läraren bedömer kompletteringen: VG med en kommentar, och kommer tillbaka till en tom kö.
  await teacherPage.goto('/grading')
  await teacherPage.getByTestId('grading-row').filter({hasText: student.name}).getByRole('link').click()
  // VG är sista valet. IG visas inte medan en deadline återstår, så valen räknas inte från början.
  await teacherPage.getByRole('radiogroup').getByRole('radio').last().click()
  await teacherPage.getByLabel('Sammanfattande kommentar').fill('Tydlig och välstrukturerad.')
  await teacherPage.getByTestId('review-save').click()
  await expect(teacherPage).toHaveURL(/\/grading$/)
  await expect(teacherPage.getByTestId('grading-row')).toHaveCount(0)

  // Eleven ser kommentaren under Studieplan och betyg.
  await studentPage.goto('/grades')
  const row = studentPage.getByTestId('study-plan-assignment').filter({hasText: assignment.title})
  await row.getByRole('button').click()
  await expect(row.getByTestId('study-plan-feedback')).toContainText('Tydlig och välstrukturerad.')

  // Från raden öppnas uppgiften med betygsstämpeln och kommentaren.
  await row.getByTestId('study-plan-open').click()
  await expect(studentPage.getByTestId('grade-stamp')).toBeVisible()
  await expect(studentPage.getByTestId('student-feedback')).toContainText('Tydlig och välstrukturerad.')
})
