import {expect, test} from '../../fixtures'
import {createWorld, login, typeInEditor} from '../../journeys'

// L1: läraren förbereder en ny kurs: beskrivning, en vecka som börjar som utkast, en del via Lägg till del,
// och publicerar. Eleven ser inget förrän veckan är publicerad.
test('L1: läraren förbereder kursen och publicerar', async ({apiAs, pageAs, request}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const [student] = world.students
  const teacherPage = await pageAs(await login(request, world.teacher.email))
  const studentPage = await pageAs(await login(request, student.email))
  const weekTitle = `Vecka ${world.tag}`
  const partTitle = `Del ${world.tag}`
  const courseUrl = `/courses/${world.course.id}`

  // Beskrivningen skrivs på kursens egen sida.
  await teacherPage.goto(`${courseUrl}/about`)
  await teacherPage.getByTestId('edit-course').click()
  const editDialog = teacherPage.getByRole('dialog')
  await typeInEditor(teacherPage, `Kursen handlar om ${world.tag}`, editDialog.locator('[contenteditable="true"]'))
  await editDialog.locator('button[type="submit"]').click()
  await expect(editDialog).toBeHidden()
  await expect(teacherPage.getByText(`Kursen handlar om ${world.tag}`)).toBeVisible()

  // En ny vecka är ett utkast.
  await teacherPage.goto(`${courseUrl}/content`)
  await teacherPage.getByTestId('add-module').click()
  await teacherPage.getByLabel('Titel').fill(weekTitle)
  await teacherPage.getByRole('dialog').locator('button[type="submit"]').click()
  const week = teacherPage.getByRole('region', {name: weekTitle})
  await expect(week).toBeVisible()
  await expect(week).toContainText('Utkast')

  // Lägg till del går direkt till redigeraren. Delen sparas som utkast.
  await week.getByTestId('add-part').click()
  await expect(teacherPage).toHaveURL(/\/modules\/.*\/new$/)
  await teacherPage.getByLabel('Titel').fill(partTitle)
  await typeInEditor(teacherPage, 'Läs det här först', teacherPage.locator('[contenteditable="true"]').first())
  await teacherPage.getByTestId('save-draft').click()
  await expect(teacherPage).toHaveURL(new RegExp(`${courseUrl}/content`))
  await expect(week.getByTestId('module-item').filter({hasText: partTitle})).toBeVisible()

  // Eleven ser ingen vecka än.
  await studentPage.goto(`${courseUrl}/content`)
  await expect(studentPage.getByText(weekTitle)).toHaveCount(0)

  // Läraren publicerar veckan.
  await week.getByTestId('publish-week').click()
  await expect(week.getByTestId('publish-week')).toHaveCount(0)

  // Nu ser eleven veckan och delen.
  await studentPage.reload()
  await expect(studentPage.getByTestId('student-module').filter({hasText: weekTitle})).toBeVisible()
  await expect(studentPage.getByText(partTitle)).toBeVisible()
})
