import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import GroupFormDialog from './GroupFormDialog.vue'

import type {Schema} from '@/lib/api'
import {mockApi, requests, sent} from '@/test/mockApi'

enableAutoUnmount(afterEach)

const existing: Schema<'GroupDto'> = {
  id: 'g1', schoolId: 's1', type: 'Class', name: 'NET25', term: 'HT25', memberCount: 4,
}

async function render(group?: Schema<'GroupDto'>, kind: 'class' | 'group' = 'class') {
  const wrapper = mount(GroupFormDialog, {
    props: {open: true, schoolId: 's1', group, kind},
    attachTo: document.body,
  })
  await flushPromises()
  return wrapper
}

function field(name: string): HTMLInputElement | HTMLSelectElement {
  return document.querySelector(`[data-test="${name}"]`) as HTMLInputElement
}

async function type(name: string, value: string, event = 'input') {
  const element = field(name)
  element.value = value
  element.dispatchEvent(new Event(event))
  await flushPromises()
}

async function submit() {
  field('save-group').click()
  await flushPromises()
}

describe('GroupFormDialog', () => {
  it('Ny klass frågar efter starttermin och visar ingen typ', async () => {
    await render()

    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ny klass')
    expect(document.querySelector('label[for="group-term"]')?.textContent?.trim()).toBe('Startade (termin)')
    expect(field('group-type')).toBeNull()
    expect(field('save-group').textContent?.trim()).toBe('Skapa klass')
  })

  it('Ny grupp frågar efter period och erbjuder inte klass', async () => {
    await render(undefined, 'group')

    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ny grupp')
    expect(document.querySelector('label[for="group-term"]')?.textContent?.trim()).toBe('Period/termin')
    expect([...field('group-type').querySelectorAll('option')].map((option) => option.textContent?.trim()))
      .toEqual(['Kommitté', 'Grupp'])
    expect(field('save-group').textContent?.trim()).toBe('Skapa grupp')
  })

  it('kräver ett namn och skickar inget förrän det finns', async () => {
    mockApi(() => ({status: 201, body: existing}))
    await render()

    await type('group-name', '   ')
    await submit()

    expect(document.querySelector('[data-test="group-name-error"]')?.textContent).toBe('Ange ett namn.')
    expect(requests).toHaveLength(0)
  })

  it('skapar en grupp i skolan med trimmat namn och period', async () => {
    mockApi(() => ({status: 201, body: {...existing, id: 'g9', name: 'NET26', memberCount: 0}}))
    const wrapper = await render(undefined, 'group')

    await type('group-name', '  NET26 ')
    await type('group-type', 'Committee', 'change')
    await type('group-term', ' HT26 ')
    await submit()

    expect(sent('POST', '/api/groups')).toEqual({
      schoolId: 's1', type: 'Committee', name: 'NET26', term: 'HT26',
    })
    expect(wrapper.emitted('saved')?.[0][0]).toMatchObject({id: 'g9', name: 'NET26'})
    expect(wrapper.emitted('update:open')?.[0]).toEqual([false])
  })

  it('skickar tom period som null', async () => {
    mockApi(() => ({status: 201, body: existing}))
    await render()

    await type('group-name', 'Elevråd')
    await submit()

    expect(sent('POST', '/api/groups')).toEqual({schoolId: 's1', type: 'Class', name: 'Elevråd', term: null})
  })

  it('redigerar med PUT och förifyllda fält', async () => {
    mockApi(() => ({status: 200, body: {...existing, name: 'NET25B'}}))
    const wrapper = await render(existing)

    expect(field('group-name').value).toBe('NET25')
    expect(field('group-type').value).toBe('Class')
    expect(field('group-term').value).toBe('HT25')

    await type('group-name', 'NET25B')
    await submit()

    expect(sent('PUT', '/api/groups/g1')).toEqual({type: 'Class', name: 'NET25B', term: 'HT25'})
    expect(wrapper.emitted('saved')?.[0][0]).toMatchObject({name: 'NET25B'})
  })

  it('visar API:ets felmeddelande och håller dialogen öppen', async () => {
    mockApi(() => ({status: 400, body: 'the school already has a group with this name and term'}))
    const wrapper = await render()

    await type('group-name', 'NET25')
    await submit()

    expect(document.querySelector('[data-test="group-form-error"]')?.textContent)
      .toContain('the school already has a group with this name and term')
    expect(wrapper.emitted('saved')).toBeUndefined()
    expect(wrapper.emitted('update:open')).toBeUndefined()
  })
})
