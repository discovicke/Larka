import {flushPromises} from '@vue/test-utils'
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import {testGroup} from '@/test/fixtures'
import {mockApi, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const overview = {
  classTimelines: [
    {
      groupId: 'g3',
      name: 'JAVA25',
      currentCourses: [{
        courseId: 'c5',
        title: 'Java med databaser',
        code: 'JAVA-DB',
        startsOn: '2026-09-01',
        endsOn: '2026-11-01',
      }],
    },
    {groupId: 'g4', name: 'FIBER25', currentCourses: []},
  ],
  coursesWithoutTeacher: [
    {courseId: 'c1', code: 'MOLN26', title: 'Molnsäkerhet', groupId: 'g1'},
    {courseId: 'c2', code: 'FRI', title: 'Fristående', groupId: null},
  ],
  usersWithoutPassword: [
    {userId: 'u1', name: 'Anna Elev', groupId: 'g1', groupName: 'NET26'},
    {userId: 'u2', name: 'Bo Elev', groupId: 'g1', groupName: 'NET26'},
    {userId: 'u3', name: 'Clara Lös', groupId: null, groupName: null},
  ],
  studentsWithoutClass: [],
}

const classGroup = testGroup({name: 'NET26', memberCount: 2})

const classAnalytics = {
  groupId: 'g1',
  groupName: 'NET26',
  courses: [{
    courseId: 'c1',
    title: 'DevOps',
    code: 'DEVOPS',
    startsOn: '2026-08-01',
    endsOn: '2026-12-01',
    studentCount: 2,
    assignmentCount: 2,
    gradedCount: 2,
    failCount: 1,
    passCount: 1,
    distinctionCount: 0,
    needsWorkCount: 0,
    submittedCount: 0,
    overdueCount: 1,
    autoFailCount: 0,
    atRiskStudentCount: 1,
  }],
  students: [
    {
      studentId: 'u1',
      name: 'Anna Elev',
      needsSupport: true,
      courses: [{
        courseId: 'c1',
        assignmentCount: 2,
        gradedCount: 1,
        failCount: 1,
        passCount: 0,
        distinctionCount: 0,
        needsWorkCount: 0,
        submittedCount: 0,
        overdueCount: 1,
        autoFailCount: 0,
        needsSupport: true,
      }],
    },
    {
      studentId: 'u2',
      name: 'Bo Elev',
      needsSupport: false,
      courses: [{
        courseId: 'c1',
        assignmentCount: 2,
        gradedCount: 1,
        failCount: 0,
        passCount: 1,
        distinctionCount: 0,
        needsWorkCount: 0,
        submittedCount: 0,
        overdueCount: 0,
        autoFailCount: 0,
        needsSupport: false,
      }],
    },
  ],
}

const nothingToDo = {
  classTimelines: [],
  coursesWithoutTeacher: [],
  usersWithoutPassword: [],
  studentsWithoutClass: [],
}

function backend(options: {me?: ReturnType<typeof meAs>, overview?: unknown, groups?: unknown[], analytics?: unknown, fail?: boolean} = {}) {
  mockApi((method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: options.me ?? meAs('Admin', [], 'Ada Admin')}
    }
    if (method === 'POST' && path === '/api/users/u1/password') {
      return {status: 200, body: {id: 'u1', schoolId: 's1', name: 'Anna Elev', email: 'anna@example.com', role: 'Student'}}
    }
    if (path === '/api/courses') {
      return {status: 200, body: []}
    }
    if (path === '/api/groups') {
      return {status: 200, body: options.groups ?? []}
    }
    if (path === '/api/groups/g1/analytics') {
      return options.analytics
        ? {status: 200, body: options.analytics}
        : {status: 404}
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
    expect(dashboard.text()).toContain('Onsdag 21 oktober, vecka 43')
    // Två kurser utan lärare och två rader utan lösenord: klassen NET26 och användaren utan klass.
    expect(dashboard.text()).toContain('4 saker att ordna')
    expect(wrapper.find('[data-test="teacher-dashboard"]').exists()).toBe(false)
  })

  it('visar klasstidslinjer med pågående kurs', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="class-timelines"]').text()).toContain('JAVA25')
    expect(wrapper.find('[data-test="class-timelines"]').text()).toContain('Java med databaser')
    expect(wrapper.find('[data-test="class-timelines"]').text()).toContain('FIBER25')
    expect(wrapper.find('[data-test="class-timelines"]').text()).toContain('Ingen pågående kurs')
    expect(wrapper.find('[data-test="timeline-warning"]').text()).toContain('dagar kvar')
    expect(wrapper.find('[data-test="admin-stat"]').exists()).toBe(false)
  })

  it('visar snabbåtgärder och öppnar klassformuläret', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const actions = wrapper.find('[data-test="admin-quick-actions"]')
    expect(actions.text()).toContain('Ny klass')
    expect(actions.text()).toContain('Ny grupp')
    expect(actions.text()).toContain('Skapa kurs')
    expect(actions.text()).toContain('Användare')
    expect(actions.text()).toContain('Tilldela lärare')

    await wrapper.find('[data-test="quick-new-class"]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Ny klass')
    expect(document.body.textContent).toContain('En klass är en årskull')
  })

  it('visar klassöversikt med betyg och riskfilter', async () => {
    backend({groups: [classGroup], analytics: classAnalytics})

    const {wrapper} = await renderApp('/')
    await flushPromises()

    expect(wrapper.find('[data-test="class-analytics"]').text()).toContain('Följ upp klass')
    expect(wrapper.find('[data-test="analytics-stat-pass"]').text()).toContain('50 %')
    expect(wrapper.find('[data-test="analytics-stat-risk"]').text()).toContain('1')
    expect(wrapper.find('[data-test="analytics-stat-missing"]').text()).toContain('1')
    expect(wrapper.find('[data-test="analytics-students"]').text()).toContain('Anna Elev')
    expect(wrapper.find('[data-test="analytics-students"]').text()).toContain('Behöver stöttning')

    await wrapper.find('[data-test="risk-filter"]').trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-test="analytics-student"]')).toHaveLength(1)
    expect(wrapper.find('[data-test="analytics-students"]').text()).toContain('Anna Elev')
    expect(wrapper.find('[data-test="analytics-students"]').text()).not.toContain('Bo Elev')
  })

  it('samlar elever utan klass i en rad som länkar till användarna', async () => {
    backend({overview: {...nothingToDo, studentsWithoutClass: [
      {userId: 'u7', name: 'Ny Elev'},
      {userId: 'u8', name: 'Annan Ny'},
    ]}})

    const {wrapper} = await renderApp('/')

    const section = wrapper.find('[data-test="no-class"]')
    expect(section.findAll('[data-test="no-class-item"]').map((item) => item.text())).toEqual(['2 elever'])
    expect(section.find('[data-test="no-class-item"] a').attributes('href')).toBe('/users?filter=Student')
    expect(wrapper.text()).toContain('1 sak att ordna')
    expect(wrapper.find('[data-test="admin-all-good"]').exists()).toBe(false)
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

  it('visar inte mentorlistan', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="no-mentor"]').exists()).toBe(false)
  })

  // #508: användare utan lösenord syns igen, med Sätt lösenord direkt på raden.
  it('listar dem som inte kan logga in och sätter ett lösenord från raden', async () => {
    backend()

    const {wrapper} = await renderApp('/')

    const section = wrapper.find('[data-test="no-password"]')
    expect(section.find('h2').text()).toBe('Kan inte logga in')
    expect(section.findAll('[data-test="no-password-item"]').map((item) => item.text().replace(/\s+/g, ' ')))
      .toEqual([
        'Anna ElevNET26 Sätt lösenord',
        'Bo ElevNET26 Sätt lösenord',
        'Clara LösUtan klass Sätt lösenord',
      ])
    expect(section.find('a').attributes('href')).toBe('/groups/g1')

    await section.find('[data-test="set-password-u1"]').trigger('click')
    await flushPromises()
    const dialog = document.querySelector('[role="dialog"]') as HTMLElement
    expect(dialog.textContent).toContain('Sätt lösenord för Anna Elev')
    const input = dialog.querySelector('[data-test="set-password"]') as HTMLInputElement
    input.value = 'hemligt123'
    input.dispatchEvent(new Event('input'))
    await flushPromises()
    ;(dialog.querySelector('[data-test="save-password"]') as HTMLElement).click()
    await flushPromises()

    expect(sent('POST', '/api/users/u1/password')).toEqual({password: 'hemligt123'})
    expect(wrapper.find('[data-test="toast"]').text()).toContain('Lösenordet är sparat. Ge det till Anna Elev.')
  })

  it('säger det när inget behöver ordnas', async () => {
    backend({overview: nothingToDo})

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="no-teacher"]').exists()).toBe(false)
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
