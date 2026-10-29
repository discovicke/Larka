import {flushPromises, mount, type VueWrapper} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import RemoveMemberDialog from '@/components/groups/RemoveMemberDialog.vue'
import {mockApi, requests} from '@/test/mockApi'

const member = {userId: 'u1', name: 'Siv Student', role: 'Student' as const}

// Dialogen portas till body, så den måste tas bort mellan testerna.
const mounted: VueWrapper[] = []

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
})

async function openDialog(status: number) {
  mockApi(() => ({status, body: status === 204
    ? undefined
    : 'fel'}))
  const wrapper = mount(RemoveMemberDialog, {
    props: {open: true, groupId: 'g1', groupName: 'TE22A', member},
    attachTo: document.body,
  })
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

const confirm = () => document.querySelector('[data-test="confirm-remove-member"]') as HTMLElement

describe('RemoveMemberDialog', () => {
  it('frågar och tar bort medlemmen vid bekräftelse', async () => {
    const wrapper = await openDialog(204)
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ta bort Siv Student från TE22A?')

    confirm().click()
    await flushPromises()

    expect(requests.at(-1)).toMatchObject({method: 'DELETE', path: '/api/groups/g1/members/u1'})
    expect(wrapper.emitted('removed')?.[0]).toEqual(['u1'])
  })

  it('visar felet när borttagningen misslyckas', async () => {
    const wrapper = await openDialog(500)

    confirm().click()
    await flushPromises()

    expect(document.querySelector('[role="alert"]')?.textContent).toContain('fel')
    expect(wrapper.emitted('removed')).toBeUndefined()
  })
})
