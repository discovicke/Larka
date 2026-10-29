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

async function render(course?: Schema<'CourseDto'>) {
  const wrapper = mount(CourseFormDialog, {
    props: {open: true, schoolId: 's1', course},
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
  it('kräver titel och kod och skickar inget förrän de finns', async () => {
    mockApi(() => ({status: 201, body: existing}))
    await render()

    await submit()

    expect(document.querySelector('[data-test="course-title-error"]')?.textContent).toBe('Ange en titel.')
    expect(document.querySelector('[data-test="course-code-error"]')?.textContent).toBe('Ange en kurskod.')
    expect(requests).toHaveLength(0)
  })

  it('stoppar omfattning utanför 1–400', async () => {
    mockApi(() => ({status: 201, body: existing}))
    await render()

    await type('course-title', 'DevOps')
    await type('course-code', 'DEVOPS')
    await type('course-points', '500')
    await submit()

    expect(document.querySelector('[data-test="course-points-error"]')?.textContent)
      .toBe('Omfattningen ska vara mellan 1 och 400 poäng.')
    expect(requests).toHaveLength(0)
  })

  it('skapar en kurs i skolan med trimmad titel och kod', async () => {
    mockApi(() => ({status: 201, body: {...existing, id: 'c9', title: 'Java', code: 'JAVA'}}))
    const wrapper = await render()

    await type('course-title', '  Java ')
    await type('course-code', ' JAVA ')
    await type('course-points', '30')
    await submit()

    expect(sent('POST', '/api/courses')).toEqual({
      schoolId: 's1', title: 'Java', code: 'JAVA', points: 30, startsOn: null, endsOn: null,
    })
    expect(wrapper.emitted('saved')?.[0]).toEqual([{...existing, id: 'c9', title: 'Java', code: 'JAVA'}])
    expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
  })

  it('i redigeringsläge är koden låst och beskrivningen följer med oförändrad', async () => {
    mockApi(() => ({status: 200, body: {...existing, title: 'DevOps 2'}}))
    const wrapper = await render(existing)

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
    mockApi(() => ({status: 400, body: {title: 'the school already has a course with this code'}}))
    const wrapper = await render()

    await type('course-title', 'Java')
    await type('course-code', 'JAVA')
    await submit()

    expect(document.querySelector('[data-test="course-form-error"]')).not.toBeNull()
    expect(wrapper.emitted('saved')).toBeUndefined()
    expect(wrapper.emitted('update:open')).toBeUndefined()
  })
})
