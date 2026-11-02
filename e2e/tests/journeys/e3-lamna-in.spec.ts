import {expect, test} from '../../fixtures'
import {createModule, createPart, createWorld, login, typeInEditor} from '../../journeys'

// E3: eleven lämnar in, läraren begär komplettering, eleven kompletterar och läraren ser den nya inlämningen.
test('E3: eleven lämnar in och kompletterar', async ({apiAs, pageAs, request}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const week = await createModule(teacherApi, world.course.id, 'Vecka 1')
  const part = await createPart(teacherApi, world.course.id, week.id, `Uppgift ${world.tag}`, {
    type: 'Assignment',
    dueInDays: 5,
  })
  const [student] = world.students

  const studentPage = await pageAs(await login(request, student.email))
  const teacherPage = await pageAs(await login(request, world.teacher.email))

  // Eleven hittar uppgiften under Att göra och lämnar in en text.
  await studentPage.goto('/')
  const todo = studentPage.getByTestId('today-desktop').getByTestId('todo-item').filter({hasText: part.title})
  await todo.click()
  // Innan inlämning finns hjälpknappen.
  await expect(studentPage.getByTestId('student-help')).toBeVisible()
  const form = studentPage.getByTestId('submission-form')
  await typeInEditor(studentPage, 'Min första version', form.locator('[contenteditable="true"]'))
  await studentPage.getByTestId('submission-submit').click()
  await expect(studentPage.getByTestId('submission-view')).toContainText('Min första version')
  // Efter inlämning är hjälpknappen borta, och ingen väntetext om betyg visas.
  await expect(studentPage.getByTestId('student-help')).toHaveCount(0)

  // Läraren ser den under Att bedöma och begär komplettering.
  await teacherPage.goto('/grading')
  await teacherPage.getByTestId('grading-row').filter({hasText: student.name}).getByRole('link').click()
  await teacherPage.getByLabel('Sammanfattande kommentar').fill('Lägg till ett exempel')
  await teacherPage.getByTestId('review-request-work').click()
  await expect(teacherPage).toHaveURL(/\/grading$/)

  // Komplettera ligger först i Att göra, före allt annat.
  await studentPage.goto('/')
  const desktop = studentPage.getByTestId('today-desktop')
  const firstGroup = desktop.locator('[data-test^="group-"]').first()
  await expect(firstGroup).toContainText(part.title)
  await firstGroup.getByTestId('todo-item').filter({hasText: part.title}).click()
  await expect(studentPage.getByTestId('student-feedback')).toContainText('Lägg till ett exempel')

  // Eleven kompletterar och lämnar in igen.
  await typeInEditor(studentPage, ' och ett exempel', studentPage.getByTestId('submission-form').locator('[contenteditable="true"]'))
  await studentPage.getByTestId('submission-submit').click()
  await expect(studentPage.getByTestId('submission-view')).toContainText('och ett exempel')

  // Läraren ser den nya inlämningen i kön igen.
  await teacherPage.goto('/grading')
  await expect(teacherPage.getByTestId('grading-row').filter({hasText: student.name})).toBeVisible()
})
