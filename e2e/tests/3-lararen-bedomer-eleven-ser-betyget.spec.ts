import {expect, test} from '../fixtures'
import {createModule, createPart, createWorld, login, submitAs} from '../journeys'

// 3. Läraren bedömer en inlämning från Att bedöma. Eleven ser betyget och kommentaren under Studieplan och betyg
// och på uppgiften själv.
test('3: läraren bedömer och eleven ser betyget', async ({apiAs, pageAs, request}) => {
  const world = await createWorld(await apiAs('admin'))
  const [student] = world.students
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const week = await createModule(teacherApi, world.course.id, 'Vecka 1')
  const assignment = await createPart(teacherApi, world.course.id, week.id, `Uppgift ${world.tag}`, {
    type: 'Assignment',
    dueInDays: 3,
  })
  await submitAs(await apiAs(await login(request, student.email)), assignment.linkId, 'Här är min redovisning')

  // Läraren öppnar inlämningen, sätter VG med en kommentar och kommer tillbaka till en tom kö.
  const teacherPage = await pageAs(await login(request, world.teacher.email))
  await teacherPage.goto('/grading')
  await teacherPage.getByTestId('grading-row').filter({hasText: student.name}).getByRole('link').click()
  await teacherPage.getByRole('radiogroup').getByRole('radio').nth(2).click()
  await teacherPage.getByLabel('Sammanfattande kommentar').fill('Tydlig och välstrukturerad.')
  await teacherPage.getByTestId('review-save').click()
  await expect(teacherPage).toHaveURL(/\/grading$/)
  await expect(teacherPage.getByTestId('grading-row')).toHaveCount(0)

  // Eleven ser kommentaren under Studieplan och betyg.
  const studentPage = await pageAs(await login(request, student.email))
  await studentPage.goto('/grades')
  const row = studentPage.getByTestId('study-plan-assignment').filter({hasText: assignment.title})
  await row.getByRole('button').click()
  await expect(row.getByTestId('study-plan-feedback')).toContainText('Tydlig och välstrukturerad.')

  // Från raden öppnas uppgiften med betygsstämpeln och kommentaren.
  await row.getByTestId('study-plan-open').click()
  await expect(studentPage.getByTestId('grade-stamp')).toBeVisible()
  await expect(studentPage.getByTestId('student-feedback')).toContainText('Tydlig och välstrukturerad.')
})
