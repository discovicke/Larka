import {expect, test} from '../../fixtures'
import {createModule, createPart, createWorld, login, typeInEditor} from '../../journeys'

// L2: läraren kör veckan: ändrar en del och publicerar en nyhet. Eleven ser båda.
test('L2: läraren ändrar en del och skriver en nyhet', async ({apiAs, pageAs, request}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const [student] = world.students
  const teacherApi = await apiAs(await login(request, world.teacher.email))
  const week = await createModule(teacherApi, world.course.id, 'Vecka 1')
  const part = await createPart(teacherApi, world.course.id, week.id, `Gammal titel ${world.tag}`)
  const teacherPage = await pageAs(await login(request, world.teacher.email))
  const studentPage = await pageAs(await login(request, student.email))
  const courseUrl = `/courses/${world.course.id}`
  const newTitle = `Ny titel ${world.tag}`
  const newsTitle = `Nyhet ${world.tag}`

  // Läraren byter titel på delen.
  await teacherPage.goto(`${courseUrl}/content`)
  const row = teacherPage.getByTestId('module-item').filter({hasText: part.title})
  await row.getByRole('button', {name: /Fler val/}).click()
  await teacherPage.getByRole('menuitem', {name: 'Redigera'}).click()
  await expect(teacherPage).toHaveURL(/\/edit$/)
  await teacherPage.getByLabel('Titel').fill(newTitle)
  await teacherPage.getByTestId('save-part').click()
  // Efter sparandet öppnas delen med den nya titeln.
  await expect(teacherPage.getByRole('heading', {name: newTitle})).toBeVisible()

  // Läraren skriver en nyhet.
  await teacherPage.goto(`${courseUrl}/news`)
  await teacherPage.getByTestId('new-news').click()
  const dialog = teacherPage.getByRole('dialog')
  await dialog.getByLabel('Rubrik').fill(newsTitle)
  await typeInEditor(teacherPage, 'Kom ihåg att läsa kapitel två', dialog.locator('[contenteditable="true"]'))
  await dialog.locator('button[type="submit"]').click()
  await expect(teacherPage.getByTestId('news-item').filter({hasText: newsTitle})).toBeVisible()

  // Eleven ser den nya titeln, nyheten i kursen och nyheten på Idag.
  await studentPage.goto(`${courseUrl}/content`)
  await expect(studentPage.getByTestId(`student-part-${part.linkId}`)).toContainText(newTitle)
  await studentPage.goto(`${courseUrl}/news`)
  await expect(studentPage.getByTestId('news-item').filter({hasText: newsTitle})).toBeVisible()
  await studentPage.goto('/')
  await expect(studentPage.getByTestId('today-desktop').getByTestId('news-item').filter({hasText: newsTitle})).toBeVisible()
})
