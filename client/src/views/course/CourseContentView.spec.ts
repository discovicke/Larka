import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, sent} from '@/test/mockApi'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

function week(id: string, title: string, content: unknown[] = []) {
  return {id, courseId: 'c1', title, description: '', position: 1, content}
}

const submission = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Assignment',
  gradeScale: 'PassFailDistinction',
  title: 'Release-pipeline',
  status: 'Published',
  groupId: null,
  publishAt: '2026-01-01T08:00:00Z',
  dueAt: '2026-10-23T21:59:00Z',
  isPinned: false,
}
const exercise = {...submission, linkId: 'l2', gradeScale: 'None', title: 'Docker-övning', dueAt: null}
const draft = {...submission, linkId: 'l3', type: 'Theory', gradeScale: null, title: 'Changelog', status: 'Draft', dueAt: null}

function backend(modules: unknown[]) {
  mockApi((_method, path) => {
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: modules}
    }
    return {status: 200, body: []}
  })
}

describe('CourseContentView', () => {
  it('visar veckorna med sina delar, typ, deadline och status', async () => {
    backend([week('m1', 'Vecka 1', [submission, exercise, draft]), week('m2', 'Vecka 2')])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.findAll('section h2').map((heading) => heading.text())).toEqual(['Vecka 1', 'Vecka 2'])
    const items = wrapper.findAll('[data-test="module-item"]').map((item) => item.text())
    expect(items[0]).toContain('Release-pipeline')
    expect(items[0]).toContain('Inlämning · fre 23 okt')
    expect(items[0]).toContain('Publicerad')
    expect(items[1]).toContain('Övning')
    expect(items[2]).toContain('Teori')
    expect(items[2]).toContain('Utkast')
  })

  it('en kurs utan veckor visar hur man kommer igång', async () => {
    backend([])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.text()).toContain('Inga veckor än')
  })

  it('flytta ner skickar den nya ordningen', async () => {
    backend([week('m1', 'Vecka 1'), week('m2', 'Vecka 2')])
    const {wrapper} = await renderApp('/courses/c1/content')

    await wrapper.findAll('[aria-label="Fler val för veckan"]')[0].trigger('keydown', {key: 'Enter'})
    await flushPromises()
    const moveDown = [...document.querySelectorAll('[role="menuitem"]')].find((item) => item.textContent?.includes('Flytta ner'))
    ;(moveDown as HTMLElement).click()
    await flushPromises()

    expect(sent('PUT', '/api/courses/c1/modules/order')).toEqual({moduleIds: ['m2', 'm1']})
  })

  it('lägg till del går till formuläret för veckan', async () => {
    backend([week('m1', 'Vecka 1')])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.find('[data-test="add-part"]').attributes('href')).toBe('/courses/c1/modules/m1/new')
  })
})
