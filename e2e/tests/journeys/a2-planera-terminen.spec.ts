import {expect, test} from '../../fixtures'
import {createLooseCourse, createUser, createWorld, pickDay} from '../../journeys'

// A2: admin planerar terminen från klassens sida: Ny kurs med lärare, och Koppla kurs. Läraren syns på kursraden
// direkt, utan att sidan laddas om.
test('A2: admin skapar en ny kurs och kopplar en befintlig', async ({apiAs, pageAs}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const newTeacher = await createUser(admin, `Lärare B ${world.tag}`, 'Teacher')
  const looseTeacher = await createUser(admin, `Lärare C ${world.tag}`, 'Teacher')
  const loose = await createLooseCourse(admin, world.tag, looseTeacher)
  const page = await pageAs('admin')
  const newTitle = `Ny kurs ${world.tag}`

  await page.goto(`/groups/${world.group.id}`)

  // Ny kurs: tom kurs med namn, kod, omfattning, datum och en lärare. Allt i ett flöde.
  await page.getByTestId('new-course-button').click()
  await page.getByTestId('new-course-empty').click()
  await page.getByTestId('new-course-title').fill(newTitle)
  await page.getByTestId('new-course-code').fill(`NY-${world.tag}`.toUpperCase())
  await page.getByTestId('new-course-points').fill('20')
  await pickDay(page, 'new-course-start', 10)
  await pickDay(page, 'new-course-end', 20)
  await page.getByTestId('new-course-add-teacher').click()
  await page.getByTestId('new-course-teacher-search').fill(newTeacher.name)
  await page.getByTestId('new-course-teacher-option').filter({hasText: newTeacher.name}).click()
  await expect(page.getByTestId('new-course-teacher').filter({hasText: newTeacher.name})).toBeVisible()
  await page.getByTestId('save-new-course').click()

  const created = page.getByTestId('course-row').filter({hasText: newTitle})
  await expect(created).toBeVisible()
  await expect(created.getByTestId('course-teacher-names')).toContainText(newTeacher.name)

  // Koppla kurs: en kurs som redan finns. Dess lärare syns utan omladdning.
  await page.getByTestId('link-course-button').click()
  await page.getByTestId('link-course').selectOption({label: `${loose.title} · LOS-${world.tag.toUpperCase()}`})
  await page.getByTestId('confirm-link-course').click()
  const linked = page.getByTestId('course-row').filter({hasText: loose.title})
  await expect(linked).toBeVisible()
  await expect(linked.getByTestId('course-teacher-names')).toContainText(looseTeacher.name)
})
