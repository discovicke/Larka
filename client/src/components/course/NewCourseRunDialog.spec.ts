import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const courses = [
  {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps HT25', description: '', points: 25, startsOn: null, endsOn: null},
  {id: 'c2', schoolId: 's1', code: 'JAVA', title: 'Java', description: '', points: null, startsOn: null, endsOn: null},
  {id: 'n1', schoolId: 's1', code: 'NET26-DEVOPS', title: 'DevOps HT25', description: '', points: 25, startsOn: null, endsOn: null},
]

const groups = [
  {id: 'g1', schoolId: 's1', type: 'Class', name: 'NET25', term: 'HT25'},
  {id: 'g2', schoolId: 's1', type: 'Class', name: 'NET26', term: 'HT26'},
  {id: 'g3', schoolId: 's1', type: 'Committee', name: 'Elevråd', term: ''},
  {id: 'g4', schoolId: 's2', type: 'Class', name: 'Annan skola', term: ''},
]

const part = {
  linkId: 'l1', contentId: 'x1', type: 'Theory', gradeScale: null, completionRule: null, title: 'Teori',
  status: 'Published', groupId: null, publishAt: '2025-01-01T08:00:00Z', dueAt: null, isPinned: false,
  blockedBy: [], hasStudentActivity: false,
}

const weeks = [
  {id: 'w1', courseId: 'c1', title: 'Vecka 1', description: '', position: 1, publishAt: null, content: [part, part, part]},
  {id: 'w2', courseId: 'c1', title: 'Vecka 2', description: '', position: 2, publishAt: null, content: [part]},
]

function backend(role: 'Teacher' | 'Student' | 'Admin' = 'Teacher', teaches: string[] = ['c1']) {
  mockApi((method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs(role, teaches)}
    }
    if (path === '/api/courses') {
      return {status: 200, body: courses}
    }
    if (path === '/api/groups') {
      return {status: 200, body: groups}
    }
    if (method === 'GET' && path === '/api/courses/c1/modules') {
      return {status: 200, body: weeks}
    }
    if (method === 'POST' && path === '/api/courses/c1/runs') {
      return {status: 201, body: {courseId: 'n1', groupId: 'g2', moduleCount: 2, partCount: 4, staffCount: 1}}
    }
    return {status: 200, body: []}
  })
}

function dialog(): HTMLElement {
  return document.querySelector('[role="dialog"]') as HTMLElement
}

function field(name: string): HTMLInputElement | HTMLSelectElement {
  return dialog().querySelector(`[data-test="${name}"]`) as HTMLInputElement
}

async function type(name: string, value: string, event = 'input') {
  const element = field(name)
  element.value = value
  element.dispatchEvent(new Event(event))
  await flushPromises()
}

async function openDialog() {
  const rendered = await renderApp('/courses/c1/content')
  await rendered.wrapper.find('[data-test="course-menu"]').trigger('keydown', {key: 'Enter'})
  await flushPromises()
  const entry = [...document.querySelectorAll('[role="menuitem"]')]
    .find((item) => item.textContent?.includes('Ny kursomgång')) as HTMLElement
  entry.click()
  await flushPromises()
  return rendered
}

async function fillForm() {
  await type('run-code', 'NET26-DEVOPS')
  await type('run-class', 'g2', 'change')
  await type('run-starts', '2026-10-12')
  await type('run-ends', '2026-11-13')
}

describe('NewCourseRunDialog', () => {
  it('visas för lärare i kursen men inte för elever eller lärare i andra kurser', async () => {
    backend('Student', ['c1'])
    const student = await renderApp('/courses/c1/content')
    expect(student.wrapper.find('[data-test="course-menu"]').exists()).toBe(false)

    backend('Teacher', ['c2'])
    const other = await renderApp('/courses/c1/content')
    expect(other.wrapper.find('[data-test="course-menu"]').exists()).toBe(false)

    backend('Admin', [])
    const admin = await renderApp('/courses/c1/content')
    expect(admin.wrapper.find('[data-test="course-menu"]').exists()).toBe(true)
  })

  it('visar titeln, bara klasser i skolan och hur mycket som kopieras', async () => {
    backend()
    await openDialog()

    expect(dialog().textContent).toContain('Ny kursomgång av DevOps HT25')
    expect(field('run-title').textContent).toContain('DevOps HT25')
    expect([...dialog().querySelectorAll('option')].map((option) => option.textContent?.trim()))
      .toEqual(['Välj klass…', 'NET25 · HT25', 'NET26 · HT26'])
    expect(dialog().querySelector('[data-test="run-copy-label"]')?.textContent)
      .toBe('Kopiera alla veckor och delar (2 veckor, 4 delar)')
    expect(dialog().textContent).toContain(
      'Deadlines flyttas lika mycket som startdatumen skiljer sig åt. Allt startar som utkast.')
    expect(field('run-copy').getAttribute('data-state')).toBe('checked')
  })

  it('kan inte skapas innan kod, klass och datum är ifyllda', async () => {
    backend()
    await openDialog()
    const button = field('confirm-run') as HTMLButtonElement

    expect(button.disabled).toBe(true)

    await fillForm()

    expect(button.disabled).toBe(false)
  })

  it('skickar formuläret och öppnar den nya kursen med ett meddelande', async () => {
    backend()
    const {router} = await openDialog()
    await fillForm()

    field('confirm-run').click()
    await flushPromises()
    await flushPromises()

    expect(sent('POST', '/api/courses/c1/runs')).toEqual({
      code: 'NET26-DEVOPS',
      groupId: 'g2',
      startsOn: '2026-10-12',
      endsOn: '2026-11-13',
      copyContent: true,
    })
    expect(router.currentRoute.value.fullPath).toBe('/courses/n1/content')
    expect(document.querySelector('[role="dialog"]')).toBeNull()
    expect(document.body.textContent).toContain(
      'Kursomgången skapades för NET26. 2 veckor och 4 delar kopierades som utkast.')
    // Kurserna och användaren hämtas om, så att den nya kursen och lärarrollen i den finns.
    expect(requests.filter((request) => request.path === '/api/courses')).toHaveLength(2)
    expect(requests.filter((request) => request.path === '/api/me')).toHaveLength(2)
  })

  it('skickar copyContent false när rutan avmarkeras', async () => {
    backend()
    await openDialog()
    await fillForm()

    field('run-copy').click()
    await flushPromises()
    field('confirm-run').click()
    await flushPromises()

    expect(sent('POST', '/api/courses/c1/runs')).toMatchObject({copyContent: false})
  })
})
