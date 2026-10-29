import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

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
      .toEqual(['Grupp', 'Kod', 'Typ', 'Status', 'Period'])
    const rows = wrapper.findAll('[data-test="group-row"]')
    expect(rows.map((row) => row.find('td').text()))
      .toEqual(['Edugrade', 'Elevrådet', 'TE22A', 'DevOps', 'Java'])

    expect(rows[0].text()).toContain('Skola')
    const classRow = rows.find((row) => row.text().includes('TE22A'))
    expect(classRow?.text()).toContain('Klass')
    expect(classRow?.text()).toContain('Startade HT25')
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

  it('en grupp länkar till gruppens sida men skolan är ingen länk', async () => {
    backend()

    const {wrapper} = await renderApp('/groups')

    const link = wrapper.find('[data-test="group-row"] a[href="/groups/g1"]')
    expect(link.text()).toBe('TE22A')
    const schoolRow = wrapper.findAll('[data-test="group-row"]')[0]
    expect(schoolRow.find('a').exists()).toBe(false)
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

  it('Grupper-fliken visar skolan och grupperna men inte kurserna', async () => {
    backend()
    const {wrapper} = await renderApp('/groups')

    const grupper = wrapper.findAll('[role="tab"]').find((tab) => tab.text() === 'Grupper')
    await grupper?.trigger('click')
    await flushPromises()

    expect(rowNames(wrapper)).toEqual(['Edugrade', 'Elevrådet', 'TE22A'])
  })

  it('Kurser-fliken skrivs till adressen och öppnas direkt med ?filter=courses', async () => {
    backend()
    const {wrapper, router} = await renderApp('/groups')

    const kurser = wrapper.findAll('[role="tab"]').find((tab) => tab.text() === 'Kurser')
    await kurser?.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query.filter).toBe('courses')

    const alla = wrapper.findAll('[role="tab"]').find((tab) => tab.text() === 'Alla')
    await alla?.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query.filter).toBeUndefined()

    const direct = await renderApp('/groups?filter=courses')
    expect(rowNames(direct.wrapper)).toEqual(['DevOps', 'Java'])
    expect(direct.wrapper.find('[role="tab"][aria-selected="true"]').text()).toBe('Kurser')
  })

  it('kurser visar datum och märke, pågående först och avslutade sist', async () => {
    const day = (offset: number) => {
      const date = new Date()
      date.setDate(date.getDate() + offset)
      return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
    }
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1', 'c2', 'c3'])}
      }
      if (path === '/api/courses') {
        return {
          status: 200,
          body: [
            {...devops, title: 'A klar', startsOn: day(-60), endsOn: day(-30)},
            {...devops, id: 'c2', title: 'B kommande', startsOn: day(30), endsOn: null},
            {...devops, id: 'c3', title: 'C pågår', startsOn: day(-5), endsOn: day(5)},
          ],
        }
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/groups?filter=courses')

    expect(rowNames(wrapper)).toEqual(['C pågår', 'B kommande', 'A klar'])
    const badges = wrapper.findAll('[data-test="course-status"]').map((badge) => badge.text())
    expect(badges).toEqual(['Pågår', 'Kommande', 'Avslutad'])
    expect(wrapper.findAll('[data-test="group-row"]')[1].text()).toContain('från ')
    expect(wrapper.text()).toContain('Avslutade kurser finns kvar här')
  })

  it('visar ett fel när grupperna inte kan hämtas', async () => {
    mockApi((_method, path) => {
      if (path === '/api/courses') {
        return {status: 200, body: [devops]}
      }
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
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

// Admin skapar, ändrar och tar bort direkt i listan.
const net25 = {id: 'g1', schoolId: 's1', type: 'Class', name: 'NET25', term: 'HT25', memberCount: 24}
const full = {description: '', points: null, startsOn: null, endsOn: null}

interface AdminOptions {
  groups?: unknown[]
  courses?: unknown[]
  groupsStatus?: number
  groupDeleteStatus?: number
  courseDeleteStatus?: number
  role?: 'Admin' | 'Teacher'
}

function adminBackend({
  groups = [net25, committee],
  courses = [{...devops, ...full}, {...java, ...full}],
  groupsStatus = 200,
  groupDeleteStatus = 204,
  courseDeleteStatus = 204,
  role = 'Admin',
}: AdminOptions = {}) {
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs(role)}
    }
    if (method === 'GET' && path === '/api/courses') {
      return {status: 200, body: courses}
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
      return {status: groupDeleteStatus, body: groupDeleteStatus === 409
        ? 'the group has published content'
        : undefined}
    }
    if (method === 'POST' && path === '/api/courses') {
      return {status: 201, body: {id: 'c9', ...full, ...(body as object)}}
    }
    if (method === 'PUT' && path === '/api/courses/c1') {
      return {status: 200, body: {...devops, ...full, ...(body as object)}}
    }
    if (method === 'DELETE' && path === '/api/courses/c1') {
      return {status: courseDeleteStatus, body: courseDeleteStatus === 409
        ? 'the course has student work'
        : undefined}
    }
    return {status: 404}
  })
}

