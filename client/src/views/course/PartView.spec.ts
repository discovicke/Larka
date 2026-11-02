import {flushPromises} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
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
  completionRule: 'Submitted',
  title: 'Release-pipeline',
  status: 'Published',
  groupId: null,
  publishAt: '2026-10-01T08:00:00Z',
  dueAt: '2026-10-23T21:59:00Z',
  resubmitDueAt: [],
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

function backend(options: {
  submissions?: unknown[]
  fail?: boolean
  me?: ReturnType<typeof meAs>
  bodyMd?: string
  part?: typeof assignment | Record<string, unknown>
} = {}) {
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
      return {status: 200, body: [{...week, content: [options.part ?? assignment]}]}
    }
    if (path === '/api/content/x1/status') {
      return {status: 204}
    }
    if (path === '/api/content/x1') {
      return {status: 200, body: {...content, bodyMd: options.bodyMd ?? content.bodyMd}}
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

describe('PartView', () => {
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

  it('öppnar delen läsbar med fakta, redigera och läget', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/parts/l1')

    expect(wrapper.find('h1').text()).toBe('Release-pipeline')
    expect(text(wrapper.find('[data-test="part-facts"]')))
      .toMatch(/^Inlämning · Vecka 4 · Deadline fre 23 okt \d\d:59 · Betygsskala Icke godkänd \/ Godkänd \/ Väl godkänd · Lämnas in som text, GitHub-repo$/)
    expect(wrapper.find('[data-test="part-content"]').text()).toContain('länken')
    expect(wrapper.find('[data-test="edit-part"]').attributes('href'))
      .toBe('/courses/c1/modules/m1/content/l1/edit')
    const state = text(wrapper.find('[data-test="part-state"]'))
    expect(state).toContain('Publicerad')
    expect(state).toContain('Inlämnade3 av 4')
    expect(state).toContain('Att bedöma1')
    expect(wrapper.find('section[aria-label="Elever"]').exists()).toBe(false)
  })

  it('Bedöm-knappen och fliken öppnar inlämningarna', async () => {
    backend()

    const {wrapper, router} = await renderApp('/courses/c1/parts/l1')

    expect(text(wrapper.find('[data-test="tab-submissions"]'))).toBe('Inlämningar 3 av 4')
    await wrapper.find('[data-test="grade-part"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.tab).toBe('inlamningar')
    expect(wrapper.find('section[aria-label="Elever"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="part-content"]').exists()).toBe(false)
  })

  it('checklistan i uppgiften visas som en lista utan bockrutor', async () => {
    backend({bodyMd: 'Texten.\n\n- [ ] Länk till repot'})

    const {wrapper} = await renderApp('/courses/c1/parts/l1')

    expect(wrapper.find('[data-test="part-checklist"]').text()).toContain('Länk till repot')
    expect(wrapper.find('[data-test="part-checklist"] [role="checkbox"]').exists()).toBe(false)
  })

  it('teori har inga flikar, och ett utkast går att publicera härifrån', async () => {
    backend({part: {...assignment, type: 'Theory', gradeScale: null, completionRule: null, dueAt: null, status: 'Draft'}})

    const {wrapper} = await renderApp('/courses/c1/parts/l1')

    expect(wrapper.find('[data-test="tab-content"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Eleverna ser inte den här delen än.')
    await wrapper.find('[data-test="publish-part"]').trigger('click')
    await flushPromises()

    expect(sent('PUT', '/api/content/x1/status')).toEqual({status: 'Published'})
  })

  it('mentorn ser inlämningar men kan inte redigera eller bedöma härifrån', async () => {
    backend({me: mentorAs(['c1'])})

    const {wrapper} = await renderApp('/courses/c1/parts/l1')

    expect(wrapper.find('[data-test="tab-submissions"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="edit-part"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="grade-part"]').exists()).toBe(false)
  })

  it('admin läser delen utan inlämningar och utan att redigera', async () => {
    backend({me: meAs('Admin', [], 'Ada Admin')})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    expect(wrapper.find('h1').text()).toBe('Release-pipeline')
    expect(wrapper.find('[data-test="tab-submissions"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="edit-part"]').exists()).toBe(false)
    expect(requests.some((request) => request.path === '/api/links/l1/submissions')).toBe(false)
  })

  it('eleven skickas till delen på kurssidan', async () => {
    backend({me: meAs('Student', ['c1'])})

    const {router} = await renderApp('/courses/c1/parts/l1')

    expect(router.currentRoute.value.name).toBe('course-content')
    expect(router.currentRoute.value.query.del).toBe('l1')
  })

  it('filtren räknar inlämningarna', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    const filters = wrapper.findAll('section[aria-label="Elever"] [role="tab"]').map(text)
    expect(filters).toEqual([
      'Alla 4',
      'Att bedöma 1',
      'Komplettering 1',
      'Inte inlämnad 1',
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
    expect(rows[3].text()).toContain('Inte inlämnad')
  })

  it('läraren kan bedöma en elev som inte lämnat in, och Ej inlämnat får ett eget filter', async () => {
    const missed = makeSubmission('s5', 'u4', 'NotSubmitted', {
      grade: {gradedById: 'u0', level: null, feedback: 'Ingen inlämning', gradedAt: '2026-10-24T09:00:00Z'},
    })
    backend({submissions: [notStarted, missed].map((row, index) => ({...row, id: `n${index}`, studentId: `u${index + 3}`}))})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    const rows = wrapper.findAll('[data-test="submission-row"]')
    expect(rows.map((row) => row.find('a').exists() && row.find('a').text())).toEqual(['Bedöm', 'Bedöm'])
    expect(rows.some((row) => row.text().includes('Ej inlämnat'))).toBe(true)
    const filters = wrapper.findAll('section[aria-label="Elever"] [role="tab"]').map(text)
    expect(filters).toContain('Ej inlämnat 1')
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
    expect(needsWorkLink.text()).toBe('Visa')

    const tile = rows[2].find('a[href="/courses/c1/assignments/l1/submissions/s1"]')
    expect(tile.text()).toBe('G')
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

  it('visar en rad per grupp och expanderar medlemmarna', async () => {
    const groupOne = {
      id: 'g1',
      name: 'Grupp 1',
      members: [{id: 'u1', name: names.u1}, {id: 'u2', name: names.u2}],
    }
    const groupTwo = {
      id: 'g2',
      name: 'Grupp 2',
      members: [{id: 'u3', name: names.u3}, {id: 'u4', name: names.u4}],
    }
    const groupSubmissions = [
      makeSubmission('gs1', 'u1', 'Submitted', {
        repo,
        group: groupOne,
        submittedAt: '2026-10-21T10:00:00Z',
      }),
      makeSubmission('gs2', 'u2', 'Graded', {
        repo,
        group: groupOne,
        grade: {gradedById: 'u0', level: 'Pass', feedback: 'Bra', gradedAt: '2026-10-24T09:00:00Z'},
      }),
      makeSubmission('gs3', 'u3', 'NeedsWork', {repo, group: groupTwo}),
      makeSubmission('gs4', 'u4', 'NotStarted', {repo, group: groupTwo}),
    ]
    backend({submissions: groupSubmissions})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    expect(wrapper.findAll('[data-test="group-row"]')).toHaveLength(2)
    expect(wrapper.findAll('[data-test="submission-row"]')).toHaveLength(0)
    expect(wrapper.findAll('[data-test="group-name"]').map(text)).toEqual(['Grupp 1', 'Grupp 2'])
    expect(wrapper.text()).toContain('Visar 2 av 2 grupper')
    expect(wrapper.find('[data-test="group-row"] a[href="/courses/c1/assignments/l1/submissions/gs1"]').exists())
      .toBe(true)

    await wrapper.findAll('[data-test="group-toggle"]')[0].trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-test="member-row"]')).toHaveLength(2)
    expect(wrapper.findAll('[data-test="member-row"]').map(text).join(' ')).toContain('Anna Andersson')
  })

  it('filtrerar grupper på aggregerad status', async () => {
    const groupOne = {
      id: 'g1',
      name: 'Grupp 1',
      members: [{id: 'u1', name: names.u1}, {id: 'u2', name: names.u2}],
    }
    const groupTwo = {
      id: 'g2',
      name: 'Grupp 2',
      members: [{id: 'u3', name: names.u3}],
    }
    backend({
      submissions: [
        makeSubmission('gs1', 'u1', 'Submitted', {repo, group: groupOne}),
        makeSubmission('gs2', 'u2', 'NotStarted', {repo, group: groupOne}),
        makeSubmission('gs3', 'u3', 'NeedsWork', {repo, group: groupTwo}),
      ],
    })

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')
    const due = wrapper.findAll('section[aria-label="Elever"] [role="tab"]')
      .find((tab) => text(tab) === 'Att bedöma 1')
    await due?.trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-test="group-row"]')).toHaveLength(1)
    expect(wrapper.find('[data-test="group-name"]').text()).toBe('Grupp 1')

    const needsWork = wrapper.findAll('section[aria-label="Elever"] [role="tab"]')
      .find((tab) => text(tab).startsWith('Komplettering'))
    await needsWork?.trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="group-name"]').text()).toBe('Grupp 2')
  })

  it('visar ett fel när inlämningarna inte kan hämtas', async () => {
    backend({fail: true})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1')

    expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte hämta delen.')
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
