import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import CourseFormDialog from './CourseFormDialog.vue'

import type {Schema} from '@/lib/api'
import {mockApi, requests, sent} from '@/test/mockApi'

enableAutoUnmount(afterEach)

const existing: Schema<'CourseDto'> = {
  id: 'c1',
  schoolId: 's1',
  code: 'DEVOPS',
  title: 'DevOps',
  description: 'Om kursen',
  points: 25,
  startsOn: '2026-08-24',
  endsOn: null,
}

async function render() {
  const wrapper = mount(CourseFormDialog, {
    props: {open: true, course: existing},
    attachTo: document.body,
  })
  await flushPromises()
  return wrapper
}

function field(name: string): HTMLInputElement {
  return document.querySelector(`[data-test="${name}"]`) as HTMLInputElement
}

async function type(name: string, value: string) {
  const element = field(name)
  element.value = value
  element.dispatchEvent(new Event('input'))
  await flushPromises()
}

async function submit() {
  field('save-course').click()
  await flushPromises()
}

describe('CourseFormDialog', () => {
  it('kräver en titel och skickar inget förrän den finns', async () => {
    mockApi(() => ({status: 200, body: existing}))
    await render()

    await type('course-title', ' ')
    await submit()

    expect(document.querySelector('[data-test="course-title-error"]')?.textContent).toBe('Ange en titel.')
    expect(requests).toHaveLength(0)
  })

  it('stoppar omfattning utanför 1–400', async () => {
    mockApi(() => ({status: 200, body: existing}))
    await render()

    await type('course-points', '500')
    await submit()

    expect(document.querySelector('[data-test="course-points-error"]')?.textContent)
      .toBe('Omfattningen ska vara mellan 1 och 400 poäng.')
    expect(requests).toHaveLength(0)
  })

  it('låser koden och låter beskrivningen följa med oförändrad', async () => {
    mockApi(() => ({status: 200, body: {...existing, title: 'DevOps 2'}}))
    const wrapper = await render()

    expect(field('course-code').disabled).toBe(true)
    expect(field('course-code').value).toBe('DEVOPS')
    expect(field('course-points').value).toBe('25')
    await type('course-title', 'DevOps 2')
    await submit()

    expect(sent('PUT', '/api/courses/c1')).toEqual({
      title: 'DevOps 2', description: 'Om kursen', points: 25, startsOn: '2026-08-24', endsOn: null,
    })
    expect(wrapper.emitted('saved')?.[0]).toEqual([{...existing, title: 'DevOps 2'}])
  })

  it('visar felet från API:et och stänger inte', async () => {
    mockApi(() => ({status: 400, body: {title: 'något gick fel'}}))
    const wrapper = await render()

    await submit()

    expect(document.querySelector('[data-test="course-form-error"]')).not.toBeNull()
    expect(wrapper.emitted('saved')).toBeUndefined()
    expect(wrapper.emitted('update:open')).toBeUndefined()
  })
})
