import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const group = {id: 'g1', schoolId: 's1', name: 'Klass 1', term: 'HT26'}

interface Blocker {
  linkId: string
  title: string | null
  isSatisfied: boolean
}

interface StudentItem {
  linkId: string
  contentId: string
  type: string
  gradeScale: string | null
  title: string
  bodyMd: string | null
  publishAt: string
  dueAt: string | null
  isPinned: boolean
  isLocked: boolean
  isCompleted: boolean
  canMarkDone: boolean
  blockedBy: Blocker[]
  submissionStatus: string | null
  gradeLevel: string | null
}

const theory: StudentItem = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Theory',
  gradeScale: null,
  title: 'Teori A',
  bodyMd: 'Läs kapitel 1',
  publishAt: '2026-01-01T08:00:00Z',
  dueAt: null,
  isPinned: false,
  isLocked: false,
  isCompleted: false,
  canMarkDone: true,
  blockedBy: [],
  submissionStatus: null,
  gradeLevel: null,
}

const locked: StudentItem = {
  ...theory,
  linkId: 'l2',
  contentId: 'x2',
  title: 'Teori B',
  // Backend skickar null för låsta delar. Testet sätter text för att visa att vyn inte läcker den.
  bodyMd: 'Hemligt innehåll',
  isLocked: true,
  blockedBy: [{linkId: 'l1', title: 'Teori A', isSatisfied: false}],
}

const submitted: StudentItem = {
  ...theory,
  linkId: 'l3',
  contentId: 'x3',
  type: 'Assignment',
  gradeScale: 'PassFail',
  title: 'Inlämning',
  bodyMd: '',
  canMarkDone: false,
  isCompleted: true,
  submissionStatus: 'Submitted',
}

const graded: StudentItem = {
  ...submitted,
  linkId: 'l4',
  contentId: 'x4',
  title: 'Betygsatt inlämning',
  submissionStatus: 'Graded',
  gradeLevel: 'Pass',
}

const hiddenBlocker: StudentItem = {
  ...locked,
  linkId: 'l5',
  contentId: 'x5',
  title: 'Teori C',
  blockedBy: [{linkId: 'l9', title: null, isSatisfied: false}],
}

const completed: StudentItem = {
  ...theory,
  linkId: 'l6',
  contentId: 'x6',
  title: 'Klar teori',
  isCompleted: true,
}

function week(content: StudentItem[], overrides: Record<string, unknown> = {}) {
  return {
    id: 'm1',
    courseId: 'c1',
    title: 'Vecka 1',
    description: 'Första veckan',
    position: 1,
    publishAt: null,
    isAvailable: true,
    content,
    ...overrides,
  }
}

function backend(content: StudentItem[]) {
  mockApi((method, path) => {
    if (path === '/api/groups') {
      return {status: 200, body: [group]}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/groups/g1/courses/c1/modules') {
      return {status: 200, body: [week(content)]}
    }
    if (method === 'PUT' && path === '/api/groups/g1/courses/c1/links/l1/completion') {
      return {
        status: 200,
        body: week(content.map((item) => item.linkId === 'l1'
          ? {...item, isCompleted: true}
          : item)),
      }
    }
    if (method === 'PUT' && path === '/api/groups/g1/courses/c1/links/l6/completion') {
      return {
        status: 200,
        body: week(content.map((item) => item.linkId === 'l6'
          ? {...item, isCompleted: false}
          : item)),
      }
    }
    return {status: 200, body: []}
  })
}

async function openSection(
  wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'],
  key: string,
) {
  const trigger = wrapper.find(`[data-test="student-section-${key}"]`)
  expect(trigger.exists()).toBe(true)
  await trigger.trigger('click')
  await flushPromises()
}

