import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import {mockApi} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

// Lokala tider, så att testet inte beror på tidszonen.
function local(month: number, day: number, hour = 12, minute = 0) {
  return new Date(2026, month - 1, day, hour, minute).toISOString()
}

const full = {
  toGrade: [
    {
      submissionId: 'sub1',
      linkId: 'l1',
      title: 'Release-pipeline',
      courseId: 'c1',
      courseTitle: 'DevOps',
      studentId: 'st1',
      studentName: 'Maja Svensson',
      submittedAt: local(10, 21, 7, 0),
    },
    {
      submissionId: 'sub2',
      linkId: 'l2',
      title: 'E2E-tester',
      courseId: 'c1',
      courseTitle: 'DevOps',
      studentId: 'st2',
      studentName: 'Omar Ali',
      submittedAt: local(10, 20, 9, 0),
    },
  ],
  toGradeTotal: 11,
  deadlines: [
    {
      linkId: 'l3',
      title: 'Testplan',
      courseId: 'c1',
      courseTitle: 'DevOps',
      dueAt: local(10, 23, 23, 59),
      submitted: 3,
      students: 12,
    },
  ],
  scheduled: [
    {
      linkId: null,
      moduleId: 'm1',
      title: 'Vecka 5',
      courseId: 'c1',
      courseTitle: 'DevOps',
      publishAt: local(10, 23, 8, 0),
      kind: 'Week',
    },
    {
      linkId: 'l4',
      moduleId: null,
      title: 'Ny del',
      courseId: 'c1',
      courseTitle: 'DevOps',
      publishAt: local(10, 24, 8, 0),
      kind: 'Part',
    },
  ],
  courses: [
    {courseId: 'c1', code: 'DEVOPS', title: 'DevOps', students: 12, toGrade: 11, needsPreparation: false},
    {courseId: 'c2', code: 'TEST', title: 'Testning', students: 1, toGrade: 0, needsPreparation: true},
  ],
}

const empty = {toGrade: [], toGradeTotal: 0, deadlines: [], scheduled: [], courses: []}

function backend(teaching: unknown = full, options: {fail?: boolean} = {}) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1', 'c2'], 'Anna Lärare')}
    }
    if (path === '/api/courses') {
      return {status: 200, body: []}
    }
    if (path === '/api/me/teaching') {
      return options.fail
        ? {status: 500, body: 'Server error'}
        : {status: 200, body: teaching}
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

describe('TeacherToday', () => {
  it('visar hälsning, datum och hur många inlämningar som väntar', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const dashboard = wrapper.find('[data-test="teacher-dashboard"]')
    expect(dashboard.find('h1').text()).toBe('God morgon, Anna')
    expect(dashboard.text()).toContain('Onsdag 21 oktober · vecka 43 · 11 att bedöma')
  })

  it('listar inlämningar att bedöma med länk till bedömningen', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const rows = wrapper.findAll('[data-test="to-grade-item"]')
    expect(rows).toHaveLength(2)
    expect(rows[0].text()).toContain('Maja Svensson')
    expect(rows[0].text()).toContain('Release-pipeline')
    expect(rows[0].text()).toContain('DevOps')
    expect(rows[0].text()).toContain('inlämnad för 2 timmar sedan')
    expect(rows[0].attributes('href')).toContain('sub1')
  })

  it('visar deadlines med hur många som lämnat in', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const row = wrapper.find('[data-test="deadline-item"]')
    expect(row.text()).toContain('Testplan')
    expect(row.text()).toContain('3 av 12 inlämnade')
    expect(row.find('[role="progressbar"]').attributes('aria-valuenow')).toBe('25')
  })

  it('visar schemalagda veckor och delar', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const rows = wrapper.findAll('[data-test="scheduled-item"]')
    expect(rows.map((row) => row.text())).toEqual([
      expect.stringContaining('Vecka 5'),
      expect.stringContaining('Ny del'),
    ])
    expect(rows[0].text()).toContain('Vecka')
    expect(rows[1].text()).toContain('Del')
  })

  it('länkar en schemalagd del till delens sida och en vecka till kursen', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const rows = wrapper.findAll('[data-test="scheduled-item"]')
    expect(rows[0].attributes('href')).toBe('/courses/c1/content')
    expect(rows[1].attributes('href')).toBe('/courses/c1/parts/l4')
  })

  it('visar kurserna med antal elever och en markering när något väntar', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const cards = wrapper.findAll('[data-test="teacher-course-card"]')
    expect(cards[0].text()).toContain('12 elever')
    expect(cards[0].text()).toContain('11 att bedöma')
    expect(cards[1].text()).toContain('1 elev')
    expect(cards[1].text()).not.toContain('att bedöma')
  })

  it('pekar ut kurser som behöver förberedas med en länk till kursen', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const prompt = wrapper.find('[data-test="needs-preparation"]')
    expect(prompt.find('h2').text()).toBe('Behöver förberedas')
    const items = prompt.findAll('[data-test="needs-preparation-item"]')
    expect(items).toHaveLength(1)
    expect(items[0].text()).toContain('Testning')
    expect(items[0].find('a').attributes('href')).toBe('/courses/c2/content')
  })

  it('lägger Behöver förberedas före Att bedöma', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const headings = wrapper.findAll('h2').map((heading) => heading.text())
    expect(headings.indexOf('Behöver förberedas')).toBeGreaterThanOrEqual(0)
    expect(headings.indexOf('Behöver förberedas')).toBeLessThan(headings.indexOf('Att bedöma'))
  })

  it('visar ingen uppmaning när alla kurser är förberedda', async () => {
    backend({...full, courses: full.courses.map((course) => ({...course, needsPreparation: false}))})

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="needs-preparation"]').exists()).toBe(false)
  })

  it('Mina kurser döljer avslutade kurser utan inlämningar att bedöma', async () => {
    const finished = {startsOn: '2020-01-01', endsOn: '2020-06-01', students: 3, needsPreparation: true}
    backend({
      ...full,
      courses: [
        {...full.courses[0]},
        {...finished, courseId: 'c3', code: 'OLD', title: 'Gammal', toGrade: 0},
        {...finished, courseId: 'c4', code: 'OLD2', title: 'Gammal med rättning', toGrade: 2},
      ],
    })

    const {wrapper} = await renderApp('/')

    expect(wrapper.findAll('[data-test="teacher-course-card"]').map((card) => card.text()))
      .toEqual([expect.stringContaining('DevOps'), expect.stringContaining('Gammal med rättning')])
    expect(wrapper.find('[data-test="needs-preparation"]').exists()).toBe(false)
  })

  it('visar tomma lägen', async () => {
    backend(empty)

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="to-grade-empty"]').text()).toBe('Inget att bedöma just nu.')
    expect(wrapper.find('[data-test="scheduled-empty"]').text())
      .toBe('Inget schemalagt de närmaste sju dagarna.')
    expect(wrapper.find('[data-test="deadline-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="teacher-courses-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="teacher-dashboard"]').text()).toContain('Inget att bedöma')
  })

  it('visar ett fel när översikten inte kan hämtas', async () => {
    backend(full, {fail: true})

    const {wrapper} = await renderApp('/')

    expect(wrapper.text()).toContain('Kunde inte hämta din översikt.')
  })
})
