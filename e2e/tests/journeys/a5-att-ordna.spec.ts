import {expect, test} from '../../fixtures'
import {createUser, createWorld, json} from '../../journeys'

// A5: admin ser på Idag vad som återstår att ordna, räknat per klass, och varje rad länkar dit det går att åtgärda.
test('A5: admin ser Att ordna per klass med länkar', async ({apiAs, pageAs}) => {
  const admin = await apiAs('admin')
  const world = await createWorld(admin, {students: 0})
  // Två elever i klassen som inte kan logga in, och en elev utan klass som inte heller kan det.
  for (const index of [1, 2]) {
    const user = await createUser(admin, `Utan lösen ${index} ${world.tag}`, 'Student', false)
    await json(await admin.post(`/api/groups/${world.group.id}/members`, {data: {userId: user.id, role: 'Student'}}))
  }
  await createUser(admin, `Utan klass ${world.tag}`, 'Student', false)

  const page = await pageAs('admin')
  await page.goto('/')
  const dashboard = page.getByTestId('admin-dashboard')

  // Klassen saknar mentor och länkar till klassens sida.
  const noMentor = dashboard.getByTestId('no-mentor-item').filter({hasText: world.group.name})
  await expect(noMentor).toBeVisible()

  // Användare utan lösenord räknas per klass, inte per användare.
  const noPassword = dashboard.getByTestId('no-password-item').filter({hasText: world.group.name})
  await expect(noPassword).toContainText('2 användare')

  // Raden för användare utan klass går till Användare.
  await expect(dashboard.getByTestId('no-password-unplaced')).toBeVisible()
  await dashboard.getByTestId('no-password-unplaced').click()
  await expect(page).toHaveURL(/\/users/)

  // Raderna leder till klassens sida, där åtgärden finns.
  await page.goto('/')
  await dashboard.getByTestId('no-password-item').filter({hasText: world.group.name}).getByRole('link').click()
  await expect(page).toHaveURL(new RegExp(`/groups/${world.group.id}$`))
  await expect(page.getByTestId('set-mentor')).toBeVisible()
})
