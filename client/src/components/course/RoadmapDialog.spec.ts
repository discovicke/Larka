import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, sent} from '@/test/mockApi'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

const theory = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Theory',
  gradeScale: null,
  completionRule: null,
  title: 'Teori A',
  status: 'Published',
  groupId: null,
  publishAt: '2026-01-01T08:00:00Z',
  dueAt: null,
  isPinned: false,
  blockedBy: [],
}

const submission = {
  ...theory,
  linkId: 'l2',
  contentId: 'x2',
  type: 'Assignment',
  gradeScale: 'PassFail',
  completionRule: 'Submitted',
  title: 'Inlämning',
  blockedBy: ['l1'],
}

const exercise = {
  ...theory,
  linkId: 'l3',
  contentId: 'x3',
  type: 'Assignment',
  gradeScale: 'None',
  title: 'Övning',
  blockedBy: ['l1'],
}

const draft = {
  ...theory,
  linkId: 'l4',
  contentId: 'x4',
  title: 'Utkast',
  status: 'Draft',
}

function week(content: unknown[]) {
  return {id: 'm1', courseId: 'c1', title: 'Vecka 1', description: '', position: 1, content}
}

function backend(content: unknown[]) {
  mockApi((method, path) => {
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: [week(content)]}
    }
    if (method === 'PUT' && path === '/api/courses/c1/modules/m1/roadmap') {
      return {status: 200, body: week(content)}
    }
    return {status: 200, body: []}
  })
}

function partAction(label: string): HTMLElement {
  const item = [...document.querySelectorAll('[role="menuitem"]')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!item) {
    throw new Error(`No menu item: ${label}`)
  }
  return item as HTMLElement
}

async function openRoadmap() {
  const {wrapper} = await renderApp('/courses/c1/content')
  await wrapper.find('[aria-label="Fler val för veckan"]').trigger('keydown', {key: 'Enter'})
  await flushPromises()
  partAction('Roadmap').click()
  await flushPromises()
  return {wrapper, dialog: document.querySelector('[role="dialog"]') as HTMLElement}
}

function row(linkId: string): HTMLElement {
  return document.querySelector(`[data-test="roadmap-part-${linkId}"]`) as HTMLElement
}

function selects(linkId: string): HTMLSelectElement[] {
  return [...row(linkId).querySelectorAll('select')]
}

async function choose(linkId: string, index: number, value: string) {
  const select = selects(linkId)[index]
  select.value = value
  select.dispatchEvent(new Event('change'))
  await flushPromises()
}

function saveButton(): HTMLButtonElement {
  return document.querySelector('[data-test="save-roadmap"]') as HTMLButtonElement
}

describe('RoadmapDialog', () => {
  it('visar veckans delar, förutsättningar och vad de låser upp', async () => {
    backend([theory, submission, exercise, draft])

    const {dialog} = await openRoadmap()

    expect(dialog.textContent).toContain('Roadmap för Vecka 1')
    expect(row('l1').textContent).toContain('Tillgänglig direkt')
    expect(row('l1').textContent).toContain('Låser upp 2 delar')
    expect(row('l2').textContent).toContain('Låst av')
    expect(row('l2').textContent).toContain('Teori A')
    expect(selects('l2')).toHaveLength(2)
    expect(selects('l3')).toHaveLength(1)
  })

  it('sparar hela veckan med förutsättningar och vald regel', async () => {
    backend([theory, submission, exercise, draft])
    await openRoadmap()

    await choose('l2', 1, 'Approved')
    saveButton().click()
    await flushPromises()

    expect(sent('PUT', '/api/courses/c1/modules/m1/roadmap')).toEqual({
      links: [
        {linkId: 'l1', blockedByLinkIds: [], completionRule: null},
        {linkId: 'l2', blockedByLinkIds: ['l1'], completionRule: 'Approved'},
        {linkId: 'l3', blockedByLinkIds: ['l1'], completionRule: null},
        {linkId: 'l4', blockedByLinkIds: [], completionRule: null},
      ],
    })
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })

  it('lägger till en förutsättning', async () => {
    backend([theory, submission, exercise, draft])
    await openRoadmap()

    await choose('l3', 0, 'l2')
    saveButton().click()
    await flushPromises()

    expect(sent('PUT', '/api/courses/c1/modules/m1/roadmap')).toMatchObject({
      links: [
        {linkId: 'l1'},
        {linkId: 'l2'},
        {linkId: 'l3', blockedByLinkIds: ['l1', 'l2']},
        {linkId: 'l4'},
      ],
    })
  })

  it('visar en cykel och stoppar sparandet', async () => {
    backend([theory, submission])
    await openRoadmap()

    await choose('l1', 0, 'l2')

    const warning = document.querySelector('[data-test="roadmap-cycle"]')
    expect(warning?.textContent).toContain('Teori A → Inlämning → Teori A')
    expect(saveButton().disabled).toBe(true)
  })

  it('delar in veckan i steg och visar vad varje del låser upp', async () => {
    backend([theory, submission, exercise, draft])
    await openRoadmap()

    expect(document.querySelector('[data-test="roadmap-stage-0"]')?.textContent).toContain('Start')
    expect(document.querySelector('[data-test="roadmap-stage-0"]')?.textContent)
      .toContain('Tillgängliga direkt')
    expect(document.querySelector('[data-test="roadmap-stage-1"]')?.textContent).toContain('Steg 2')
    expect(row('l1').textContent).toContain('Låser upp 2 delar')
    expect(row('l1').textContent).toContain('Inlämning')
    expect(row('l1').textContent).toContain('Övning')
    expect(row('l2').textContent).toContain('Låst av')
    expect(row('l2').textContent).toContain('Teori A')
  })

  it('visar att ett utkast publiceras när roadmapen sparas', async () => {
    backend([theory, {...submission, blockedBy: ['l4']}, draft])
    await openRoadmap()

    expect(row('l4').textContent).toContain('publiceras automatiskt')
    expect(document.querySelector('[data-test="roadmap-warning"]')).toBeNull()
  })

  it('förhandsvisar hur en ny elev ser veckan', async () => {
    const scheduled = {
      ...theory,
      linkId: 'l7',
      contentId: 'x7',
      title: 'Schemalagd',
      publishAt: '2030-01-01T08:00:00Z',
    }
    backend([theory, submission, draft, scheduled])
    await openRoadmap()

    const preview = document.querySelector('[data-test="roadmap-preview"]') as HTMLElement
    preview.click()
    await flushPromises()

    expect(row('l1').textContent).toContain('Tillgänglig för en ny elev')
    expect(row('l2').textContent).toContain('Låst för en ny elev')
    // Utkast publiceras när roadmapen sparas, så en ny elev ser den direkt.
    expect(row('l4').textContent).toContain('Tillgänglig för en ny elev')
    expect(row('l7').textContent).toContain('Syns inte än')
  })

  it('en nyhet kan inte bli förutsättning eller låsas', async () => {
    const news = {
      ...theory,
      linkId: 'l5',
      contentId: 'x5',
      type: 'News',
      title: 'Nyhet',
    }
    backend([theory, news])
    await openRoadmap()

    expect(row('l5').textContent).toContain('Nyheter kan inte ingå i roadmapen')
    const candidates = [...selects('l1')[0].options].map((option) => option.value)
    expect(candidates).not.toContain('l5')
  })
})
