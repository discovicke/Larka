import {expect, test} from '../fixtures'
import {createUser, pickDay, uid} from '../journeys'

// 1. Admin förbereder kursen: skapar en klass, lägger in elever och skapar en kurs med en lärare på klassens sida.
// Eleverna finns redan i skolan, som efter sin första inloggning med Microsoft.
test('1: admin skapar en klass med elever och en kurs med lärare', async ({apiAs, pageAs}) => {
  const tag = uid()
  const teacher = await createUser(await apiAs('admin'), `Lärare ${tag}`, 'Teacher')
  const page = await pageAs('admin')
  const className = `Klass ${tag}`
  const courseTitle = `Kurs ${tag}`
  const students = [`Siv ${tag}`, `Stig ${tag}`]
  for (const name of students) {
    await createUser(await apiAs('admin'), name, 'Student')
  }

  // Ny klass från Alla grupper. Efter sparandet öppnas klassens sida.
  await page.goto('/groups')
  await page.getByTestId('new-menu').click()
  await page.getByTestId('new-class').click()
  await page.getByTestId('group-name').fill(className)
  await page.getByTestId('save-group').click()
  await expect(page).toHaveURL(/\/groups\/[0-9a-f-]+$/)
  await expect(page.getByRole('heading', {name: className, level: 1})).toBeVisible()

  // Eleverna läggs till en i taget: sök på namnet och välj träffen i listan.
  for (const name of students) {
    await page.getByTestId('add-member').click()
    await page.getByTestId('member-user-search').fill(name)
    await expect(page.getByTestId('member-user-option')).toHaveCount(1)
    await page.getByTestId('member-user-option').filter({hasText: name}).click()
    await page.getByTestId('confirm-add-member').click()
    await expect(page.getByTestId('member-row').filter({hasText: name})).toBeVisible()
  }

  // Ny kurs på klassens sida: namn, kod, omfattning, datum och lärare i ett flöde.
  await page.getByTestId('new-course-button').click()
  await page.getByTestId('new-course-empty').click()
  await page.getByTestId('new-course-title').fill(courseTitle)
  await page.getByTestId('new-course-code').fill(`NY-${tag}`.toUpperCase())
  await page.getByTestId('new-course-points').fill('20')
  await pickDay(page, 'new-course-start', 10)
  await pickDay(page, 'new-course-end', 20)
  await page.getByTestId('new-course-add-teacher').click()
  await page.getByTestId('new-course-teacher-search').fill(teacher.name)
  await page.getByTestId('new-course-teacher-option').filter({hasText: teacher.name}).click()
  await page.getByTestId('save-new-course').click()

  // Kursen står på klassens sida med sin lärare.
  const course = page.getByTestId('course-row').filter({hasText: courseTitle})
  await expect(course).toBeVisible()
  await expect(course.getByTestId('course-teacher-names')).toContainText(teacher.name)
})
