import {expect, test} from '../../fixtures'
import {createModule, createPart, createWorld, grade, login, submitAs} from '../../journeys'

// E1: Idag, Kalender och mobilen visar vad eleven ska göra, med det läraren vill ha kompletterat först.
// Körs både som desktop och som mobil.
test('E1: eleven ser vad hen ska göra, Komplettera först', async ({apiAs, pageAs, request, isMobile}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const [student] = world.students
  const studentApi = await apiAs(await login(request, student.email))
  const week = await createModule(teacherApi, world.course.id, 'Vecka 1')

  // Uppgiften som ska kompletteras har redan passerat sin deadline. Den ska ändå stå först i Att göra, före
  // uppgiften som snart ska in: en komplettering försvinner inte för att datumet gått.
  const needsWork = await createPart(teacherApi, world.course.id, week.id, `Komplettera ${world.tag}`, {
    type: 'Assignment',
    dueInDays: -2,
  })
  const soon = await createPart(teacherApi, world.course.id, week.id, `Snart ${world.tag}`, {type: 'Assignment', dueInDays: 1})
  const submission = await submitAs(studentApi, needsWork.linkId, 'Första försöket')
  await grade(teacherApi, submission.id, 'Visa ett exempel till', null, true)

  const page = await pageAs(await login(request, student.email))
  await page.goto('/')
  const today = page.getByTestId(isMobile
    ? 'today-mobile'
    : 'today-desktop')

  if (isMobile) {
    // Närmast: det som ska kompletteras, sedan resten av listan.
    await expect(today.getByTestId('next-up')).toContainText(needsWork.title)
    await expect(today.getByTestId('mobile-todo-item').filter({hasText: soon.title})).toBeVisible()
    // Nytt: återkopplingen syns lika på mobilen som på datorn.
    await expect(today.getByTestId('mobile-recent-item').filter({hasText: needsWork.title})).toBeVisible()
  } else {
    await expect(today.locator('[data-test^="group-"]').first().getByTestId('todo-item')).toContainText(needsWork.title)
    await expect(today.getByTestId('todo-item').filter({hasText: soon.title})).toBeVisible()
    await expect(today.getByTestId('feedback-item').filter({hasText: needsWork.title})).toBeVisible()
  }

  // Kalendern visar deadline för uppgiften som ska göras.
  await page.goto('/calendar')
  await expect(page.getByTestId('calendar-chip').filter({hasText: soon.title}).first()).toBeVisible()

  // Ett tryck från Idag öppnar delen med lärarens återkoppling.
  await page.goto('/')
  await today.getByText(needsWork.title).first().click()
  await expect(page.getByTestId('student-feedback')).toContainText('Visa ett exempel till')
})
