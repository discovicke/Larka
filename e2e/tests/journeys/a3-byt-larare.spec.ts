import {expect, test} from '../../fixtures'
import {createUser, createWorld} from '../../journeys'

// A3: admin ersätter kursens lärare med en vikarie, och får en bekräftelse innan den sista läraren tas bort.
test('A3: admin ersätter läraren och bekräftar innan den sista tas bort', async ({apiAs, pageAs}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin)
  const substitute = await createUser(admin, `Vikarie ${world.tag}`, 'Teacher')
  const page = await pageAs('admin')

  await page.goto(`/groups/${world.group.id}`)
  const row = page.getByTestId('course-row').filter({hasText: world.course.title})
  await expect(row.getByTestId('course-teacher-names')).toContainText(world.teacher.name)
  await row.getByTestId('edit-teachers').click()
  const dialog = page.getByRole('dialog').filter({hasText: world.course.title})

  // Ersätt: välj vikarien och bekräfta. Listan byter lärare.
  await dialog.getByTestId('replace-teacher').click()
  await dialog.getByTestId('add-teacher-select').selectOption({value: substitute.id})
  await dialog.getByTestId('add-teacher').click()
  await expect(dialog.getByTestId('course-teacher').filter({hasText: substitute.name})).toBeVisible()
  await expect(dialog.getByTestId('course-teacher')).toHaveCount(1)

  // Den sista läraren tas inte bort förrän admin har bekräftat.
  await dialog.getByTestId('remove-teacher').click()
  const confirm = page.getByTestId('confirm-last-teacher')
  await expect(confirm).toBeVisible()
  await confirm.getByRole('button').first().click()
  await expect(confirm).toBeHidden()
  await expect(dialog.getByTestId('course-teacher')).toHaveCount(1)

  await dialog.getByTestId('remove-teacher').click()
  await page.getByTestId('confirm-remove-last').click()
  await expect(dialog.getByTestId('no-teachers')).toBeVisible()

  // Kursraden visar att kursen saknar lärare, utan omladdning.
  await dialog.getByRole('button', {name: 'Stäng'}).click()
  await expect(row.getByTestId('course-teacher-names')).toHaveCount(0)
})
