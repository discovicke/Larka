import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {mentorAs, meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

const repo = {
  repoFullName: 'anna/release-pipeline',
  lastSha: '0123456789abcdef',
  lastCommitMessage: 'feat: release pipeline',
  lastCommitAt: '2026-10-23T20:00:00Z',
  commitCount: 14,
  checkStatus: 'Green',
  checksPassed: 12,
  checksTotal: 12,
  updatedAt: '2026-10-23T20:00:00Z',
  provisioningState: 'Ready',
  provisioningError: null,
  submissionSha: null,
  lockedAt: null,
  lockError: null,
  reopenedAt: '2026-10-30T10:00:00Z',
  checkRuns: [],
}

const baseSubmission = {
  id: 's1',
  contentLinkId: 'l1',
  studentId: 'u1',
  studentName: 'Anna Andersson',
  status: 'Submitted',
  bodyMd: 'Min **lösning** finns i repot.',
  submittedAt: '2026-10-21T10:00:00Z',
  files: [{id: 'f1', fileName: 'rapport.pdf', url: 'https://files.example/f1', sizeBytes: 1200}],
  repo,
  grade: null,
}

const gradedSubmission = {
  ...baseSubmission,
  status: 'Graded',
  grade: {
    gradedById: 'u0',
    level: 'Pass',
    feedback: 'Tidigare kommentar',
    gradedAt: '2026-10-24T09:00:00Z',
  },
}

function week(scale: string) {
  return {
    id: 'm1',
    courseId: 'c1',
    title: 'Vecka 4',
    description: '',
    position: 4,
    content: [{
      linkId: 'l1',
      contentId: 'x1',
      type: 'Assignment',
      gradeScale: scale,
      completionRule: null,
      title: 'Release-pipeline',
      status: 'Published',
      groupId: null,
      publishAt: '2026-10-01T08:00:00Z',
      dueAt: '2026-10-23T21:59:00Z',
      isPinned: false,
      blockedBy: [],
    }],
  }
}

const content = {
  id: 'x1',
  authorId: 'u0',
  type: 'Assignment',
  title: 'Release-pipeline',
  bodyMd: 'Lämna in **länken** till repot.',
  status: 'Published',
  createdAt: '2026-10-01T08:00:00Z',
  assignment: {gradeScale: 'PassFail', submissionModes: ['Text', 'Repo'], templateRepo: null},
}

function backend(options: {scale?: string, submission?: unknown, me?: ReturnType<typeof meAs>} = {}) {
  mockApi((method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: options.me ?? meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: [week(options.scale ?? 'PassFail')]}
    }
    if (path === '/api/content/x1') {
      return {status: 200, body: content}
    }
    if (path === '/api/submissions/s1' && method === 'GET') {
      return {status: 200, body: options.submission ?? baseSubmission}
    }
    if (path === '/api/submissions/s1/grade' && method === 'POST') {
      return {status: 200, body: baseSubmission}
    }
    if (path === '/api/links/l1/submissions') {
      return {status: 200, body: []}
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

function button(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], label: string) {
  const found = wrapper.findAll('button')
    .find((candidate) => text(candidate) === label)
  if (!found) {
    throw new Error(`No button: ${label}`)
  }
  return found
}

function radios(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']): string[] {
  return wrapper.findAll('[role="radio"]').map(text)
}

describe('ReviewView', () => {
  it('mentorn läser inlämningen och betyget men kan inte bedöma', async () => {
    backend({me: mentorAs(['c1']), submission: gradedSubmission})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('h1').text()).toBe('Anna Andersson')
    expect(wrapper.find('strong').text()).toBe('lösning')
    expect(wrapper.find('[data-test="read-only-grade"]').text()).toBe('G · Godkänt')
    expect(wrapper.text()).toContain('Tidigare kommentar')
    expect(wrapper.findAll('[role="radio"]')).toHaveLength(0)
    expect(wrapper.find('textarea').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Publicera bedömning')
    expect(wrapper.text()).not.toContain('Begär komplettering')
  })

  it('läraren ser bedömningsformuläret', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('[data-test="read-only-grade"]').exists()).toBe(false)
    expect(wrapper.find('textarea').exists()).toBe(true)
    expect(wrapper.text()).toContain('Publicera bedömning')
  })

  it('visar eleven, svaret, filerna och repot', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('h1').text()).toBe('Anna Andersson')
    expect(wrapper.find('strong').text()).toBe('lösning')

    const file = wrapper.find('a[href="https://files.example/f1"]')
    expect(file.text()).toContain('rapport.pdf')
    expect(file.text()).toContain('1,2 kB')
    expect(file.attributes('target')).toBe('_blank')

    expect(wrapper.text()).toContain('Repo')
    expect(wrapper.text()).toContain('anna/release-pipeline')
    expect(wrapper.text()).toContain('0123456')
    expect(wrapper.text()).toContain('Tester: 12/12')
    expect(wrapper.text()).toContain('Gröna')
    expect(wrapper.text()).toContain('Öppnat för komplettering')
  })

  it('visar IG och G för skalan IG-G', async () => {
    backend({scale: 'PassFail'})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(radios(wrapper)).toHaveLength(2)
    expect(radios(wrapper)[0]).toContain('IG')
    expect(radios(wrapper)[0]).toContain('Icke godkänt')
    expect(radios(wrapper)[1]).toContain('G')
    expect(radios(wrapper)[1]).toContain('Godkänt')
  })

  it('visar IG, G och VG för skalan IG-VG', async () => {
    backend({scale: 'PassFailDistinction'})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(radios(wrapper)).toHaveLength(3)
    expect(radios(wrapper)[0]).toContain('IG')
    expect(radios(wrapper)[1]).toContain('G')
    expect(radios(wrapper)[2]).toContain('VG')
    expect(radios(wrapper)[2]).toContain('Väl godkänt')
  })

  it('visar bara återkoppling när uppgiften saknar betyg', async () => {
    backend({scale: 'None'})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.findAll('[role="radio"]')).toHaveLength(0)
    expect(wrapper.text()).toContain('Uppgiften har inget betyg, bara återkoppling.')
  })

  it('kräver ett betyg innan bedömningen publiceras', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    await button(wrapper, 'Publicera bedömning').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Välj ett betyg.')
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)
  })

  it('publicerar betyget och går tillbaka till inlämningarna', async () => {
    backend()

    const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    await wrapper.findAll('[role="radio"]')[1].trigger('click')
    await wrapper.find('#feedback').setValue('Bra jobbat')
    await button(wrapper, 'Publicera bedömning').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade'))
      .toEqual({feedback: 'Bra jobbat', level: 'Pass', needsWork: false})
    expect(router.currentRoute.value.name).toBe('part')
    expect(router.currentRoute.value.query.tab).toBe('inlamningar')
  })

  it('kräver en kommentar när eleven ska komplettera', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    await button(wrapper, 'Begär komplettering').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Skriv vad eleven ska komplettera.')
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)
  })

  it('begär komplettering med kommentaren och utan betyg', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    await wrapper.find('#feedback').setValue('Komplettera testerna.')
    await button(wrapper, 'Begär komplettering').trigger('click')
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade'))
      .toEqual({feedback: 'Komplettera testerna.', level: null, needsWork: true})
  })

  it('fyller i en tidigare bedömning', async () => {
    backend({submission: gradedSubmission})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect((wrapper.find('#feedback').element as HTMLTextAreaElement).value).toBe('Tidigare kommentar')
    expect(wrapper.findAll('[role="radio"]')[1].attributes('aria-checked')).toBe('true')
    expect(wrapper.text()).toContain('Bedömd')
  })
})