function dialog(): HTMLElement {
  return document.querySelector('[role="dialog"]') as HTMLElement
}

async function type(selector: string, value: string) {
  const element = document.querySelector(selector) as HTMLInputElement
  element.value = value
  element.dispatchEvent(new Event('input'))
  await flushPromises()
}

async function click(element: Element | null) {
  ;(element as HTMLElement).click()
  await flushPromises()
}

async function openMenu(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], label: string) {
  await wrapper.find(`[aria-label="${label}"]`).trigger('keydown', {key: 'Enter'})
  await flushPromises()
}

async function openNewMenu(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  await wrapper.find('[data-test="new-menu"]').trigger('keydown', {key: 'Enter'})
  await flushPromises()
}

async function choose(label: string) {
  const item = [...document.querySelectorAll('[role="menuitem"]')]
    .find((candidate) => candidate.textContent?.trim() === label)
  await click(item ?? null)
}

describe('GroupsView som admin', () => {
  it('visar Ny och en åtgärdsmeny för varje grupp och kurs, men inte för skolan', async () => {
    adminBackend()
    const {wrapper} = await renderApp('/groups')

    expect(wrapper.find('[data-test="new-menu"]').text()).toBe('Ny')
    expect(wrapper.findAll('thead th').map((cell) => cell.text()))
      .toEqual(['Grupp', 'Kod', 'Typ', 'Status', 'Period', 'Åtgärder'])
    expect(wrapper.find('[aria-label="Åtgärder för NET25"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Åtgärder för DevOps"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Åtgärder för Edugrade"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-test="row-actions"]')).toHaveLength(4)
  })

  it('en lärare ser sidan som förut, utan Ny och åtgärder', async () => {
    adminBackend({role: 'Teacher'})
    const {wrapper} = await renderApp('/groups')

    expect(wrapper.find('[data-test="new-menu"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="row-actions"]').exists()).toBe(false)
    expect(wrapper.findAll('thead th')).toHaveLength(5)
  })

  it('Ny klass eller grupp skapar en grupp som hamnar i listan utan omladdning', async () => {
    adminBackend()
    const {wrapper} = await renderApp('/groups')

    await openNewMenu(wrapper)
    await choose('Ny klass eller grupp')
    await type('[data-test="group-name"]', 'Alfa')
    await click(dialog().querySelector('[data-test="save-group"]'))

    expect(sent('POST', '/api/groups')).toMatchObject({schoolId: 's1', name: 'Alfa'})
    expect(rowNames(wrapper)).toEqual(['Edugrade', 'Alfa', 'Elevrådet', 'NET25', 'DevOps', 'Java'])
  })

  it('Ny kurs skapar en kurs som hamnar i listan', async () => {
    adminBackend()
    const {wrapper} = await renderApp('/groups')

    await openNewMenu(wrapper)
    await choose('Ny kurs')
    await type('[data-test="course-title"]', 'Python')
    await type('[data-test="course-code"]', 'PY')
    await click(dialog().querySelector('[data-test="save-course"]'))

    expect(sent('POST', '/api/courses')).toMatchObject({schoolId: 's1', title: 'Python', code: 'PY'})
    expect(rowNames(wrapper)).toEqual(['Edugrade', 'Elevrådet', 'NET25', 'DevOps', 'Java', 'Python'])
  })

  it('Redigera ändrar en grupp i listan', async () => {
    adminBackend()
    const {wrapper} = await renderApp('/groups')

    await openMenu(wrapper, 'Åtgärder för NET25')
    await choose('Redigera')
    await type('[data-test="group-name"]', 'NET25B')
    await click(dialog().querySelector('[data-test="save-group"]'))

    expect(sent('PUT', '/api/groups/g1')).toEqual({type: 'Class', name: 'NET25B', term: 'HT25'})
    expect(rowNames(wrapper)).toEqual(['Edugrade', 'Elevrådet', 'NET25B', 'DevOps', 'Java'])
  })

  it('Redigera ändrar en kurs i listan', async () => {
    adminBackend()
    const {wrapper} = await renderApp('/groups')

    await openMenu(wrapper, 'Åtgärder för DevOps')
    await choose('Redigera')
    await type('[data-test="course-title"]', 'DevOps 2')
    await click(dialog().querySelector('[data-test="save-course"]'))

    expect(sent('PUT', '/api/courses/c1')).toMatchObject({title: 'DevOps 2'})
    expect(rowNames(wrapper)).toEqual(['Edugrade', 'Elevrådet', 'NET25', 'DevOps 2', 'Java'])
  })

  it('Ta bort frågar med gruppens namn och tar bort den ur listan', async () => {
    adminBackend()
    const {wrapper} = await renderApp('/groups')

    await openMenu(wrapper, 'Åtgärder för NET25')
    await choose('Ta bort')
    expect(dialog().textContent).toContain('Ta bort NET25?')
    await click(dialog().querySelector('[data-test="confirm-delete-group"]'))

    expect(rowNames(wrapper)).toEqual(['Edugrade', 'Elevrådet', 'DevOps', 'Java'])
  })

  it('en grupp med innehåll blir kvar och felet visas', async () => {
    adminBackend({groupDeleteStatus: 409})
    const {wrapper} = await renderApp('/groups')

    await openMenu(wrapper, 'Åtgärder för NET25')
    await choose('Ta bort')
    await click(dialog().querySelector('[data-test="confirm-delete-group"]'))

    expect(dialog().textContent).toContain('Gruppen har publicerat innehåll och kan inte tas bort.')
    expect(rowNames(wrapper)).toContain('NET25')
  })

  it('Ta bort frågar med kursens namn och tar bort den ur listan', async () => {
    adminBackend()
    const {wrapper} = await renderApp('/groups')

    await openMenu(wrapper, 'Åtgärder för DevOps')
    await choose('Ta bort')
    expect(dialog().textContent).toContain('Ta bort DevOps?')
    await click(dialog().querySelector('[data-test="confirm-delete-course"]'))

    expect(requests.some((request) => request.method === 'DELETE' && request.path === '/api/courses/c1')).toBe(true)
    expect(rowNames(wrapper)).toEqual(['Edugrade', 'Elevrådet', 'NET25', 'Java'])
  })

  it('en kurs med elevarbete blir kvar och 409 visas som ett tydligt meddelande', async () => {
    adminBackend({courseDeleteStatus: 409})
    const {wrapper} = await renderApp('/groups')

    await openMenu(wrapper, 'Åtgärder för DevOps')
    await choose('Ta bort')
    await click(dialog().querySelector('[data-test="confirm-delete-course"]'))

    expect(dialog().textContent).toContain('Kursen har elevarbete och kan inte tas bort.')
    expect(rowNames(wrapper)).toContain('DevOps')
  })

  it('utan grupper finns skapa-knappen i tomläget', async () => {
    adminBackend({groups: []})
    const {wrapper} = await renderApp('/groups')

    expect(wrapper.find('[data-test="groups-empty"]').exists()).toBe(true)
    await click(wrapper.find('[data-test="empty-new-group"]').element)
    expect(dialog().textContent).toContain('Ny grupp')
    await type('[data-test="group-name"]', 'NET26')
    await click(dialog().querySelector('[data-test="save-group"]'))

    expect(wrapper.find('[data-test="groups-empty"]').exists()).toBe(false)
    expect(rowNames(wrapper)).toContain('NET26')
  })

  it('utan kurser har fliken Kurser en knapp för att skapa den första', async () => {
    adminBackend({courses: []})
    const {wrapper} = await renderApp('/groups')

    const kurser = wrapper.findAll('[role="tab"]').find((tab) => tab.text() === 'Kurser')
    await kurser?.trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="empty-new-course"]').text()).toContain('Skapa skolans första kurs')
    expect(wrapper.find('table').exists()).toBe(false)
  })

  it('lärare ser inget tomläge med skapa-knapp', async () => {
    adminBackend({groups: [], role: 'Teacher'})
    const {wrapper} = await renderApp('/groups')

    expect(wrapper.find('[data-test="groups-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="empty-new-group"]').exists()).toBe(false)
  })
})
