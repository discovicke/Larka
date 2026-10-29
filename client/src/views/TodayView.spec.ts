import {flushPromises} from '@vue/test-utils'
import {afterEach, beforeEach, describe, expect, it} from 'vitest'

import {mockApi} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'
import {vi} from 'vitest'

// Lokala tider, så att testet inte beror på tidszonen.
function local(month: number, day: number, hour = 23, minute = 59) {
  return new Date(2026, month - 1, day, hour, minute).toISOString()
}

function todo(linkId: string, title: string, dueAt: string, extra: Record<string, unknown> = {}) {
  return {
    linkId,
    contentId: `x-${linkId}`,
    title,
    courseId: 'c1',
    courseTitle: 'DevOps',
    dueAt,
    status: 'NotStarted',
    ...extra,
  }
}

const full = {
  todo: [
    todo('l1', 'Release-pipeline', local(10, 23)),
    todo('l2', 'E2E-tester', local(10, 25)),
    todo('l3', 'Inlämningsuppgift 1', local(11, 6), {status: 'NeedsWork'}),
  ],
  submitted: [
    {
      linkId: 's1',
      title: 'Testplan',
      courseId: 'c1',
      courseTitle: 'DevOps',
      submittedAt: local(10, 18, 9, 0),
      status: 'Graded',
      grade: 'Pass',
    },
  ],
  feedback: [
    {
      linkId: 's1',
      title: 'Testplan',
      courseId: 'c1',
      courseTitle: 'DevOps',
      grade: 'Pass',
      feedback: 'Tydlig plan',
      gradedAt: local(10, 20, 11, 20),
    },
  ],
  news: [
    {
      contentId: 'n1',
      title: 'Gästföreläsning',
      courseId: 'c1',
      source: 'DevOps',
      publishAt: local(10, 20, 8, 0),
    },
  ],
  courses: [{courseId: 'c1', code: 'DEVOPS', title: 'DevOps', totalParts: 24, completedParts: 9}],
}

const empty = {todo: [], submitted: [], feedback: [], news: [], courses: []}

function backend(today: unknown = full, options: {fail?: boolean} = {}) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Student', ['c1'], 'Christian Gennari')}
    }
    if (path === '/api/courses') {
      return {status: 200, body: []}
    }
    if (path === '/api/me/today') {
      return options.fail
        ? {status: 500, body: 'Server error'}
        : {status: 200, body: today}
    }
    return {status: 404}
  })
}

beforeEach(() => {
  vi.useFakeTimers({toFake: ['Date']})
  vi.setSystemTime(new Date(2026, 9, 21, 9, 0))
})

afterEach(() => {
  vi.useRealTimers()
})

