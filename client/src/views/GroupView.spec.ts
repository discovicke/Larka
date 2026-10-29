import {describe, expect, it} from 'vitest'

import {mockApi} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const classGroup = {id: 'g1', schoolId: 's1', type: 'Class', name: 'TE22A', term: 'HT25'}
const committee = {id: 'g2', schoolId: 's1', type: 'Committee', name: 'Elevrådet', term: ''}

const devops = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

const members = [
  {userId: 'u1', name: 'Siv Student', role: 'Student'},
  {userId: 'u2', name: 'Lars Lärare', role: 'Teacher'},
  {userId: 'u3', name: 'Malin Mentor', role: 'Mentor'},
  {userId: 'u4', name: 'Stina Student', role: 'Student'},
]

interface Backend {
  group?: typeof classGroup
  courses?: unknown[]
  members?: unknown[]
  membersStatus?: number
}

function backend(options: Backend = {}) {
  const group = options.group ?? classGroup
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [devops]}
    }
    if (path === `/api/groups/${group.id}`) {
      return {status: 200, body: group}
    }
    if (path === `/api/groups/${group.id}/courses`) {
      return {status: 200, body: options.courses ?? [devops]}
    }
    if (path === `/api/groups/${group.id}/members`) {
      return options.membersStatus
        ? {status: options.membersStatus, body: 'fel'}
        : {status: 200, body: options.members ?? members}
    }
    return {status: 404}
  })
}

describe('GroupView', () => {
  it('visar namn, typ och termin i rubriken', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/g1')

    expect(wrapper.find('h1').text()).toBe('TE22A')
    expect(wrapper.find('[data-test="group-subtitle"]').text()).toBe('Klass · HT25')
  })

  it('grupperar medlemmarna efter roll med lärare först', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/g1')

    const sections = wrapper.findAll('[data-test="member-role"]')
    expect(sections.map((section) => section.find('h3').text())).toEqual(['Lärare', 'Mentor', 'Elev'])
    expect(sections[0].findAll('[data-test="member-name"]').map((name) => name.text())).toEqual(['Lars Lärare'])
    expect(sections[2].findAll('[data-test="member-row"]')).toHaveLength(2)
  })

  it('döljer roller utan medlemmar', async () => {
    backend({members: [members[0]]})

    const {wrapper} = await renderApp('/groups/g1')

    const sections = wrapper.findAll('[data-test="member-role"]')
    expect(sections.map((section) => section.find('h3').text())).toEqual(['Elev'])
  })

  it('en kurs länkar till kursens sida', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/g1')

    const link = wrapper.find('[data-test="course-row"] a[href="/courses/c1/content"]')
    expect(link.text()).toBe('DevOps')
  })

  it('döljer kurserna för en grupp utan kurser', async () => {
    backend({group: committee, courses: []})

    const {wrapper} = await renderApp('/groups/g2')

    expect(wrapper.find('h1').text()).toBe('Elevrådet')
    expect(wrapper.find('[data-test="group-subtitle"]').text()).toBe('Kommitté')
    expect(wrapper.find('#courses-heading').exists()).toBe(false)
  })

  it('visar att gruppen saknar medlemmar', async () => {
    backend({members: []})

    const {wrapper} = await renderApp('/groups/g1')

    expect(wrapper.text()).toContain('Gruppen har inga medlemmar ännu.')
  })

  it('visar sidan som saknas när gruppen inte finns', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/okand')

    expect(wrapper.find('[data-test="not-found"]').exists()).toBe(true)
  })

  it('visar ett fel när medlemmarna inte kan hämtas', async () => {
    backend({membersStatus: 500})

    const {wrapper} = await renderApp('/groups/g1')

    expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte hämta gruppen.')
  })

  it('Alla grupper är aktiv i sidomenyn', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/g1')

    expect(wrapper.find('[data-slot="sidebar"] a[href="/groups"]').attributes('data-active')).toBe('true')
  })
})
