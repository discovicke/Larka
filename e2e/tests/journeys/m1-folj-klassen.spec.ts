import {expect, test} from '../../fixtures'
import {createModule, createPart, createWorld, login, submitAs} from '../../journeys'

// M1: mentorn följer klassen: ser elever, kurser och inlämningar, men kan inte ändra något.
test('M1: mentorn läser klassen utan att kunna ändra', async ({apiAs, pageAs, request}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin, {mentor: true})
  const [student] = world.students
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const week = await createModule(teacherApi, world.course.id, 'Vecka 1')
  const part = await createPart(teacherApi, world.course.id, week.id, `Uppgift ${world.tag}`, {type: 'Assignment', dueInDays: 3})
  const submission = await submitAs(await apiAs(await login(request, student.email)), part.linkId, 'Elevens svar')

  const page = await pageAs(await login(request, world.mentor!.email))

  // Klassen: eleverna syns, men inget går att lägga till eller ändra.
  await page.goto(`/groups/${world.group.id}`)
  await expect(page.getByTestId('member-row').filter({hasText: student.name})).toBeVisible()
  await expect(page.getByTestId('add-member')).toHaveCount(0)
  await expect(page.getByTestId('new-course-button')).toHaveCount(0)
  await expect(page.getByTestId('set-mentor')).toHaveCount(0)

  // Elevens läge i klassens alla kurser: inlämningen väntar på bedömning.
  await page.getByTestId('member-row').filter({hasText: student.name}).getByTestId('member-overview').click()
  const overview = page.getByTestId('student-overview')
  await expect(overview.getByTestId('overview-course').filter({hasText: world.course.title})).toBeVisible()
  await expect(overview.getByTestId('overview-submitted')).toContainText('1')
  await page.keyboard.press('Escape')

  // Kursen: innehållet läses, men det finns inga knappar för att lägga till eller publicera.
  await page.goto(`/courses/${world.course.id}/content`)
  await expect(page.getByText(`Uppgift ${world.tag}`)).toBeVisible()
  await expect(page.getByTestId('add-part')).toHaveCount(0)
  await expect(page.getByTestId('add-module')).toHaveCount(0)
  await expect(page.getByTestId('publish-week')).toHaveCount(0)

  // Inlämningen läses, men det går inte att bedöma.
  await page.goto(`/courses/${world.course.id}/assignments/${part.linkId}/submissions/${submission.id}`)
  await expect(page.getByText('Elevens svar')).toBeVisible()
  await expect(page.getByTestId('review-save')).toHaveCount(0)
  await expect(page.getByLabel('Sammanfattande kommentar')).toHaveCount(0)

  // API:et håller med: mentorn får inte sätta betyg.
  const mentorApi = await apiAs(await login(request, world.mentor!.email))
  const response = await mentorApi.post(`/api/submissions/${submission.id}/grade`, {data: {feedback: 'x', level: 'Pass', needsWork: false}})
  expect(response.status()).toBeGreaterThanOrEqual(400)
  expect(response.status()).toBeLessThan(500)
})