describe('TodayView', () => {
  it('visar hälsning, datum och hur många saker som förfaller den här veckan', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const desktop = wrapper.find('[data-test="today-desktop"]')
    expect(desktop.find('h1').text()).toBe('God morgon, Christian')
    expect(desktop.text()).toContain('Onsdag 21 oktober · vecka 43 · 2 saker förfaller den här veckan')
  })

  it('delar Att göra i den här veckan och senare', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const thisWeek = wrapper.find('[data-test="group-Den här veckan"]')
    const later = wrapper.find('[data-test="group-Senare"]')
    expect(thisWeek.findAll('[data-test="todo-item"]').map((row) => row.text()))
      .toEqual([expect.stringContaining('Release-pipeline'), expect.stringContaining('E2E-tester')])
    expect(later.findAll('[data-test="todo-item"]')).toHaveLength(1)
    expect(later.text()).toContain('Inlämningsuppgift 1')
    expect(later.text()).toContain('Komplettering begärd')
    expect(thisWeek.text()).toContain('om 2 dagar')
    expect(thisWeek.find('[data-test="todo-due"]').text()).toBe('Fre 23 okt 23:59')
  })

  it('lägger försenade uppgifter först under Försenat, i rött', async () => {
    backend({
      ...full,
      todo: [todo('l0', 'Sen uppgift', local(10, 19)), todo('l1', 'Snart', local(10, 22)), ...full.todo.slice(1)],
    })

    const {wrapper} = await renderApp('/')

    const groups = wrapper.findAll('[data-test="today-desktop"] [data-test^="group-"]')
    expect(groups[0]?.attributes('data-test')).toBe('group-Försenat')
    const overdue = groups[0]!.find('[data-test="todo-item"]')
    expect(overdue.text()).toContain('Sen uppgift')
    expect(overdue.find('.text-destructive').exists()).toBe(true)
    const soon = wrapper.find('[data-test="group-Den här veckan"] [data-test="todo-item"]')
    expect(soon.find('.text-warning').exists()).toBe(true)
  })

  it('länkar uppgifter och återkoppling till delen på kurssidan', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="todo-item"]').attributes('href')).toBe('/courses/c1/content?del=l1')
    expect(wrapper.find('[data-test="feedback-item"]').attributes('href')).toBe('/courses/c1/content?del=s1')
  })

  it('byter till Inlämnat', async () => {
    backend()
    const {wrapper} = await renderApp('/')

    await wrapper.find('[data-test="tab-submitted"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="todo-item"]').exists()).toBe(false)
    const row = wrapper.find('[data-test="submitted-item"]')
    expect(row.text()).toContain('Testplan')
    expect(row.text()).toContain('Bedömd · G')
  })

  it('visar återkoppling, nyheter och kurser med framsteg', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const feedback = wrapper.find('[data-test="feedback-item"]')
    expect(feedback.text()).toContain('G')
    expect(feedback.text()).toContain('Tydlig plan')
    expect(wrapper.find('[data-test="news-item"]').text()).toContain('Gästföreläsning')
    const card = wrapper.find('[data-test="course-card"]')
    expect(card.text()).toContain('DEVOPS')
    expect(card.text()).toContain('9 av 24 klara')
    expect(card.find('[role="progressbar"]').attributes('aria-valuenow')).toBe('38')
  })

  it('visar tomma lägen i korten', async () => {
    backend(empty)

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="todo-empty"]').text()).toBe('Inget att göra just nu')
    expect(wrapper.find('[data-test="feedback-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="news-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="courses-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="today-desktop"]').text()).toContain('Inget förfaller den här veckan')
  })

  it('visar Närmast-kortet och resten i listan på mobilen', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const mobile = wrapper.find('[data-test="today-mobile"]')
    expect(mobile.find('h1').text()).toBe('Hej Christian')
    const nextUp = mobile.find('[data-test="next-up"]')
    expect(nextUp.text()).toContain('Närmast')
    expect(nextUp.text()).toContain('Release-pipeline')
    expect(nextUp.text()).toContain('fre 23 okt 23:59')
    expect(mobile.findAll('[data-test="mobile-todo-item"]').map((row) => row.text()))
      .toEqual([expect.stringContaining('E2E-tester'), expect.stringContaining('Inlämningsuppgift 1')])
    expect(mobile.findAll('[data-test="mobile-recent-item"]')).toHaveLength(2)
  })

  it('visar en kurs utan delar utan framsteg', async () => {
    backend({...full, courses: [{courseId: 'c1', code: 'DEVOPS', title: 'DevOps', totalParts: 0, completedParts: 0}]})

    const {wrapper} = await renderApp('/')

    const card = wrapper.find('[data-test="course-card"]')
    expect(card.text()).toContain('Inget innehåll än')
    expect(card.text()).not.toContain('0 av 0')
    expect(card.find('[role="progressbar"]').exists()).toBe(false)
  })

  it('visar tomt-raden på mobilen när inget finns efter Närmast', async () => {
    backend({...full, todo: [full.todo[0]]})

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="today-mobile"] [data-test="todo-empty-mobile"]').text())
      .toBe('Inget mer att göra just nu')
  })

  it('visar ett fel när sidan inte kan hämtas', async () => {
    backend(full, {fail: true})

    const {wrapper} = await renderApp('/')

    expect(wrapper.text()).toContain('Kunde inte hämta din översikt.')
  })

  it('visar lärarens översikt för en lärare, utan elevens listor', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'], 'Anna Lärare')}
      }
      if (path === '/api/courses') {
        return {status: 200, body: []}
      }
      if (path === '/api/me/teaching') {
        return {status: 200, body: {toGrade: [], toGradeTotal: 0, deadlines: [], scheduled: [], courses: []}}
      }
      return {status: 404}
    })

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="teacher-dashboard"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="today-desktop"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="todo-item"]').exists()).toBe(false)
  })

  it('visar lärarens översikt för den som bara är lärare i en kurs', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: {...meAs('Student'), courses: [{courseId: 'c1', role: 'Teacher'}]}}
      }
      if (path === '/api/courses') {
        return {status: 200, body: []}
      }
      if (path === '/api/me/teaching') {
        return {status: 200, body: {toGrade: [], toGradeTotal: 0, deadlines: [], scheduled: [], courses: []}}
      }
      return {status: 404}
    })

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="teacher-dashboard"]').exists()).toBe(true)
  })

  it('visar elevens sida för en elev', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="today-desktop"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="teacher-dashboard"]').exists()).toBe(false)
  })
})
