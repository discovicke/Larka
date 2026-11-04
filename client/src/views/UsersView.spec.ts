import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const me = {id: 'u-me', schoolId: 's1', name: 'Anna Lärare', email: 'anna@example.com', role: 'Admin'}
const elev = {
  id: 'u1', schoolId: 's1', name: 'Elin Elev', email: 'elin@example.com', role: 'Student', microsoftLinked: true,
}
const larare = {id: 'u2', schoolId: 's1', name: 'Lars Lärare', email: 'lars@example.com', role: 'Teacher'}
const sparrad = {
  id: 'u3', schoolId: 's1', name: 'Sara Spärrad', email: 'sara@example.com', role: 'Student',
  deactivatedAt: '2026-11-01T10:00:00Z',
}

function backend({role = 'Admin', roleStatus = 200}: {role?: string, roleStatus?: number} = {}) {
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs(role as 'Admin')}
    }
    if (method === 'GET' && path === '/api/users') {
      return {status: 200, body: [me, elev, larare, sparrad]}
    }
    if (method === 'POST' && path === '/api/users') {
      return {status: 201, body: {id: 'u9', ...(body as object)}}
    }
    if (method === 'POST' && path === '/api/users/u1/deactivate') {
      return {status: 200, body: {...elev, deactivatedAt: '2026-11-02T10:00:00Z'}}
    }
    if (method === 'DELETE' && path === '/api/users/u1/microsoft') {
      return {status: 200, body: {...elev, microsoftLinked: false}}
    }
    if (method === 'POST' && path === '/api/users/u3/reactivate') {
      return {status: 200, body: {...sparrad, deactivatedAt: null}}
    }
    if (method === 'PUT' && path === '/api/users/u1/role') {
      return {status: roleStatus, body: roleStatus === 200
        ? {...elev, ...(body as object)}
        : 'you cannot change your own role'}
    }
    if (method === 'PUT' && path === '/api/users/u2/role') {
      return {status: 409, body: 'the user still teaches a course or a class'}
    }
    return {status: 404}
  })
}

function rows(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  return wrapper.findAll('[data-test="user-row"]')
}

function dialog(): HTMLElement {
  return document.querySelector('[role="dialog"]') as HTMLElement
}

async function click(element: Element | null) {
  ;(element as HTMLElement).click()
  await flushPromises()
}

async function openChangeRole(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], name: string) {
  await wrapper.find(`[aria-label="Åtgärder för ${name}"]`).trigger('keydown', {key: 'Enter'})
  await flushPromises()
  await click(document.querySelector('[data-test="row-change-role"]'))
}

async function chooseRole(value: string) {
  const select = dialog().querySelector('[data-test="user-role"]') as HTMLSelectElement
  select.value = value
  select.dispatchEvent(new Event('change'))
  await flushPromises()
}

