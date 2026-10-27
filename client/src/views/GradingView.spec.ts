import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests} from '@/test/mockApi'
import {renderApp} from '@/test/renderApp'

const devops = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const databases = {id: 'c2', schoolId: 's1', code: 'DB', title: 'Databaser'}
const teacher = {id: 'u0', schoolId: 's1', name: 'Anna Lärare', email: 'larare@example.com', role: 'Teacher'}
const anna = {id: 'u1', schoolId: 's1', name: 'Anna Andersson', email: 'anna@example.com', role: 'Student'}
const bo = {id: 'u2', schoolId: 's1', name: 'Bo Berg', email: 'bo@example.com', role: 'Student'}
const clara = {id: 'u3', schoolId: 's1', name: 'Clara Carlsson', email: 'clara@example.com', role: 'Student'}
const users = [teacher, anna, bo, clara]

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
    dueAt: null,
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

function submission(id: string, studentId: string, status: string, submittedAt: string | null = null) {
  return {
    id,
    contentLinkId: 'l1',
    studentId,
    status,
    bodyMd: null,
    submittedAt,
    files: [],
    repo: null,
    grade: null,
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
    if (path === '/api/users') {
      return {status: 200, body: users}
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
  it('samlar inlämnade uppgifter från kurserna, äldst först', async () => {
    backend()

    const {wrapper} = await renderApp('/grading')
    await flushPromises()

    const rows = wrapper.findAll('[data-test="grading-row"]')
    expect(rows).toHaveLength(2)
    expect(text(rows[0])).toContain('Bo Berg')
    expect(text(rows[0])).toContain('Docker-uppgift')
    expect(text(rows[0])).toContain('Databaser')
    expect(text(rows[1])).toContain('Anna Andersson')
    expect(text(rows[1])).toContain('Release-pipeline')
    expect(text(rows[1])).toContain('DevOps')

    expect(rows[0].find('a').attributes('href'))
      .toBe('/courses/c2/assignments/l3/submissions/s4')
    expect(rows[1].find('a').attributes('href'))
      .toBe('/courses/c1/assignments/l1/submissions/s1')
  })

  it('hoppar över bedömda, kompletteringar och teoridelar', async () => {
    backend()

    const {wrapper} = await renderApp('/grading')
    await flushPromises()

    expect(wrapper.findAll('[data-test="grading-row"]')).toHaveLength(2)
    expect(requests.map((request) => request.path)).not.toContain('/api/links/l2/submissions')
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
})