describe('StudentCourseContentView', () => {
  it('visar nästa steg och låsta delar utan blockerartexter', async () => {
    backend([theory, locked])
    const {wrapper} = await renderApp('/dev/student')

    expect(requests).toContainEqual({
      method: 'GET',
      path: '/api/groups/g1/courses/c1/modules',
      body: undefined,
    })
    expect(wrapper.text()).toContain('Vecka 1')
    expect(wrapper.text()).not.toContain('Så funkar låsningar')
    expect(wrapper.find('[data-test="student-section-next"]').text()).toContain('Nästa steg')
    expect(wrapper.find('[data-test="student-section-unlocking"]').text())
      .toContain('Låses upp härnäst')

    const nextPart = wrapper.find('[data-test="student-part-l1"]').text()
    expect(nextPart).toContain('Teori A')

    const lockedPart = wrapper.find('[data-test="student-part-l2"]').text()
    expect(lockedPart).toContain('Teori B')
    expect(lockedPart).not.toContain('Låst av')
    expect(lockedPart).not.toContain('Teori A')

    await wrapper.find('[data-test="student-open-l2"]').trigger('click')
    await flushPromises()

    const detail = wrapper.find('[data-test="student-detail"]').text()
    expect(detail).toContain('Den här delen är låst')
    expect(detail).toContain('Öppnas när du har klarat de delar den väntar på. Börja med delarna i Nästa steg.')
    expect(detail).not.toContain('Teori A')
    expect(detail).not.toContain('Låst av')
    expect(detail).not.toContain('Förutsättningar')
    expect(detail).not.toContain('Hemligt innehåll')
  })

  it('visar innehållet och vad en del låser upp i detaljvyn', async () => {
    backend([theory, locked])
    const {wrapper} = await renderApp('/dev/student')

    await wrapper.find('[data-test="student-open-l1"]').trigger('click')
    await flushPromises()

    const detail = wrapper.find('[data-test="student-detail"]').text()
    expect(detail).toContain('Läs kapitel 1')
    expect(detail).toContain('När du är klar med den här delen öppnas:')
    expect(detail).toContain('Teori B')
  })

  it('visar bara ett lager av låsta delar som nästa upplåsning', async () => {
    backend([theory, locked, hiddenBlocker])
    const {wrapper} = await renderApp('/dev/student')

    expect(wrapper.find('[data-test="student-part-l2"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="student-part-l5"]').exists()).toBe(false)

    await openSection(wrapper, 'later')

    expect(wrapper.find('[data-test="student-part-l5"]').text()).toContain('Teori C')
    expect(wrapper.find('[data-test="student-part-l5"]').text()).not.toContain('Låst av')
    expect(wrapper.find('[data-test="student-part-l5"]').text())
      .not.toContain('En del som inte är publicerad än')
  })

  it('filtrerar fram bara det som kan göras nu', async () => {
    backend([theory, locked])
    const {wrapper} = await renderApp('/dev/student')

    await wrapper.find('[data-test="student-filter-todo"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="student-part-l1"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="student-part-l2"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="student-section-unlocking"]').exists()).toBe(false)
  })

  it('klarmarkerar en teori och uppdaterar veckan', async () => {
    backend([theory, locked])
    const {wrapper} = await renderApp('/dev/student')

    await wrapper.find('[data-test="complete-l1"]').trigger('click')
    await flushPromises()

    expect(sent('PUT', '/api/groups/g1/courses/c1/links/l1/completion')).toEqual({completed: true})
    await openSection(wrapper, 'completed')
    expect(wrapper.find('[data-test="undo-l1"]').exists()).toBe(true)
  })

  it('ångrar en klarmarkering', async () => {
    backend([completed])
    const {wrapper} = await renderApp('/dev/student')

    await openSection(wrapper, 'completed')
    await wrapper.find('[data-test="undo-l6"]').trigger('click')
    await flushPromises()

    expect(sent('PUT', '/api/groups/g1/courses/c1/links/l6/completion')).toEqual({completed: false})
    expect(wrapper.find('[data-test="complete-l6"]').exists()).toBe(true)
  })

  it('visar inlämningens status och betyg', async () => {
    backend([submitted, graded])
    const {wrapper} = await renderApp('/dev/student')

    await openSection(wrapper, 'completed')

    expect(wrapper.text()).toContain('Inlämnad')
    expect(wrapper.text()).toContain('Godkänd (G)')
    expect(wrapper.find('[data-test="complete-l3"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="complete-l4"]').exists()).toBe(false)
  })

  it('visar en framtida vecka som ett låst skal utan innehåll', async () => {
    const future = week([theory], {
      id: 'm2',
      title: 'Vecka 2',
      description: 'Hemlig beskrivning',
      position: 2,
      publishAt: '2026-11-03T08:00:00Z',
      isAvailable: false,
    })
    mockApi((_method, path) => {
      if (path === '/api/groups') {
        return {status: 200, body: [group]}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/groups/g1/courses/c1/modules') {
        return {status: 200, body: [week([theory]), future]}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/dev/student')

    const shell = wrapper.find('[data-test="student-module-locked"]')
    expect(shell.exists()).toBe(true)
    expect(shell.text()).toContain('Vecka 2')
    expect(shell.text()).toContain('Öppnar')
    expect(shell.text()).not.toContain('Hemlig beskrivning')
    expect(shell.text()).not.toContain('Läs kapitel 1')
    expect(wrapper.find('[data-test="student-part-l1"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('1 delar')
    expect(wrapper.text()).toContain('0 av 1 klara')
  })

  it('döljer framtida veckor när eleven filtrerar', async () => {
    const future = week([], {
      id: 'm2',
      title: 'Vecka 2',
      position: 2,
      publishAt: '2026-11-03T08:00:00Z',
      isAvailable: false,
    })
    mockApi((_method, path) => {
      if (path === '/api/groups') {
        return {status: 200, body: [group]}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/groups/g1/courses/c1/modules') {
        return {status: 200, body: [week([theory]), future]}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/dev/student')
    await wrapper.find('[data-test="student-filter-todo"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="student-module-locked"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="student-part-l1"]').exists()).toBe(true)
  })
})
