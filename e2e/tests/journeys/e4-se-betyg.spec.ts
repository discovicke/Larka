import {expect, test} from '../../fixtures'
import {createModule, createPart, createWorld, grade, login, submitAs} from '../../journeys'

// E4: eleven ser lärarens återkoppling och betyg under Studieplan och betyg, och på delen själv.
test('E4: eleven ser återkoppling och betyg', async ({apiAs, pageAs, request}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const [student] = world.students
  const studentApi = await apiAs(await login(request, student.email))
  const week = await createModule(teacherApi, world.course.id, 'Vecka 1')
  const part = await createPart(teacherApi, world.course.id, week.id, `Redovisning ${world.tag}`, {
    type: 'Assignment',
    dueInDays: 3,
  })
  const submission = await submitAs(studentApi, part.linkId, 'Här är min redovisning')
  await grade(teacherApi, submission.id, 'Tydlig och välstrukturerad.', 'Distinction')

  const page = await pageAs(await login(request, student.email))
  await page.goto('/grades')

  // Kursen och uppgiften finns på sidan. Återkopplingen fälls ut när man trycker på uppgiften.
  await expect(page.getByTestId('study-plan-course').filter({hasText: world.course.title})).toBeVisible()
  const row = page.getByTestId('study-plan-assignment').filter({hasText: part.title})
  await expect(row).toBeVisible()
  await row.getByRole('button').click()
  await expect(row.getByTestId('study-plan-feedback')).toContainText('Tydlig och välstrukturerad.')

  // Från raden öppnas delen med betygsstämpeln och kommentaren.
  await row.getByTestId('study-plan-open').click()
  await expect(page.getByTestId('student-feedback')).toContainText('Tydlig och välstrukturerad.')
  await expect(page.getByTestId('grade-stamp')).toBeVisible()
  // En bedömd uppgift har ingen hjälpknapp.
  await expect(page.getByTestId('student-help')).toHaveCount(0)
})
