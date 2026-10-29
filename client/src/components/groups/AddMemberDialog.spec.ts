import {flushPromises, mount, type VueWrapper} from '@vue/test-utils'
import {afterEach, describe, expect, it, vi} from 'vitest'

import AddMemberDialog from '@/components/groups/AddMemberDialog.vue'
import {mockApi, requests, sent} from '@/test/mockApi'

const users = [
  {id: 'u1', schoolId: 's1', name: 'Siv Student', email: 'siv@example.com', role: 'Student'},
  {id: 'u2', schoolId: 's1', name: 'Lars Lärare', email: 'lars@example.com', role: 'Teacher'},
]

// Dialogen portas till body, så den måste tas bort mellan testerna.
const mounted: VueWrapper[] = []

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
})

async function openDialog(status = 201) {
  mockApi((method, path) => {
    if (method === 'GET' && path === '/api/users') {
      return {status: 200, body: users}
    }
    return {status, body: status === 201
      ? {}
      : 'fel'}
  })
  const wrapper = mount(AddMemberDialog, {
    props: {open: false, groupId: 'g1', groupName: 'TE22A', memberIds: ['u1']},
    attachTo: document.body,
  })
  await wrapper.setProps({open: true})
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

const field = (name: string) => document.querySelector(`[data-test="${name}"]`) as HTMLSelectElement

async function choose(name: string, value: string) {
  const element = field(name)
  element.value = value
  element.dispatchEvent(new Event(name === 'member-search'
    ? 'input'
    : 'change'))
  await flushPromises()
}

describe('AddMemberDialog', () => {
  it('listar bara användare som inte redan är medlemmar och har Elev som standardroll', async () => {
    await openDialog()

    expect([...field('member-user').options].map((option) => option.value)).toEqual(['', 'u2'])
    expect(field('member-role-select').value).toBe('Student')
    expect(field('confirm-add-member').hasAttribute('disabled')).toBe(true)
  })

  it('skickar vald användare och roll och meddelar att medlemmen lades till', async () => {
    const wrapper = await openDialog()

    await choose('member-user', 'u2')
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

    await choose('member-user', 'u2')
    field('confirm-add-member').click()
    await flushPromises()

    expect(document.querySelector('[role="alert"]')?.textContent).toContain('fel')
    expect(wrapper.emitted('added')).toBeUndefined()
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(1)
  })
})
