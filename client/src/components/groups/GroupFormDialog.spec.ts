import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import GroupFormDialog from './GroupFormDialog.vue'

import type {Schema} from '@/lib/api'
import {mockApi, requests, sent} from '@/test/mockApi'
import {chooseOption, selectOptions} from '@/test/select'

enableAutoUnmount(afterEach)

const existing: Schema<'GroupDto'> = {
  id: 'g1', schoolId: 's1', type: 'Class', name: 'NET25', memberCount: 4, lastCourseEndsOn: null,
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
  it('Ny klass visar ingen typ', async () => {
    await render()

    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ny klass')
    expect(field('group-type')).toBeNull()
    expect(field('save-group').textContent?.trim()).toBe('Skapa klass')
  })

  it('Ny grupp erbjuder inte klass', async () => {
    await render(undefined, 'group')

    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ny grupp')
    expect(await selectOptions('[data-test="group-type"]')).toEqual(['Kommitté', 'Grupp'])
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

  it('skapar en grupp i skolan med trimmat namn', async () => {
    mockApi(() => ({status: 201, body: {...existing, id: 'g9', name: 'NET26', memberCount: 0}}))
    const wrapper = await render(undefined, 'group')

    await type('group-name', '  NET26 ')
    await chooseOption('[data-test="group-type"]', 'Kommitté')
    await submit()

    expect(sent('POST', '/api/groups')).toEqual({
      schoolId: 's1', type: 'Committee', name: 'NET26',
    })
    expect(wrapper.emitted('saved')?.[0][0]).toMatchObject({id: 'g9', name: 'NET26'})
    expect(wrapper.emitted('update:open')?.[0]).toEqual([false])
  })

  it('redigerar med PUT och förifyllda fält', async () => {
    mockApi(() => ({status: 200, body: {...existing, name: 'NET25B'}}))
    const wrapper = await render(existing)

    expect(field('group-name').value).toBe('NET25')
    expect(field('group-type').textContent?.trim()).toBe('Klass')

    await type('group-name', 'NET25B')
    await submit()

    expect(sent('PUT', '/api/groups/g1')).toEqual({type: 'Class', name: 'NET25B'})
    expect(wrapper.emitted('saved')?.[0][0]).toMatchObject({name: 'NET25B'})
  })

  it('visar API:ets felmeddelande och håller dialogen öppen', async () => {
    mockApi(() => ({status: 400, body: 'the school already has a group with this name'}))
    const wrapper = await render()

    await type('group-name', 'NET25')
    await submit()

    expect(document.querySelector('[data-test="group-form-error"]')?.textContent)
      .toContain('Det finns redan en klass med det namnet.')
    expect(wrapper.emitted('saved')).toBeUndefined()
    expect(wrapper.emitted('update:open')).toBeUndefined()
  })

  it('Ny klass visar vilken adresskod namnet fångar', async () => {
    await render()
    expect(field('group-name').value).toBe('')
    expect(document.querySelector('[data-test="group-code-hint"]')).toBeNull()

    await type('group-name', '.NET25')

    expect(document.querySelector('[data-test="group-code-hint"]')?.textContent)
      .toContain('Elever med adresser som slutar på .net25@… hamnar här.')
  })

  it('Ny klass varnar när namnet inte ger någon användbar kod', async () => {
    await render()

    await type('group-name', 'NET 25 DevOps')

    expect(document.querySelector('[data-test="group-code-hint"]')).toBeNull()
    expect(document.querySelector('[data-test="group-code-hint-missing"]')?.textContent)
      .toContain('ger ingen kod')
  })

  it('Redigera klass visar koden från namnet och följer med när det ändras', async () => {
    await render(existing)

    expect(document.querySelector('[data-test="group-code-hint"]')?.textContent).toContain('.net25@…')

    await type('group-name', 'NET 26')

    expect(document.querySelector('[data-test="group-code-hint"]')?.textContent).toContain('.net26@…')
  })

  it('en grupp som inte är en klass visar ingen adresskod', async () => {
    await render(undefined, 'group')

    await type('group-name', 'Elevråd')

    expect(document.querySelector('[data-test="group-code-hint"]')).toBeNull()
    expect(document.querySelector('[data-test="group-code-hint-missing"]')).toBeNull()
  })
})
