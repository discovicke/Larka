import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import type {Schema} from '@/lib/api'
import {deadlineState} from '@/test/deadline'
import {mockApi, requests} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const devops = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const databases = {id: 'c2', schoolId: 's1', code: 'DB', title: 'Databaser'}
const names: Record<string, string> = {u0: 'Anna Lärare', u1: 'Anna Andersson', u2: 'Bo Berg', u3: 'Clara Carlsson'}

function assignment(linkId: string, contentId: string, title: string) {
  return {
    linkId,
    contentId,
    type: 'Assignment',
    gradeScale: 'PassFail',
    completionRule: null,
    title,
    status: 'Published',
    groupId: null,
    publishAt: '2026-10-01T08:00:00Z',
    dueAt: null as string | null,
    resubmitDueAt: [] as string[],
    isPinned: false,
    blockedBy: [],
  }
}

const release = assignment('l1', 'x1', 'Release-pipeline')
const theory = {
  ...assignment('l2', 'x2', 'Changelog'),
  type: 'Theory',
  gradeScale: null,
}
const docker = assignment('l3', 'x3', 'Docker-uppgift')

function week(id: string, content: unknown[]) {
  return {id, courseId: 'c1', title: 'Vecka 1', description: '', position: 1, content}
}

function submission(
  id: string,
  studentId: string,
  status: string,
  submittedAt: string | null = null,
  grade: unknown = null,
  deadline: Partial<Schema<'DeadlineStateDto'>> | null = null,
) {
  return {
    id,
    contentLinkId: 'l1',
    studentId,
    studentName: names[studentId],
    status,
    bodyMd: null,
    submittedAt,
    files: [],
    repo: null,
    grade,
    ...deadline
      ? {deadline: deadlineState(deadline)}
      : {},
  }
}

const releaseSubmissions = [
  submission('s1', 'u1', 'Submitted', '2026-10-10T10:00:00Z'),
  submission('s2', 'u2', 'Graded', '2026-10-09T10:00:00Z'),
  submission('s3', 'u3', 'NotStarted'),
]
const dockerSubmissions = [
  submission('s4', 'u2', 'Submitted', '2026-10-01T10:00:00Z'),
  submission('s5', 'u1', 'NeedsWork', '2026-10-05T10:00:00Z'),
]

function backend(options: {release?: unknown[], docker?: unknown[]} = {}) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1', 'c2'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [devops, databases]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: [week('m1', [release, theory])]}
    }
    if (path === '/api/courses/c2/modules') {
      return {status: 200, body: [week('m2', [docker])]}
    }
    if (path === '/api/links/l1/submissions') {
      return {status: 200, body: options.release ?? releaseSubmissions}
    }
    if (path === '/api/links/l3/submissions') {
      return {status: 200, body: options.docker ?? dockerSubmissions}
    }
    return {status: 404}
  })
}

function text(element: {text: () => string}): string {
  return element
    .text()
    .replace(/\s+/g, ' ')
    .trim()
}

