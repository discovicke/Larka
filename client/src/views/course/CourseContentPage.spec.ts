import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi} from '@/test/mockApi'
import {mentorAs, meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const part = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Theory',
  gradeScale: null,
  title: 'Teori A',
  status: 'Published',
  groupId: null,
  publishAt: '2026-01-01T08:00:00Z',
  dueAt: null,
  isPinned: false,
  blockedBy: [],
  bodyMd: 'Läs kapitel 1',
  isLocked: false,
  isCompleted: false,
  canMarkDone: true,
  submissionStatus: null,
  gradeLevel: null,
}
const week = {id: 'm1', courseId: 'c1', title: 'Vecka 1', description: '', position: 1, publishAt: null, isAvailable: true, content: [part]}

function backend(me: ReturnType<typeof meAs>) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: me}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules' || path === '/api/courses/c1/student-modules') {
      return {status: 200, body: [week]}
    }
    if (path === '/api/content/x1') {
      return {status: 200, body: {id: 'x1', type: 'Theory', title: 'Teori A', bodyMd: 'Läs kapitel 1'}}
    }
    return {status: 200, body: []}
  })
}

describe('CourseContentPage', () => {
  it('skickar en lärare med ?del= vidare till delens sida', async () => {
    backend(meAs('Teacher', ['c1']))

    const {router} = await renderApp('/courses/c1/content?del=l1')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('part')
    expect(router.currentRoute.value.fullPath).toBe('/courses/c1/parts/l1')
  })

  it('skickar en mentor med ?del= vidare till delens sida', async () => {
    backend(mentorAs(['c1']))

    const {router} = await renderApp('/courses/c1/content?del=l1')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('part')
    expect(router.currentRoute.value.params.linkId).toBe('l1')
  })

  it('låter en elev stanna kvar på kurssidan med ?del=', async () => {
    backend(meAs('Student', ['c1']))

    const {router} = await renderApp('/courses/c1/content?del=l1')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('course-content')
    expect(router.currentRoute.value.query.del).toBe('l1')
  })

  it('låter en lärare utan ?del= stanna på kursens redigerare', async () => {
    backend(meAs('Teacher', ['c1']))

    const {router} = await renderApp('/courses/c1/content')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('course-content')
  })
})
