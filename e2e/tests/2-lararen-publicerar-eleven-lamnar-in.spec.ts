import {expect, test} from '../fixtures'
import {createWorld, login, typeInEditor} from '../journeys'

// 2. Läraren lägger en uppgift i en ny vecka och publicerar. Eleven ser den först efter publiceringen, lämnar in
// en text, och inlämningen hamnar under lärarens Att bedöma.
test('2: läraren publicerar en uppgift och eleven lämnar in', async ({apiAs, pageAs, request}) => {
  const world = await createWorld(await apiAs('admin'))
  const [student] = world.students
  const teacherPage = await pageAs(await login(request, world.teacher.email), 'lärare')
  const courseUrl = `/courses/${world.course.id}/content`
  const weekTitle = `Vecka ${world.tag}`
  const assignmentTitle = `Uppgift ${world.tag}`

  // En ny vecka börjar som utkast.
  await teacherPage.goto(courseUrl)
  await teacherPage.getByTestId('add-module').click()
  await teacherPage.getByLabel('Titel').fill(weekTitle)
  await teacherPage.getByRole('dialog').locator('button[type="submit"]').click()
  const week = teacherPage.getByRole('region', {name: weekTitle})
  await expect(week).toContainText('Utkast')

  // Lägg till del: en uppgift som eleven lämnar in som text.
  await week.getByTestId('add-part').click()
  await teacherPage.locator('label[for="part-Submission"]').click()
  await teacherPage.getByLabel('Titel').fill(assignmentTitle)
  await typeInEditor(teacherPage, 'Skriv en kort reflektion', teacherPage.locator('[contenteditable="true"]').first())
  await teacherPage.locator('label[for="mode-Text"]').click()
  await teacherPage.getByTestId('save-draft').click()
  await expect(week.getByTestId('module-item').filter({hasText: assignmentTitle})).toBeVisible()

  // Eleven ser ingenting förrän läraren publicerar veckan. Elevens sida öppnas först här, så att videon inte börjar
  // med en tom sida medan läraren arbetar.
  const studentPage = await pageAs(await login(request, student.email), 'elev')
  await studentPage.goto(courseUrl)
  await expect(studentPage.getByText(weekTitle)).toHaveCount(0)
  await week.getByTestId('publish-week').click()
  await expect(week.getByTestId('publish-week')).toHaveCount(0)

  // Eleven öppnar uppgiften och lämnar in.
  await studentPage.reload()
  await expect(studentPage.getByTestId('student-module').filter({hasText: weekTitle})).toBeVisible()
  await studentPage.getByText(assignmentTitle).click()
  const form = studentPage.getByTestId('submission-form')
  await typeInEditor(studentPage, 'Min reflektion', form.locator('[contenteditable="true"]'))
  await studentPage.getByTestId('submission-submit').click()
  await expect(studentPage.getByTestId('submission-view')).toContainText('Min reflektion')

  // Inlämningen väntar under lärarens Att bedöma.
  await teacherPage.goto('/grading')
  await expect(teacherPage.getByTestId('grading-row').filter({hasText: student.name})).toBeVisible()
})