describe('GradingView', () => {
  it('grupperar inlämnade per uppgift med namnen i ordning och en länk per rad', async () => {
    backend()

    const {wrapper} = await renderApp('/grading')
    await flushPromises()

    const groups = wrapper.findAll('[data-test="grading-group"]')
    expect(groups).toHaveLength(2)
    expect(text(groups[0])).toContain('Release-pipeline')
    expect(text(groups[0])).toContain('DevOps')
    expect(text(groups[0])).toContain('Ingen deadline')
    expect(text(groups[1])).toContain('Docker-uppgift')

    const rows = wrapper.findAll('[data-test="grading-row"]')
    expect(rows).toHaveLength(2)
    expect(text(rows[0])).toContain('Anna Andersson')
    expect(text(rows[1])).toContain('Bo Berg')
    expect(rows[0].find('a').attributes('href'))
      .toBe('/courses/c1/assignments/l1/submissions/s1')
    expect(rows[1].find('a').attributes('href'))
      .toBe('/courses/c2/assignments/l3/submissions/s4')
  })

  it('knappen Bedöm i ordning öppnar gruppens första inlämning', async () => {
    backend({
      release: [
        submission('s1', 'u3', 'Submitted', '2026-10-10T10:00:00Z'),
        submission('s2', 'u2', 'Submitted', '2026-10-09T10:00:00Z'),
      ],
    })

    const {wrapper} = await renderApp('/grading')
    await flushPromises()

    const button = wrapper.find('[data-test="grade-in-order"]')
    expect(text(button)).toBe('Bedöm 2 i ordning')
    expect(button.attributes('href')).toBe('/courses/c1/assignments/l1/submissions/s2')
  })

  it('markerar första inlämningen och inlämnat igen', async () => {
    backend({
      release: [
        submission('s1', 'u1', 'Submitted', '2026-10-10T10:00:00Z'),
        submission('s2', 'u2', 'Submitted', '2026-10-09T10:00:00Z', {
          gradedById: 'u0', level: null, feedback: 'Komplettera', gradedAt: '2026-10-08T10:00:00Z',
        }),
      ],
    })

    const {wrapper} = await renderApp('/grading')
    await flushPromises()

    const rows = wrapper.findAll('[data-test="grading-row"]')
    expect(text(rows[0])).toContain('Första inlämningen')
    expect(text(rows[1])).toContain('Inlämnat igen')
  })

  it('visar deadlineraden i gruppens huvud', async () => {
    const closed = {...release, dueAt: '2026-11-01T12:00:00Z', resubmitDueAt: ['2026-11-02T12:00:00Z']}
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [devops]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('m1', [closed])]}
      }
      if (path === '/api/links/l1/submissions') {
        return {status: 200, body: releaseSubmissions}
      }
      return {status: 404}
    })

    const {wrapper} = await renderApp('/grading')
    await flushPromises()

    expect(text(wrapper.find('[data-test="grading-deadline"]')))
      .toBe('Sista deadline passerad 2 nov · bara G eller IG')
  })

  it('delar upp i två flikar utan automatiskt IG med antal och byter innehåll', async () => {
    const passed = '2020-01-01T10:00:00Z'
    const withDeadlines = {...release, dueAt: passed, resubmitDueAt: ['2099-01-01T10:00:00Z']}
    const lastPassed = {...docker, dueAt: passed, resubmitDueAt: ['2020-02-01T10:00:00Z']}
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1', 'c2'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [devops, databases]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('m1', [withDeadlines])]}
      }
      if (path === '/api/courses/c2/modules') {
        return {status: 200, body: [week('m2', [lastPassed])]}
      }
      if (path === '/api/links/l1/submissions') {
        return {
          status: 200,
          body: [
            submission('s1', 'u1', 'Submitted', '2026-10-10T10:00:00Z'),
            submission('s2', 'u2', 'NotStarted', null, null, {nextDeadline: '2099-01-01T10:00:00Z'}),
            submission('s3', 'u3', 'NeedsWork', '2026-10-09T10:00:00Z', null, {nextDeadline: '2099-01-01T10:00:00Z'}),
          ],
        }
      }
      if (path === '/api/links/l3/submissions') {
        return {status: 200, body: [submission('s4', 'u2', 'NotStarted', null, null, {autoFail: true})]}
      }
      return {status: 404}
    })

    const {wrapper} = await renderApp('/grading')
    await flushPromises()

    const counts = wrapper.findAll('[data-test="grading-tab-count"]').map((count) => count.text())
    expect(counts).toEqual(['1', '2'])
    expect(wrapper.find('[data-test="grading-tab-autoFail"]').exists()).toBe(false)

    expect(wrapper.text()).not.toContain('IG · Ingen inlämning')

    await wrapper.find('[data-test="grading-tab-waiting"]').trigger('click')
    const waiting = wrapper.findAll('[data-test="grading-row"]')
    expect(text(waiting[0])).toContain('Bo Berg')
    expect(text(waiting[1])).toContain('Clara Carlsson')
    expect(text(waiting[1])).toContain('Komplettera')
    expect(text(waiting[1])).toContain('Chans till')
    expect(waiting[1].find('a').exists()).toBe(false)
  })

  it('visar en tom sida när inget väntar på bedömning', async () => {
    backend({
      release: [submission('s2', 'u2', 'Graded', '2026-10-09T10:00:00Z')],
      docker: [],
    })

    const {wrapper} = await renderApp('/grading')
    await flushPromises()

    expect(wrapper.text()).toContain('Inget att bedöma')
    expect(wrapper.findAll('[data-test="grading-row"]')).toHaveLength(0)
  })

  it('hoppar över teoridelar', async () => {
    backend()

    await renderApp('/grading')
    await flushPromises()

    expect(requests.map((request) => request.path)).not.toContain('/api/links/l2/submissions')
  })

  it('en elev som öppnar Att bedöma hamnar på Idag', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Student', ['c1', 'c2'], 'Bo Elev')}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [devops, databases]}
      }
      if (path === '/api/me/today') {
        return {status: 200, body: {todo: [], submitted: [], feedback: [], news: [], courses: []}}
      }
      return {status: 200, body: []}
    })

    const {router, wrapper} = await renderApp('/grading')

    expect(router.currentRoute.value.name).toBe('today')
    expect(router.currentRoute.value.path).toBe('/')
    expect(wrapper.text()).toContain('Idag')
    // Omdirigeringen är inte tyst: användaren får veta varför.
    expect(wrapper.find('[data-test="toast"]').text()).toContain('Du har inte behörighet till den sidan.')
  })
})
