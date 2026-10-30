import {flushPromises} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

import {mockApi, requests} from '@/test/mockApi'
import {mentorAs, meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const names: Record<string, string> = {
  u0: 'Anna Lärare',
  u1: 'Anna Andersson',
  u2: 'Bo Berg',
  u3: 'Clara Carlsson',
  u4: 'David Dahl',
}

const assignment = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Assignment',
  gradeScale: 'PassFailDistinction',
  completionRule: null,
  title: 'Release-pipeline',
  status: 'Published',
  groupId: null,
  publishAt: '2026-10-01T08:00:00Z',
  dueAt: '2026-10-23T21:59:00Z',
  isPinned: false,
  blockedBy: [],
}
const week = {id: 'm1', courseId: 'c1', title: 'Vecka 4', description: '', position: 4, content: [assignment]}
const content = {
  id: 'x1',
  authorId: 'u0',
  type: 'Assignment',
  title: 'Release-pipeline',
  bodyMd: 'Lämna in **länken** till repot.',
  status: 'Published',
  createdAt: '2026-10-01T08:00:00Z',
  assignment: {gradeScale: 'PassFailDistinction', submissionModes: ['Text', 'Repo'], templateRepo: null},
}

const repo = {
  repoFullName: 'anna/release-pipeline',
  lastSha: '0123456789abcdef',
  lastCommitMessage: 'feat: release pipeline',
  lastCommitAt: '2026-10-30T08:00:00Z',
  commitCount: 14,
  checkStatus: 'Green',
  checksPassed: 9,
  checksTotal: 12,
  updatedAt: '2026-10-30T08:00:00Z',
  provisioningState: 'Ready',
  provisioningError: null,
  submissionSha: null,
  lockedAt: null,
  lockError: null,
}

function makeSubmission(id: string, studentId: string, status: string, extra: Record<string, unknown> = {}) {
  return {
    id,
    contentLinkId: 'l1',
    studentId,
    studentName: names[studentId],
    status,
    bodyMd: null,
    submittedAt: null,
    files: [],
    repo: null,
    grade: null,
    ...extra,
  }
}

const graded = makeSubmission('s1', 'u1', 'Graded', {
  submittedAt: '2026-10-20T10:00:00Z',
  repo,
  grade: {gradedById: 'u0', level: 'Pass', feedback: 'Bra jobbat', gradedAt: '2026-10-24T09:00:00Z'},
})
const submitted = makeSubmission('s2', 'u2', 'Submitted', {
  submittedAt: '2026-10-21T10:00:00Z',
  files: [{id: 'f1', fileName: 'rapport.pdf', url: 'https://files.example/f1', sizeBytes: 1200}],
})
const needsWork = makeSubmission('s3', 'u3', 'NeedsWork', {submittedAt: '2026-10-19T10:00:00Z'})
const notStarted = makeSubmission('s4', 'u4', 'NotStarted')
const roster = [notStarted, graded, needsWork, submitted]

