import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import DeleteGroupDialog from './DeleteGroupDialog.vue'

import type {Schema} from '@/lib/api'
import {mockApi, requests} from '@/test/mockApi'

enableAutoUnmount(afterEach)

const group: Schema<'GroupDto'> = {
  id: 'g1', schoolId: 's1', type: 'Class', name: 'NET25', term: 'HT25', memberCount: 4,
}

async function confirm() {
  const wrapper = mount(DeleteGroupDialog, {props: {open: true, group}, attachTo: document.body})
  await flushPromises()
  ;(document.querySelector('[data-test="confirm-delete-group"]') as HTMLElement).click()
  await flushPromises()
  return wrapper
}

describe('DeleteGroupDialog', () => {
  it('namnger gruppen i frågan', async () => {
    mockApi(() => ({status: 204}))
    mount(DeleteGroupDialog, {props: {open: true, group}, attachTo: document.body})
    await flushPromises()

    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ta bort NET25?')
  })

  it('tar bort gruppen och stänger', async () => {
    mockApi(() => ({status: 204}))

    const wrapper = await confirm()

    expect(requests.map((request) => `${request.method} ${request.path}`)).toEqual(['DELETE /api/groups/g1'])
    expect(wrapper.emitted('deleted')?.[0]).toEqual([group])
    expect(wrapper.emitted('update:open')?.[0]).toEqual([false])
  })

  it('409 visar att gruppen har innehåll och håller dialogen öppen', async () => {
    mockApi(() => ({status: 409, body: 'the group has published content'}))

    const wrapper = await confirm()

    expect(document.querySelector('[data-test="delete-group-error"]')?.textContent)
      .toContain('Gruppen har publicerat innehåll och kan inte tas bort.')
    expect(wrapper.emitted('deleted')).toBeUndefined()
    expect(wrapper.emitted('update:open')).toBeUndefined()
  })
})
