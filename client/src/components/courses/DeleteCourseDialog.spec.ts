import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import DeleteCourseDialog from './DeleteCourseDialog.vue'

import type {Schema} from '@/lib/api'
import {mockApi, requests} from '@/test/mockApi'

enableAutoUnmount(afterEach)

const course: Schema<'CourseDto'> = {
  id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps', description: '', points: null, startsOn: null, endsOn: null,
}

async function confirm() {
  const wrapper = mount(DeleteCourseDialog, {props: {open: true, course}, attachTo: document.body})
  await flushPromises()
  ;(document.querySelector('[data-test="confirm-delete-course"]') as HTMLElement).click()
  await flushPromises()
  return wrapper
}

describe('DeleteCourseDialog', () => {
  it('namnger kursen i frågan', async () => {
    mockApi(() => ({status: 204}))
    mount(DeleteCourseDialog, {props: {open: true, course}, attachTo: document.body})
    await flushPromises()

    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ta bort DevOps?')
  })

  it('tar bort kursen och stänger', async () => {
    mockApi(() => ({status: 204}))

    const wrapper = await confirm()

    expect(requests.map((request) => `${request.method} ${request.path}`)).toEqual(['DELETE /api/courses/c1'])
    expect(wrapper.emitted('deleted')?.[0]).toEqual([course])
    expect(wrapper.emitted('update:open')?.[0]).toEqual([false])
  })

  it('409 visar att kursen har elevarbete och håller dialogen öppen', async () => {
    mockApi(() => ({status: 409, body: 'the course has student work'}))

    const wrapper = await confirm()

    expect(document.querySelector('[data-test="delete-course-error"]')?.textContent)
      .toContain('Kursen har elevarbete och kan inte tas bort.')
    expect(wrapper.emitted('deleted')).toBeUndefined()
    expect(wrapper.emitted('update:open')).toBeUndefined()
  })

  it('visar andra fel från API:et', async () => {
    mockApi(() => ({status: 403, body: 'nej'}))

    await confirm()

    expect(document.querySelector('[data-test="delete-course-error"]')?.textContent)
      .toContain('Du har inte behörighet.')
  })
})
