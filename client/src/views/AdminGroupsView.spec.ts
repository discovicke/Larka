import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const net25 = {id: 'g1', schoolId: 's1', type: 'Class', name: 'NET25', term: 'HT25', memberCount: 24}
const council = {id: 'g2', schoolId: 's1', type: 'Committee', name: 'Elevrådet', term: '', memberCount: 5}

interface Options {
  groups?: unknown[]
  groupsStatus?: number
  deleteStatus?: number
  role?: 'Admin' | 'Teacher'
}

function backend({groups = [net25, council], groupsStatus = 200, deleteStatus = 204, role = 'Admin'}: Options = {}) {
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs(role)}
    }
    if (method === 'GET' && path === '/api/groups') {
      return {status: groupsStatus, body: groupsStatus === 200
        ? groups
        : 'fel'}
    }
    if (method === 'POST' && path === '/api/groups') {
      return {status: 201, body: {id: 'g9', ...(body as object), memberCount: 0}}
    }
    if (method === 'PUT' && path === '/api/groups/g1') {
      return {status: 200, body: {...net25, ...(body as object)}}
    }
    if (method === 'DELETE' && path === '/api/groups/g1') {
      return {status: deleteStatus, body: deleteStatus === 409
        ? 'the group has published content'
        : undefined}
    }
    return {status: 404}
  })
}

async function page(options?: Options) {
  backend(options)
  return renderApp('/settings/groups')
}

function names(wrapper: Awaited<ReturnType<typeof page>>['wrapper']) {
  return wrapper.findAll('[data-test="group-row"]').map((row) => row.find('td').text())
}

function dialog(): HTMLElement {
  return document.querySelector('[role="dialog"]') as HTMLElement
}

async function type(selector: string, value: string, event = 'input') {
  const element = document.querySelector(selector) as HTMLInputElement
  element.value = value
  element.dispatchEvent(new Event(event))
  await flushPromises()
}

async function click(element: Element | null) {
  ;(element as HTMLElement).click()
  await flushPromises()
}

describe('AdminGroupsView', () => {
  it('visar grupperna sorterade med typ, period och antal medlemmar', async () => {
    const {wrapper} = await page()

    expect(wrapper.find('h1').text()).toBe('Program och klasser')
    expect(wrapper.findAll('thead th').map((cell) => cell.text()))
      .toEqual(['Grupp', 'Typ', 'Period', 'Medlemmar', 'Åtgärder'])
    expect(names(wrapper)).toEqual(['Elevrådet', 'NET25'])
    const row = wrapper.findAll('[data-test="group-row"]')[1]
    expect(row.text()).toContain('Klass')
    expect(row.text()).toContain('HT25')
    expect(row.find('[data-test="member-count"]').text()).toBe('24')
    expect(row.find('a').attributes('href')).toBe('/groups/g1')
    expect(wrapper.findAll('[data-test="group-row"]')[0].text()).toContain('–')
  })

  it('söker på namn och period', async () => {
    const {wrapper} = await page()
    const search = wrapper.find('#admin-group-search')

    await search.setValue('ht25')
    expect(names(wrapper)).toEqual(['NET25'])

    await search.setValue('elev')
    expect(names(wrapper)).toEqual(['Elevrådet'])

    await search.setValue('finns inte')
    expect(names(wrapper)).toEqual([])
    expect(wrapper.text()).toContain('Inga grupper matchar sökningen.')
  })

  it('visar tomt läge när skolan saknar grupper', async () => {
    const {wrapper} = await page({groups: []})

    expect(wrapper.find('[data-test="groups-empty"]').exists()).toBe(true)
    expect(wrapper.find('table').exists()).toBe(false)
  })

  it('visar felet när grupperna inte går att hämta', async () => {
    const {wrapper} = await page({groupsStatus: 500})

    expect(wrapper.text()).toContain('Kunde inte hämta grupperna.')
  })

  it('en grupp som skapas hamnar i listan utan omladdning', async () => {
    const {wrapper} = await page()

    await click(wrapper.find('[data-test="new-group"]').element)
    await type('[data-test="group-name"]', 'Alfa')
    await click(dialog().querySelector('[data-test="save-group"]'))

    expect(sent('POST', '/api/groups')).toMatchObject({schoolId: 's1', name: 'Alfa'})
    expect(names(wrapper)).toEqual(['Alfa', 'Elevrådet', 'NET25'])
  })

  it('en ändrad grupp uppdateras i listan', async () => {
    const {wrapper} = await page()

    await click(wrapper.find('[aria-label="Redigera NET25"]').element)
    await type('[data-test="group-name"]', 'NET25B')
    await click(dialog().querySelector('[data-test="save-group"]'))

    expect(sent('PUT', '/api/groups/g1')).toEqual({type: 'Class', name: 'NET25B', term: 'HT25'})
    expect(names(wrapper)).toEqual(['Elevrådet', 'NET25B'])
  })

  it('en borttagen grupp försvinner ur listan', async () => {
    const {wrapper} = await page()

    await click(wrapper.find('[aria-label="Ta bort NET25"]').element)
    expect(dialog().textContent).toContain('Ta bort NET25?')
    await click(dialog().querySelector('[data-test="confirm-delete-group"]'))

    expect(names(wrapper)).toEqual(['Elevrådet'])
  })

  it('en grupp med innehåll blir kvar och felet visas', async () => {
    const {wrapper} = await page({deleteStatus: 409})

    await click(wrapper.find('[aria-label="Ta bort NET25"]').element)
    await click(dialog().querySelector('[data-test="confirm-delete-group"]'))

    expect(dialog().textContent).toContain('Gruppen har publicerat innehåll och kan inte tas bort.')
    expect(names(wrapper)).toEqual(['Elevrådet', 'NET25'])
  })

  it('en lärare skickas till startsidan', async () => {
    const {router} = await page({role: 'Teacher'})

    expect(router.currentRoute.value.name).toBe('today')
  })
})