function backend(options: {submissions?: unknown[], fail?: boolean, me?: ReturnType<typeof meAs>} = {}) {
  mockApi((method, path) => {
    if (method === 'POST' && path === '/api/links/l1/repos/refresh') {
      return {status: 204}
    }
    if (method === 'POST' && path === '/api/links/l1/repos/lock') {
      return {status: 204}
    }
    if (method === 'POST' && path === '/api/links/l1/help/u1/resolve') {
      return {status: 204}
    }
    if (path === '/api/me') {
      return {status: 200, body: options.me ?? meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: [week]}
    }
    if (path === '/api/content/x1') {
      return {status: 200, body: content}
    }
    if (path === '/api/links/l1/submissions') {
      return options.fail
        ? {status: 500, body: 'boom'}
        : {status: 200, body: options.submissions ?? roster}
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

function rowNames(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']): string[] {
  return wrapper.findAll('[data-test="submission-row"]')
    .map((row) => row.find('.font-medium').text())
}

describe('SubmissionsView', () => {
  it('mentorn läser listan och får Visa i stället för Bedöm', async () => {
    backend({me: mentorAs(['c1'])})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    expect(rowNames(wrapper)).toContain('Bo Berg')
    const row = wrapper.findAll('[data-test="submission-row"]')
      .find((candidate) => candidate.text().includes('Bo Berg'))!
    expect(text(row.find('a'))).toBe('Visa')
    const actions = wrapper.findAll('[data-test="submission-row"] a').map(text)
    expect(actions).not.toContain('Bedöm')
  })

  it('läraren får Bedöm', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    const row = wrapper.findAll('[data-test="submission-row"]')
      .find((candidate) => candidate.text().includes('Bo Berg'))!
    expect(text(row.find('a'))).toBe('Bedöm')
  })

  it('visar uppgiften, veckan, betygsskalan och antalet elever', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    expect(wrapper.find('h1').text()).toBe('Release-pipeline')
    expect(wrapper.text()).toContain('Vecka 4')
    expect(wrapper.text()).toContain('IG–VG')
    expect(wrapper.text()).toContain('Slutar')
    expect(wrapper.text()).toContain('Publicerad')
    expect(wrapper.text()).toContain('Inlämning')
  })

  it('räknar inlämningarna i korten och i filtret', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    const cards = wrapper.findAll('[data-test="stat-card"]').map(text)
    expect(cards).toHaveLength(5)
    expect(cards[0]).toContain('Inlämnade')
    expect(cards[0]).toContain('3')
    expect(cards[0]).toContain('av 4')
    expect(cards[1]).toContain('Att bedöma')
    expect(cards[1]).toContain('1')
    expect(cards[2]).toContain('Bedömda')
    expect(cards[2]).toContain('1')
    expect(cards[3]).toContain('Komplettering')
    expect(cards[3]).toContain('1')
    expect(cards[4]).toContain('Behöver hjälp')
    expect(cards[4]).toContain('0')

    const filters = wrapper.findAll('section[aria-label="Elever"] [role="tab"]').map(text)
    expect(filters).toEqual([
      'Alla 4',
      'Att bedöma 1',
      'Komplettering 1',
      'Ej inlämnad 1',
      'Behöver hjälp 0',
      'Inte startat 0',
      'Ingen push 0',
      'Röda tester 0',
      'Elevflagga 0',
    ])
    expect(wrapper.text()).toContain('Visar 4 av 4 elever')
  })

  it('sorterar inlämnade först och visar status, filer, tester och betyg', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    expect(rowNames(wrapper)).toEqual(['Bo Berg', 'Clara Carlsson', 'Anna Andersson', 'David Dahl'])

    const rows = wrapper.findAll('[data-test="submission-row"]')
    expect(rows[0].text()).toContain('Inlämnad')
    expect(rows[0].text()).toContain('1')
    expect(rows[2].text()).toContain('9/12')
    expect(rows[2].text()).toContain('14')
    expect(rows[1].text()).toContain('Komplettera')
    expect(rows[2].text()).toContain('Bedömd')
    expect(rows[2].text()).toContain('anna/release-pipeline')
    expect(rows[3].text()).toContain('Ej inlämnad')
  })

  it('filtrerar på status och söker på namn', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    const filter = wrapper.findAll('section[aria-label="Elever"] [role="tab"]')
      .find((tab) => text(tab) === 'Komplettering 1')
    await filter?.trigger('click')
    await flushPromises()
    expect(rowNames(wrapper)).toEqual(['Clara Carlsson'])
    expect(wrapper.text()).toContain('Visar 1 av 4 elever')

    const alla = wrapper.findAll('section[aria-label="Elever"] [role="tab"]')
      .find((tab) => text(tab) === 'Alla 4')
    await alla?.trigger('click')
    await wrapper.find('#submission-search').setValue('anna')
    await flushPromises()

    expect(rowNames(wrapper)).toEqual(['Anna Andersson'])
  })

  it('länkar till bedömningen och visar betygstilen för en bedömd elev', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    const rows = wrapper.findAll('[data-test="submission-row"]')
    const toGrade = rows[0].find('a[href="/courses/c1/assignments/l1/submissions/s2"]')
    expect(toGrade.text()).toBe('Bedöm')

    const needsWorkLink = rows[1].find('a[href="/courses/c1/assignments/l1/submissions/s3"]')
    expect(needsWorkLink.text()).toBe('Bedöm')

    const tile = rows[2].find('a[href="/courses/c1/assignments/l1/submissions/s1"]')
    expect(tile.text()).toBe('G')
  })

  it('Instruktioner visar uppgiftens text i stället för tabellen', async () => {
    backend()
    const {wrapper, router} = await renderApp('/courses/c1/assignments/l1')

    const instructions = wrapper.findAll('button[role="tab"]')
      .find((tab) => tab.text() === 'Instruktioner')
    await instructions?.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.tab).toBe('instructions')
    expect(wrapper.find('strong').text()).toBe('länken')
    expect(wrapper.findAll('[data-test="submission-row"]')).toHaveLength(0)
  })

  it('visar och filtrerar stödorsaker samt kan lösa en elevflagga', async () => {
    const supported = makeSubmission('s5', 'u1', 'NotStarted', {
      repo,
      support: {
        notStarted: true,
        noRecentPush: true,
        redChecks: true,
        helpRequested: true,
        helpRequestedAt: '2026-10-30T09:00:00Z',
        helpMessage: 'Jag fastnar på releaseflödet.',
        helpResolved: false,
      },
    })
    backend({submissions: [supported]})
    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    expect(wrapper.find('[data-test="support-reasons"]').text()).toContain('Elevflagga')
    expect(wrapper.find('[data-test="support-reasons"]').text()).toContain('Inte startat')

    const filter = wrapper.findAll('section[aria-label="Elever"] [role="tab"]')
      .find((tab) => text(tab) === 'Röda tester 1')
    await filter?.trigger('click')
    await flushPromises()
    expect(rowNames(wrapper)).toEqual(['Anna Andersson'])

    await wrapper.find('[data-test="support-resolve"]').trigger('click')
    await flushPromises()
    expect(requests.some((request) =>
      request.method === 'POST' && request.path === '/api/links/l1/help/u1/resolve')).toBe(true)
  })

  it('uppdaterar, klonar och låser repon från lärarvyn', async () => {
    backend()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    await wrapper.find('[data-test="github-refresh"]').trigger('click')
    await flushPromises()
    expect(requests.some((request) =>
      request.method === 'POST' && request.path === '/api/links/l1/repos/refresh')).toBe(true)

    await wrapper.find('[data-test="github-clone-all"]').trigger('click')
    await flushPromises()
    const cloneCommands = document.querySelector<HTMLTextAreaElement>('[data-test="clone-commands"]')
    expect(cloneCommands?.value).toContain(
      'git clone https://github.com/anna/release-pipeline.git',
    )

    await wrapper.find('[data-test="github-lock"]').trigger('click')
    await flushPromises()
    expect(confirm).toHaveBeenCalled()
    expect(requests.some((request) =>
      request.method === 'POST' && request.path === '/api/links/l1/repos/lock')).toBe(true)
  })

  it('visar ett fel när inlämningarna inte kan hämtas', async () => {
    backend({fail: true})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte hämta inlämningarna.')
    expect(wrapper.findAll('[data-test="submission-row"]')).toHaveLength(0)
  })

  it('visar en tom rad när sökningen inte matchar', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    await wrapper.find('#submission-search').setValue('zzz')
    await flushPromises()

    expect(wrapper.text()).toContain('Inga elever matchar sökningen.')
  })
})
