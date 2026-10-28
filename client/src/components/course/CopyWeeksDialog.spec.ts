import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const courses = [
  {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps HT26'},
  {id: 'c2', schoolId: 's1', code: 'DEVOPS25', title: 'DevOps HT25'},
  {id: 'c3', schoolId: 's1', code: 'JAVA', title: 'Java (inte min)'},
]

const part = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Theory',
  gradeScale: null,
  completionRule: null,
  title: 'Teori',
  status: 'Published',
  groupId: null,
  publishAt: '2025-01-01T08:00:00Z',
  dueAt: null,
  isPinned: false,
  blockedBy: [],
  hasStudentActivity: false,
}

function week(id: string, courseId: string, title: string, parts: number) {
  return {
    id,
    courseId,
    title,
    description: '',
    position: 1,
    publishAt: null,
    content: Array.from({length: parts}, (_, index) => ({...part, linkId: `${id}-${index}`})),
  }
}

const sourceWeeks = [week('s1', 'c2', 'Vecka 1', 4), week('s2', 'c2', 'Vecka 2', 1), week('s3', 'c2', 'Vecka 3', 6)]

function backend(targetWeeks: unknown[] = [week('m1', 'c1', 'Vecka 1', 0)]) {
  mockApi((method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1', 'c2'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: courses}
    }
    if (method === 'GET' && path === '/api/courses/c2/modules') {
      return {status: 200, body: sourceWeeks}
    }
    if (method === 'GET' && path === '/api/courses/c1/modules') {
      return {status: 200, body: targetWeeks}
    }
    if (method === 'POST' && path === '/api/courses/c1/modules/copy') {
      return {status: 201, body: {modules: [], partCount: 5}}
    }
    return {status: 200, body: []}
  })
}

function dialog(): HTMLElement {
  return document.querySelector('[role="dialog"]') as HTMLElement
}

function checkbox(weekId: string): HTMLElement {
  return document.querySelector(`[data-test="copy-week-${weekId}"]`) as HTMLElement
}

async function openDialog() {
  const {wrapper} = await renderApp('/courses/c1/content')
  await wrapper.find('[data-test="add-part-menu"]').trigger('keydown', {key: 'Enter'})
  await flushPromises()
  const entry = [...document.querySelectorAll('[role="menuitem"]')]
    .find((item) => item.textContent?.trim() === 'Kopiera från annan kurs') as HTMLElement
  entry.click()
  await flushPromises()
  return wrapper
}

async function chooseSource(id: string) {
  const select = dialog().querySelector('select') as HTMLSelectElement
  select.value = id
  select.dispatchEvent(new Event('change'))
  await flushPromises()
}

describe('CopyWeeksDialog', () => {
  it('erbjuder bara andra kurser som läraren undervisar i', async () => {
    backend()
    await openDialog()

    const options = [...dialog().querySelectorAll('option')].map((option) => option.textContent?.trim())

    expect(options).toEqual(['Välj kurs…', 'DevOps HT25'])
  })

  it('listar källkursens veckor med antal delar och räknar ihop vad som kopieras', async () => {
    backend()
    await openDialog()
    await chooseSource('c2')

    expect(dialog().textContent).toContain('Vecka 1')
    expect(dialog().textContent).toContain('4 delar')
    expect(dialog().textContent).toContain('1 del')
    expect(dialog().querySelector('[data-test="copy-preview"]')).toBeNull()

    checkbox('s1').click()
    checkbox('s2').click()
    await flushPromises()

    expect(dialog().querySelector('[data-test="copy-preview"]')?.textContent?.trim())
      .toBe('2 veckor, 5 delar kopieras')
  })

  it('skickar de valda veckorna och visar resultatet', async () => {
    backend()
    const wrapper = await openDialog()
    await chooseSource('c2')
    checkbox('s1').click()
    checkbox('s3').click()
    await flushPromises()

    ;(dialog().querySelector('[data-test="confirm-copy"]') as HTMLElement).click()
    await flushPromises()

    expect(sent('POST', '/api/courses/c1/modules/copy')).toEqual({
      sourceCourseId: 'c2',
      moduleIds: ['s1', 's3'],
    })
    expect(requests.filter((request) => request.method === 'GET' && request.path === '/api/courses/c1/modules'))
      .toHaveLength(2)
    expect(wrapper.text()).toContain('kopierades som utkast')
  })

  it('kan inte kopiera innan en vecka är vald', async () => {
    backend()
    await openDialog()
    await chooseSource('c2')

    const button = dialog().querySelector('[data-test="confirm-copy"]') as HTMLButtonElement

    expect(button.disabled).toBe(true)
  })

  it('en kurs utan veckor kan kopiera till sig från tomma läget', async () => {
    backend([])
    const {wrapper} = await renderApp('/courses/c1/content')

    await wrapper.find('[data-test="copy-weeks-empty"]').trigger('click')
    await flushPromises()

    expect(dialog().textContent).toContain('Kopiera från annan kurs')
  })
})