describe('UsersView', () => {
  it('visar skolans användare med e-post och roll på svenska', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    expect(rows(wrapper).map((row) => row.find('td').text().replace(/\s+/g, ' ')))
      .toEqual(['Anna Lärare (du)', 'Elin Elev', 'Lars Lärare', 'Sara Spärrad Spärrad'])
    expect(wrapper.findAll('[data-test="user-role-cell"]').map((cell) => cell.text()))
      .toEqual(['Admin', 'Elev', 'Lärare', 'Elev'])
    expect(rows(wrapper)[1].text()).toContain('elin@example.com')
  })

  it('har ingen meny på den egna raden, eftersom ingen kan ändra sin egen roll', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    expect(wrapper.find('[aria-label="Åtgärder för Anna Lärare"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-test="row-actions"]')).toHaveLength(3)
  })

  it('sökningen och flikarna filtrerar listan', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    await wrapper.find('#user-search').setValue('LARS@')
    expect(rows(wrapper).map((row) => row.find('td').text().replace(/\s+/g, ' '))).toEqual(['Lars Lärare'])

    await wrapper.find('#user-search').setValue('')
    await wrapper.findAll('[role="tab"]').find((tab) => tab.text() === 'Elever')?.trigger('click')
    expect(rows(wrapper).map((row) => row.find('td').text().replace(/\s+/g, ' '))).toEqual(['Elin Elev', 'Sara Spärrad Spärrad'])

    await wrapper.findAll('[role="tab"]').find((tab) => tab.text() === 'Spärrade')?.trigger('click')
    expect(rows(wrapper).map((row) => row.find('td').text().replace(/\s+/g, ' '))).toEqual(['Sara Spärrad Spärrad'])
  })

  it('öppnas med filtret från länken på Idag', async () => {
    backend()
    const {wrapper} = await renderApp('/users?filter=Student')

    expect(rows(wrapper).map((row) => row.find('td').text().replace(/\s+/g, ' '))).toEqual(['Elin Elev', 'Sara Spärrad Spärrad'])
  })

  it('Spärra frågar först och markerar sedan raden som spärrad', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    await wrapper.find('[aria-label="Åtgärder för Elin Elev"]').trigger('keydown', {key: 'Enter'})
    await flushPromises()
    await click(document.querySelector('[data-test="row-deactivate"]'))
    expect(dialog().textContent).toContain('Spärra Elin Elev?')
    await click(dialog().querySelector('[data-test="confirm-deactivate"]'))

    expect(requests.some((request) => request.method === 'POST' && request.path === '/api/users/u1/deactivate'))
      .toBe(true)
    expect(rows(wrapper)[1].find('[data-test="deactivated-badge"]').exists()).toBe(true)
  })

  it('visar vem som har loggat in med Microsoft och kan koppla bort det', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    expect(wrapper.findAll('[data-test="user-microsoft-cell"]').map((cell) => cell.text()))
      .toEqual(['–', 'Kopplad', '–', '–'])

    await wrapper.find('[aria-label="Åtgärder för Elin Elev"]').trigger('keydown', {key: 'Enter'})
    await flushPromises()
    await click(document.querySelector('[data-test="row-unlink"]'))
    await click(dialog().querySelector('[data-test="confirm-unlink"]'))

    expect(wrapper.findAll('[data-test="user-microsoft-cell"]')[1].text()).toBe('–')
  })

  it('Återaktivera släpper spärren', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    await wrapper.find('[aria-label="Åtgärder för Sara Spärrad"]').trigger('keydown', {key: 'Enter'})
    await flushPromises()
    expect(document.querySelector('[data-test="row-deactivate"]')).toBeNull()
    await click(document.querySelector('[data-test="row-reactivate"]'))
    await click(dialog().querySelector('[data-test="confirm-deactivate"]'))

    expect(rows(wrapper)[3].find('[data-test="deactivated-badge"]').exists()).toBe(false)
  })

  it('Ändra roll gör en elev till lärare och uppdaterar raden', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    await openChangeRole(wrapper, 'Elin Elev')
    await chooseRole('Teacher')
    await click(dialog().querySelector('[data-test="save-role"]'))

    expect(sent('PUT', '/api/users/u1/role')).toEqual({role: 'Teacher'})
    expect(wrapper.findAll('[data-test="user-role-cell"]')[1].text()).toBe('Lärare')
  })

  it('säger att en lärare först ersätts i sina kurser, och visar varför servern säger nej', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    await openChangeRole(wrapper, 'Lars Lärare')
    expect(dialog().querySelector('[data-test="demote-note"]')).toBeNull()

    await chooseRole('Admin')
    expect(dialog().querySelector('[data-test="demote-note"]')?.textContent)
      .toContain('måste du först ersätta hen där')
    await click(dialog().querySelector('[data-test="save-role"]'))

    expect(dialog().querySelector('[data-test="change-role-error"]')?.textContent)
      .toContain('Hen undervisar fortfarande i en kurs eller klass.')
    expect(wrapper.findAll('[data-test="user-role-cell"]')[2].text()).toBe('Lärare')
  })

  it('visar felet när API:et säger nej', async () => {
    backend({roleStatus: 409})
    const {wrapper} = await renderApp('/users')

    await openChangeRole(wrapper, 'Elin Elev')
    await chooseRole('Admin')
    await click(dialog().querySelector('[data-test="save-role"]'))

    expect(dialog().querySelector('[data-test="change-role-error"]')?.textContent)
      .toContain('Du kan inte ändra din egen roll.')
  })

  describe('Ny användare', () => {
    async function create(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], name: string, role: string) {
      await click(wrapper.find('[data-test="new-user"]').element)
      for (const [field, value] of [['new-user-name', name], ['new-user-email', 'ny@example.com']]) {
        const input = dialog().querySelector(`[data-test="${field}"]`) as HTMLInputElement
        input.value = value
        input.dispatchEvent(new Event('input'))
      }
      const select = dialog().querySelector('[data-test="new-user-role"]') as HTMLSelectElement
      select.value = role
      select.dispatchEvent(new Event('change'))
      await flushPromises()
      await click(dialog().querySelector('[data-test="confirm-create-user"]'))
    }

    it('admin skapar en användare utan grupp och hen står först i listan', async () => {
      backend()
      const {wrapper} = await renderApp('/users')

      await create(wrapper, 'Ada Admin', 'Admin')

      expect(sent('POST', '/api/users')).toEqual({
        schoolId: 's1', name: 'Ada Admin', email: 'ny@example.com', role: 'Admin', password: null,
      })
      expect(requests.some((request) => request.path.startsWith('/api/groups'))).toBe(false)
      expect(dialog()).toBeNull()
      expect(rows(wrapper)).toHaveLength(5)
      expect(rows(wrapper)[0].text()).toContain('Ada Admin')
      expect(rows(wrapper)[0].find('[data-test="user-role-cell"]').text()).toBe('Admin')
    })

    it('visar alla igen när filtret skulle dölja den nya användaren', async () => {
      backend()
      const {wrapper} = await renderApp('/users?filter=Student')
      expect(rows(wrapper)).toHaveLength(2)

      await create(wrapper, 'Tove Lärare', 'Teacher')

      expect(rows(wrapper)).toHaveLength(5)
      expect(rows(wrapper)[0].text()).toContain('Tove Lärare')
    })
  })

  it('en lärare kommer inte åt sidan och ser ingen länk dit', async () => {
    backend({role: 'Teacher'})
    const {wrapper, router} = await renderApp('/users')

    expect(router.currentRoute.value.name).not.toBe('users')
    expect(wrapper.find('[data-test="users-link"]').exists()).toBe(false)
  })

  it('admin ser länken Användare i menyn', async () => {
    backend()
    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="users-link"]').exists()).toBe(true)
  })
})
