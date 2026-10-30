import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import {mockApi} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const overview = {
  courses: {upcoming: 2, running: 4, finished: 7, undated: 1},
  coursesWithoutTeacher: [
    {courseId: 'c1', code: 'MOLN26', title: 'Molnsäkerhet', groupId: 'g1'},
    {courseId: 'c2', code: 'FRI', title: 'Fristående', groupId: null},
  ],
  classesWithoutMentor: [{groupId: 'g2', name: 'FIB26', term: 'VT26'}],
  usersWithoutPassword: [
    {userId: 'u1', name: 'Anna Elev', groupId: 'g1', groupName: 'NET26'},
    {userId: 'u2', name: 'Bo Elev', groupId: 'g1', groupName: 'NET26'},
    {userId: 'u3', name: 'Clara Lös', groupId: null, groupName: null},
  ],
}

const nothingToDo = {
  courses: {upcoming: 0, running: 1, finished: 0, undated: 0},
  coursesWithoutTeacher: [],
  classesWithoutMentor: [],
  usersWithoutPassword: [],
}

function backend(options: {me?: ReturnType<typeof meAs>, overview?: unknown, fail?: boolean} = {}) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: options.me ?? meAs('Admin', [], 'Ada Admin')}
    }
    if (path === '/api/courses' || path === '/api/groups') {
      return {status: 200, body: []}
    }
    if (path === '/api/me/admin-overview') {
      return options.fail
        ? {status: 500, body: 'Server error'}
        : {status: 200, body: options.overview ?? overview}
    }
    if (path === '/api/me/teaching') {
      return {status: 200, body: {toGrade: [], toGradeTotal: 0, deadlines: [], scheduled: [], courses: []}}
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

describe('AdminToday', () => {
  it('admins Idag är en egen vy med hälsning och hur mycket som ska ordnas', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const dashboard = wrapper.find('[data-test="admin-dashboard"]')
    expect(dashboard.find('h1').text()).toBe('God morgon, Ada')
    expect(dashboard.text()).toContain('Onsdag 21 oktober · vecka 43 · 6 saker att ordna')
    expect(wrapper.find('[data-test="teacher-dashboard"]').exists()).toBe(false)
  })

  it('visar kurserna per läge', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="course-counts"]').text())
      .toBe('4 pågår · 2 kommande · 7 avslutade · 1 utan datum')
  })

  it('kurser utan lärare länkar till klassen där läraren läggs till, eller till kursen', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const items = wrapper.findAll('[data-test="no-teacher-item"]')
    expect(items.map((item) => item.text())).toEqual([
      expect.stringContaining('Molnsäkerhet'),
      expect.stringContaining('Fristående'),
    ])
    expect(items[0].find('a').attributes('href')).toBe('/groups/g1')
    expect(items[1].find('a').attributes('href')).toBe('/courses/c2/content')
  })

  it('klasser utan mentor länkar till klassen', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const item = wrapper.find('[data-test="no-mentor-item"]')
    expect(item.text()).toContain('FIB26')
    expect(item.find('a').attributes('href')).toBe('/groups/g2')
  })

  it('användare utan lösenord samlas per klass', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const items = wrapper.findAll('[data-test="no-password-item"]')
    expect(items).toHaveLength(2)
    expect(items[0].text()).toContain('NET26')
    expect(items[0].text()).toContain('2 användare')
    expect(items[0].find('a').attributes('href')).toBe('/groups/g1')
    expect(items[1].text()).toContain('Utan klass')
    expect(items[1].find('a').exists()).toBe(false)
  })

  it('säger det när inget behöver ordnas', async () => {
    backend({overview: nothingToDo})

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="no-teacher"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="no-mentor"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="no-password"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="admin-all-good"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="admin-dashboard"]').text()).toContain('Inget behöver ordnas')
  })

  it('visar inga att bedöma-delar för en admin som inte undervisar', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    expect(wrapper.text()).not.toContain('Att bedöma')
    expect(wrapper.text()).not.toContain('Behöver förberedas')
  })

  it('en admin som också undervisar ser lärarens delar under översikten', async () => {
    backend({me: meAs('Admin', ['c1'], 'Ada Admin')})

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="admin-dashboard"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="teacher-dashboard"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="teacher-dashboard"] h1').exists()).toBe(false)
  })

  it('visar ett fel när översikten inte kan hämtas', async () => {
    backend({fail: true})

    const {wrapper} = await renderApp('/')

    expect(wrapper.text()).toContain('Kunde inte hämta din översikt.')
  })
})
