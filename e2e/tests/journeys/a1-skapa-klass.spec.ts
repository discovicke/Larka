import {expect, test} from '../../fixtures'
import {uid} from '../../journeys'

// A1: admin skapar en klass och lägger in elever genom att klistra in en lista.
test('A1: admin skapar en klass och lägger till elever', async ({pageAs}) => {
  const page = await pageAs('admin')
  const tag = uid()
  const className = `Klass ${tag}`
  const students = [`Siv ${tag}`, `Stig ${tag}`]

  // Ny klass från Alla grupper. Efter sparandet öppnas klassens sida.
  await page.goto('/groups')
  await page.getByTestId('new-menu').click()
  await page.getByTestId('new-class').click()
  // Dialogen visar vilka skoladresser klassen fångar, och varnar när namnet inte ger någon kod.
  await page.getByTestId('group-name').fill('.NET99')
  await expect(page.getByTestId('group-code-hint')).toContainText('.net99@')
  await page.getByTestId('group-name').fill('NET 99 DevOps')
  await expect(page.getByTestId('group-code-hint-missing')).toBeVisible()
  await page.getByTestId('group-name').fill(className)
  await page.getByTestId('group-term').fill('HT26')
  await page.getByTestId('save-group').click()
  await expect(page).toHaveURL(/\/groups\/[0-9a-f-]+$/)
  await expect(page.getByRole('heading', {name: className, level: 1})).toBeVisible()
  // En ny klass har inga elever än, och Att ordna säger vad som återstår.
  await expect(page.getByTestId('group-setup')).toBeVisible()

  // Eleverna klistras in som namn och e-post, en per rad.
  await page.getByTestId('add-member').click()
  await page.getByTestId('show-paste').click()
  await page.getByTestId('import-text').fill(students.map((name, index) => `${name}, elev${index}.${tag}@e2e.test`).join('\n'))
  await expect(page.getByTestId('import-row')).toHaveCount(2)
  await page.getByTestId('confirm-import').click()
  await expect(page.getByTestId('import-report-row')).toHaveCount(2)
  await page.getByTestId('import-close').click()

  // Eleverna står nu i klassen.
  for (const name of students) {
    await expect(page.getByTestId('member-row').filter({hasText: name})).toBeVisible()
  }
})
