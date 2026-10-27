import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const school = {id: 's1', name: 'Nacka gymnasium'}

const classGroup = {id: 'g1', schoolId: 's1', type: 'Class', name: 'TE22A', term: 'HT25'}
const committee = {id: 'g2', schoolId: 's1', type: 'Committee', name: 'Elevrådet', term: ''}

const devops = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const java = {id: 'c2', schoolId: 's1', code: 'JAVA', title: 'Java'}

function backend(groups = [classGroup, committee]) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1', 'c2'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [devops, java]}
    }
    if (path === '/api/schools') {
      return {status: 200, body: [school]}
    }
    if (path === '/api/groups') {
      return {status: 200, body: groups}
    }
    return {status: 404}
  })
}

function rowNames(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  return wrapper.findAll('[data-test="group-row"]').map((row) => row.find('td').text())
}

describe('GroupsView', () => {
  it('visar skolan, grupperna och kurserna med typ, termin och kod', async () => {
    backend()

    const {wrapper} = await renderApp('/groups')

    expect(wrapper.findAll('thead th').map((cell) => cell.text()))
      .toEqual(['Grupp', 'Kod', 'Typ', 'Period'])
    const rows = wrapper.findAll('[data-test="group-row"]')
    expect(rows.map((row) => row.find('td').text()))
      .toEqual(['Nacka gymnasium', 'Elevrådet', 'TE22A', 'DevOps', 'Java'])

    expect(rows[0].text()).toContain('Skola')
    const classRow = rows.find((row) => row.text().includes('TE22A'))
    expect(classRow?.text()).toContain('Klass')
    expect(classRow?.text()).toContain('HT25')
    const courseRow = rows.find((row) => row.text().includes('DEVOPS'))
    expect(courseRow?.text()).toContain('Kurs')
  })

  it('en kurs länkar till kursens innehåll', async () => {
    backend()

    const {wrapper} = await renderApp('/groups')

    const link = wrapper.find('[data-test="group-row"] a[href="/courses/c1/content"]')

    expect(link.exists()).toBe(true)
    expect(link.text()).toBe('DevOps')
  })

  it('sökningen filtrerar raderna', async () => {
    backend()
    const {wrapper} = await renderApp('/groups')

    await wrapper.find('#group-search').setValue('te22a')
    await flushPromises()

    expect(rowNames(wrapper)).toEqual(['TE22A'])
  })

  it('Kurser-fliken visar bara kurserna', async () => {
    backend()
    const {wrapper} = await renderApp('/groups')

    const kurser = wrapper.findAll('[role="tab"]').find((tab) => tab.text() === 'Kurser')
    await kurser?.trigger('click')
    await flushPromises()

    expect(rowNames(wrapper)).toEqual(['DevOps', 'Java'])
  })

  it('visar ett fel när grupperna inte kan hämtas', async () => {
    mockApi((_method, path) => {
      if (path === '/api/courses') {
        return {status: 200, body: [devops]}
      }
      if (path === '/api/schools') {
        return {status: 200, body: [school]}
      }
      if (path === '/api/groups') {
        return {status: 500, body: 'boom'}
      }
      return {status: 404}
    })

    const {wrapper} = await renderApp('/groups')

    expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte hämta grupperna.')
    expect(wrapper.findAll('[data-test="group-row"]')).toHaveLength(0)
  })

  it('sidomenyn har en länk till alla grupper', async () => {
    backend()
    const {wrapper} = await renderApp('/groups')

    const link = wrapper.find('[data-slot="sidebar"] a[href="/groups"]')

    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('Alla grupper')
  })
})
