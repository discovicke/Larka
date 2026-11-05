import {flushPromises, mount, type VueWrapper} from '@vue/test-utils'
import {afterEach, describe, expect, it, vi} from 'vitest'

import AddMemberDialog from '@/components/groups/AddMemberDialog.vue'
import {mockApi, requests, sent} from '@/test/mockApi'
import {chooseOption} from '@/test/select'

const users = [
  {id: 'u1', schoolId: 's1', name: 'Siv Student', email: 'siv@example.com', role: 'Student'},
  {id: 'u2', schoolId: 's1', name: 'Lars Lärare', email: 'lars@example.com', role: 'Teacher'},
]

// Dialogen portas till body, så den måste tas bort mellan testerna.
const mounted: VueWrapper[] = []

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
})

async function openDialog(status = 201, createStatus = 201) {
  mockApi((method, path, body) => {
    if (method === 'GET' && path === '/api/users') {
      return {status: 200, body: users}
    }
    if (method === 'POST' && path === '/api/users') {
      return createStatus === 201
        ? {status: 201, body: {id: 'u9', schoolId: 's1', name: (body as {name: string}).name, email: 'ny@example.com', role: 'Student'}}
        : {status: createStatus, body: 'email is already in use'}
    }
    return {status, body: status === 201
      ? {}
      : 'fel'}
  })
  const wrapper = mount(AddMemberDialog, {
    props: {open: false, groupId: 'g1', groupName: 'TE22A', schoolId: 's1', memberIds: ['u1']},
    attachTo: document.body,
  })
  await wrapper.setProps({open: true})
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

const field = (name: string) => document.querySelector(`[data-test="${name}"]`) as HTMLSelectElement

async function choose(name: string, value: string) {
  await chooseOption(`[data-test="${name}"]`, {Student: 'Elev', Teacher: 'Lärare', Admin: 'Admin'}[value] ?? value)
}

// Användarna i listan under sökfältet, och ett klick på en av dem.
const shown = () => [...document.querySelectorAll<HTMLInputElement>('[data-test="member-user-radio"]')].map((radio) => radio.value)

async function pickUser(id: string) {
  document.querySelector<HTMLInputElement>(`[data-test="member-user-radio"][value="${id}"]`)!.click()
  await flushPromises()
}

describe('AddMemberDialog', () => {
  it('listar bara användare som inte redan är medlemmar och har Elev som standardroll', async () => {
    await openDialog()

    expect(shown()).toEqual(['u2'])
    expect(field('member-role-select').textContent?.trim()).toBe('Elev')
    expect(field('confirm-add-member').hasAttribute('disabled')).toBe(true)
  })

  it('skickar vald användare och roll och meddelar att medlemmen lades till', async () => {
    const wrapper = await openDialog()

    await pickUser('u2')
    await choose('member-role-select', 'Teacher')
    field('confirm-add-member').click()
    await flushPromises()

    expect(sent('POST', '/api/groups/g1/members')).toEqual({userId: 'u2', role: 'Teacher'})
    await vi.waitFor(() => expect(wrapper.emitted('added')).toBeDefined())
    expect(wrapper.emitted('added')?.[0]).toEqual([{userId: 'u2', name: 'Lars Lärare', role: 'Teacher'}])
    expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
  })

  it('visar felet och stänger inte vid misslyckande', async () => {
    const wrapper = await openDialog(500)

    await pickUser('u2')
    field('confirm-add-member').click()
    await flushPromises()

    expect(document.querySelector('[role="alert"]')?.textContent).toContain('fel')
    expect(wrapper.emitted('added')).toBeUndefined()
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(1)
  })

  describe('skapa användare', () => {
    async function fill(values: {name?: string, email?: string, password?: string, schoolRole?: string}) {
      for (const [name, key] of [['new-user-name', 'name'], ['new-user-email', 'email'], ['new-user-password', 'password']] as const) {
        const value = values[key]
        if (value !== undefined) {
          const element = field(name) as unknown as HTMLInputElement
          element.value = value
          element.dispatchEvent(new Event('input'))
        }
      }
      if (values.schoolRole) {
        await choose('new-user-role', values.schoolRole)
      }
      await flushPromises()
    }

    async function openCreate(status = 201, createStatus = 201) {
      const wrapper = await openDialog(status, createStatus)
      field('show-create-user').click()
      await flushPromises()
      return wrapper
    }

    it('länken byter till formuläret och tillbaka till listan', async () => {
      await openDialog()
      expect(field('new-user-name')).toBeNull()

      field('show-create-user').click()
      await flushPromises()
      expect(field('new-user-name')).not.toBeNull()
      expect(field('member-user')).toBeNull()
      expect(field('confirm-add-member').hasAttribute('disabled')).toBe(false)

      field('show-pick-user').click()
      await flushPromises()
      expect(field('member-user')).not.toBeNull()
    })

    it('kräver namn och giltig e-post och kontrollerar lösenordets längd', async () => {
      await openCreate()

      field('confirm-add-member').click()
      await flushPromises()
      expect(document.querySelector('[data-test="new-user-name-error"]')?.textContent).toBe('Ange ett namn.')
      expect(document.querySelector('[data-test="new-user-email-error"]')?.textContent)
        .toBe('Ange en giltig e-postadress.')

      await fill({name: 'Ny Elev', email: 'ny@example.com', password: 'kort'})
      field('confirm-add-member').click()
      await flushPromises()
      expect(document.querySelector('[data-test="new-user-password-error"]')?.textContent)
        .toBe('Lösenordet måste vara minst 8 tecken.')
      expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)
    })

    it('skapar användaren och lägger till hen i gruppen med vald grupproll', async () => {
      const wrapper = await openCreate()

      await fill({name: ' Ny Elev ', email: 'ny@example.com', password: 'hemligt123', schoolRole: 'Teacher'})
      await choose('member-role-select', 'Teacher')
      field('confirm-add-member').click()
      await flushPromises()

      expect(sent('POST', '/api/users')).toEqual({
        schoolId: 's1', name: 'Ny Elev', email: 'ny@example.com', role: 'Teacher', password: 'hemligt123',
      })
      expect(sent('POST', '/api/groups/g1/members')).toEqual({userId: 'u9', role: 'Teacher'})
      expect(wrapper.emitted('added')?.[0]).toEqual([{userId: 'u9', name: 'Ny Elev', role: 'Teacher'}])
      expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
    })

    it('utan lösenord skickas inget lösenord', async () => {
      await openCreate()

      await fill({name: 'Ny Elev', email: 'ny@example.com'})
      field('confirm-add-member').click()
      await flushPromises()

      expect((sent('POST', '/api/users') as {password: unknown}).password).toBeNull()
    })

    it('visar att e-posten redan används och lägger inte till någon', async () => {
      const wrapper = await openCreate(201, 400)

      await fill({name: 'Ny Elev', email: 'siv@example.com'})
      field('confirm-add-member').click()
      await flushPromises()

      expect(document.querySelector('[role="alert"]')?.textContent)
        .toContain('E-postadressen används redan av en annan användare.')
      expect(requests.some((request) => request.path === '/api/groups/g1/members')).toBe(false)
      expect(wrapper.emitted('added')).toBeUndefined()
    })

    it('om gruppen inte kan nås efter att användaren skapats skapas hen inte en gång till', async () => {
      const wrapper = await openCreate(500)

      await fill({name: 'Ny Elev', email: 'ny@example.com'})
      field('confirm-add-member').click()
      await flushPromises()

      expect(document.querySelector('[role="alert"]')?.textContent)
        .toContain('Användaren skapades men kunde inte läggas till i gruppen')
      field('confirm-add-member').click()
      await flushPromises()

      expect(requests.filter((request) => request.method === 'POST' && request.path === '/api/users')).toHaveLength(1)
      expect(requests.filter((request) => request.path === '/api/groups/g1/members')).toHaveLength(2)
      expect(wrapper.emitted('added')).toBeUndefined()
    })
  })
})
