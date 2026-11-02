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
    expect(thisWeek.findAll('[data-test="todo-item"]').map((row) => row.text()))
      .toEqual([expect.stringContaining('Release-pipeline'), expect.stringContaining('E2E-tester')])
    expect(wrapper.find('[data-test="group-Senare"]').exists()).toBe(false)
    expect(thisWeek.text()).toContain('om 2 dagar')
    expect(thisWeek.find('[data-test="todo-due"]').text()).toBe('Fre 23 okt 23:59')
  })

  it('lägger delar att komplettera först i Att göra, före försenade', async () => {
    backend({
      ...full,
      todo: [todo('l0', 'Sen uppgift', local(10, 19)), ...full.todo],
    })

    const {wrapper} = await renderApp('/')

    const groups = wrapper.findAll('[data-test="today-desktop"] [data-test^="group-"]')
    expect(groups[0]?.attributes('data-test')).toBe('group-Att komplettera')
    expect(groups[0]!.text()).toContain('Inlämningsuppgift 1')
    expect(groups[0]!.text()).toContain('Komplettera')
    expect(groups[1]?.attributes('data-test')).toBe('group-Försenat')
  })

  it('lägger försenade uppgifter först under Försenat, med ett Försenat-märke', async () => {
    backend({
      ...full,
      todo: [todo('l0', 'Sen uppgift', local(10, 19)), todo('l1', 'Snart', local(10, 22)), ...full.todo.slice(1, 2)],
    })

    const {wrapper} = await renderApp('/')

    const groups = wrapper.findAll('[data-test="today-desktop"] [data-test^="group-"]')
    expect(groups[0]?.attributes('data-test')).toBe('group-Försenat')
    const overdue = groups[0]!.find('[data-test="todo-item"]')
    expect(overdue.text()).toContain('Sen uppgift')
    expect(groups[0]!.find('[data-status="deadline.overdue"]').exists()).toBe(true)
    expect(overdue.find('[data-status="deadline.overdue"]').exists()).toBe(false)
    const soon = wrapper.find('[data-test="group-Den här veckan"] [data-test="todo-item"]')
    expect(soon.find('[data-status="deadline.soon"]').exists()).toBe(true)
  })

  it('länkar uppgifter och återkoppling till delen på kurssidan', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="todo-item"]').attributes('href')).toBe('/courses/c1/content?del=l3')
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
    expect(row.text()).toContain('Godkänd')
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

  it('Mina kurser visar bara pågående och kommande kurser', async () => {
    const oldCourse = {courseId: 'c9', code: 'OLD', title: 'Gammal kurs', totalParts: 4, completedParts: 4}
    backend({
      ...full,
      courses: [
        {...oldCourse, startsOn: '2020-01-01', endsOn: '2020-06-01'},
        {...full.courses[0], startsOn: null, endsOn: null},
      ],
    })

    const {wrapper} = await renderApp('/')

    const cards = wrapper.findAll('[data-test="course-card"]')
    expect(cards).toHaveLength(1)
    expect(cards[0].text()).toContain('DevOps')
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
    // Delen att komplettera går före, som på datorn, och kortet säger att den ska kompletteras.
    expect(nextUp.find('[data-test="next-up-label"]').text()).toBe('Att komplettera')
    expect(nextUp.text()).toContain('Inlämningsuppgift 1')
    expect(nextUp.text()).toContain('fre 6 nov 23:59')
    expect(mobile.findAll('[data-test="mobile-todo-item"]').map((row) => row.text()))
      .toEqual([expect.stringContaining('Release-pipeline'), expect.stringContaining('E2E-tester')])
    expect(mobile.findAll('[data-test="mobile-recent-item"]')).toHaveLength(2)
  })

  it('visar Komplettera-märket och återkopplingen att komplettera på mobilen', async () => {
    backend({
      ...full,
      todo: [todo('l1', 'Release-pipeline', local(10, 23)), ...full.todo.slice(2)],
      feedback: [
        {
          linkId: 'l3',
          title: 'Inlämningsuppgift 1',
          courseId: 'c1',
          courseTitle: 'DevOps',
          grade: null,
          feedback: 'Lägg till tester',
          gradedAt: local(10, 20, 12, 0),
        },
        ...full.feedback,
      ],
    })

    const {wrapper} = await renderApp('/')

    const mobile = wrapper.find('[data-test="today-mobile"]')
    const recent = mobile.findAll('[data-test="mobile-recent-item"]').map((row) => row.text())
    expect(recent).toContainEqual(expect.stringContaining('Komplettera'))
    expect(recent).toContainEqual(expect.stringContaining('Inlämningsuppgift 1'))
    const row = mobile.find('[data-test="mobile-todo-item"]')
    expect(row.text()).toContain('Release-pipeline')
    expect(mobile.find('[data-test="next-up"]').text()).toContain('Inlämningsuppgift 1')
  })

  it('en komplettering med passerad deadline står först utan att räknas som att den förfaller', async () => {
    const olderNews = [1, 2, 3, 4, 5].map((index) => ({
      contentId: `n${index}`,
      title: `Nyhet ${index}`,
      courseId: 'c1',
      source: 'DevOps',
      publishAt: local(10, 20, 8, index),
    }))
    backend({
      ...full,
      todo: [todo('l9', 'CI/CD', local(10, 14), {status: 'NeedsWork'}), todo('l1', 'Release-pipeline', local(10, 23))],
      feedback: [
        {
          linkId: 'l9',
          title: 'CI/CD',
          courseId: 'c1',
          courseTitle: 'DevOps',
          grade: null,
          feedback: 'Pipelinen kör inte testerna',
          gradedAt: local(10, 15, 12, 0),
        },
      ],
      news: olderNews,
    })

    const {wrapper} = await renderApp('/')

    // Bara Release-pipeline förfaller den här veckan. CI/CD:s deadline har redan passerat.
    expect(wrapper.find('[data-test="today-desktop"]').text()).toContain('1 sak förfaller den här veckan')
    const mobile = wrapper.find('[data-test="today-mobile"]')
    const nextUp = mobile.find('[data-test="next-up"]')
    expect(nextUp.find('[data-test="next-up-label"]').text()).toBe('Att komplettera')
    expect(nextUp.find('[data-test="next-up-feedback"]').text()).toBe('Pipelinen kör inte testerna')
    expect(nextUp.text()).toContain('Deadline passerad')
    // Återkopplingen på kompletteringen står först i Nytt fast nyheterna är nyare.
    expect(mobile.findAll('[data-test="mobile-recent-item"]')[0].text()).toContain('CI/CD')
  })

  it('visar Komplettera-märket i mobilens lista', async () => {
    backend({
      ...full,
      todo: [todo('l1', 'Release-pipeline', local(10, 23)), todo('l3', 'Inlämningsuppgift 1', local(11, 6), {status: 'NeedsWork'}), todo('l4', 'Tredje', local(11, 7), {status: 'NeedsWork'})],
    })

    const {wrapper} = await renderApp('/')

    const rows = wrapper.findAll('[data-test="today-mobile"] [data-test="mobile-todo-item"]')
    expect(rows.map((row) => row.text())).toEqual([
      expect.stringContaining('Tredje'),
      expect.stringContaining('Release-pipeline'),
    ])
    expect(rows[0]!.find('[data-status="submission.needsWork"]').exists()).toBe(true)
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

  it('en ny elev utan klass får veta att admin lägger in hen', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Student', [], 'Ny Elev')}
      }
      if (path === '/api/courses') {
        return {status: 200, body: []}
      }
      if (path === '/api/me/today') {
        return {status: 200, body: empty}
      }
      return {status: 404}
    })

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="today-no-class"]').text()).toContain('Du är inte med i någon klass än')
    expect(wrapper.find('[data-test="today-desktop"]').exists()).toBe(false)
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
