import {flushPromises} from '@vue/test-utils'
import {afterEach, describe, expect, it, vi} from 'vitest'

import {formatDate} from '@/lib/content'
import {mockApi} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

afterEach(() => {
  vi.useRealTimers()
})

const devops = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const databases = {id: 'c2', schoolId: 's1', code: 'DB', title: 'Databaser'}
const group = {id: 'g1', schoolId: 's1', type: 'Class', name: 'Lag 1', term: 'HT26'}

function part(
  linkId: string,
  contentId: string,
  title: string,
  publishAt: string,
  extra: Record<string, unknown> = {},
) {
  return {
    linkId,
    contentId,
    type: 'Theory',
    title,
    bodyMd: '',
    authorId: 'u1',
    authorName: 'Anna Lärare',
    groupId: null,
    courseId: null,
    moduleId: null,
    publishAt,
    dueAt: null,
    isPinned: false,
    ...extra,
  }
}

function attachment(id: string, contentId: string, fileName: string, sizeBytes: number) {
  return {id, contentId, fileName, url: `https://files.example/${id}`, sizeBytes}
}

interface Feeds {
  courses?: Record<string, unknown[]>
  groups?: Record<string, unknown[]>
  attachments?: Record<string, unknown[]>
  fail?: string
}

function backend(feeds: Feeds = {}) {
  mockApi((method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1', 'c2'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [devops, databases]}
    }
    if (path === '/api/groups') {
      return {status: 200, body: [group]}
    }
    const attachmentPath = path.match(/^\/api\/content\/([^/]+)\/attachments$/)
    if (attachmentPath) {
      if (feeds.fail === attachmentPath[1]) {
        return {status: 500, body: 'Server error'}
      }
      return {status: 200, body: feeds.attachments?.[attachmentPath[1]] ?? []}
    }
    const courseFeed = path.match(/^\/api\/courses\/([^/]+)\/content$/)
    if (courseFeed) {
      if (feeds.fail === courseFeed[1]) {
        return {status: 500, body: 'Server error'}
      }
      return {status: 200, body: feeds.courses?.[courseFeed[1]] ?? []}
    }
    const groupFeed = path.match(/^\/api\/groups\/([^/]+)\/content$/)
    if (groupFeed) {
      if (feeds.fail === groupFeed[1]) {
        return {status: 500, body: 'Server error'}
      }
      return {status: 200, body: feeds.groups?.[groupFeed[1]] ?? []}
    }
    if (method === 'DELETE') {
      return {status: 204}
    }
    return {status: 404}
  })
}

function twoContents() {
  return {
    courses: {
      c1: [part('l1', 'x1', 'Release-pipeline', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      c2: [part('l2', 'x2', 'Normalisering', '2026-10-01T10:00:00Z', {courseId: 'c2'})],
    },
    // Gruppens flöde tar med kursens länk och en egen länk till samma innehåll.
    groups: {
      g1: [
        part('l1', 'x1', 'Release-pipeline', '2026-10-19T10:00:00Z', {courseId: 'c1'}),
        part('l3', 'x1', 'Release-pipeline', '2026-10-19T10:00:00Z', {groupId: 'g1'}),
      ],
    },
    attachments: {
      x1: [attachment('a1', 'x1', 'rapport.pdf', 2048)],
      x2: [attachment('a2', 'x2', 'tabeller.xlsx', 1_500_000)],
    },
  }
}

function rows(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  return wrapper.findAll('[data-test="material-row"]')
}

function rowCells(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], index: number) {
  return rows(wrapper)[index].findAll('td').map((cell) => cell.text())
}

function headings(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  return wrapper.findAll('[data-test="material-section"]').map((cell) => cell.text())
}

describe('MaterialView', () => {
  it('visar filerna med del, grupp, uppladdare och datum', async () => {
    backend(twoContents())

    const {wrapper} = await renderApp('/material')

    expect(wrapper.findAll('thead th').map((cell) => cell.text()))
      .toEqual(['Namn', 'Del', 'Grupp', 'Uppladdat av', 'Datum'])

    const cells = rowCells(wrapper, 0)
    expect(cells[0]).toContain('rapport.pdf')
    expect(cells[0]).toContain('2,0 kB')
    expect(cells[1]).toBe('Release-pipeline')
    expect(cells[2]).toBe('DevOps · Lag 1')
    expect(cells[3]).toBe('Anna Lärare')
    expect(cells[4]).toBe(formatDate('2026-10-19T10:00:00Z'))

    expect(rowCells(wrapper, 1)[0]).toContain('tabeller.xlsx')
  })

  it('sorterar nyast först', async () => {
    backend(twoContents())

    const {wrapper} = await renderApp('/material')

    expect(rows(wrapper).map((row) => row.find('a').text()))
      .toEqual([expect.stringContaining('rapport.pdf'), expect.stringContaining('tabeller.xlsx')])
  })

  it('delar in filerna i nytt den här veckan och tidigare', async () => {
    vi.useFakeTimers({toFake: ['Date']})
    vi.setSystemTime(new Date('2026-10-20T12:00:00'))
    backend(twoContents())

    const {wrapper} = await renderApp('/material')

    expect(headings(wrapper)).toEqual(['Nytt den här veckan', 'Tidigare'])
    expect(rows(wrapper).map((row) => row.find('a').text()))
      .toEqual([expect.stringContaining('rapport.pdf'), expect.stringContaining('tabeller.xlsx')])
    expect(rows(wrapper)[0].find('span.font-medium').text()).toBe('rapport.pdf')
  })

  it('söker på filnamn och deltitel', async () => {
    backend(twoContents())
    const {wrapper} = await renderApp('/material')

    await wrapper.find('#material-search').setValue('tabell')
    await flushPromises()
    expect(rows(wrapper).map((row) => row.find('a').text()))
      .toEqual([expect.stringContaining('tabeller.xlsx')])

    await wrapper.find('#material-search').setValue('release')
    await flushPromises()
    expect(rows(wrapper).map((row) => row.find('a').text()))
      .toEqual([expect.stringContaining('rapport.pdf')])
  })

  it('visar Inga filer matchar när sökningen inte träffar', async () => {
    backend(twoContents())
    const {wrapper} = await renderApp('/material')

    await wrapper.find('#material-search').setValue('finns inte')
    await flushPromises()

    expect(rows(wrapper)).toHaveLength(0)
    expect(wrapper.text()).toContain('Inga filer matchar.')
  })

  it('filtrerar på mål med chipsen', async () => {
    backend(twoContents())
    const {wrapper} = await renderApp('/material')

    const chips = wrapper.findAll('button[aria-pressed]')
    expect(chips.map((chip) => chip.text())).toEqual(['Allt', 'DevOps', 'Databaser', 'Lag 1'])
    expect(chips[0].attributes('aria-pressed')).toBe('true')

    const databasesChip = chips.find((chip) => chip.text() === 'Databaser')
    await databasesChip?.trigger('click')
    await flushPromises()

    expect(rows(wrapper).map((row) => row.find('a').text()))
      .toEqual([expect.stringContaining('tabeller.xlsx')])
    expect(databasesChip?.attributes('aria-pressed')).toBe('true')
  })

  it('fillänken öppnar bilagans url i en ny flik', async () => {
    backend(twoContents())
    const {wrapper} = await renderApp('/material')

    const link = rows(wrapper)[0].find('a')
    expect(link.attributes('href')).toBe('https://files.example/a1')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toBe('noopener')
  })

  it('visar tomvyn när inga filer finns', async () => {
    backend()

    const {wrapper} = await renderApp('/material')

    expect(wrapper.text()).toContain('Inget material än')
    expect(wrapper.text()).toContain('Filer som lärarna bifogar i kursernas delar visas här.')
    expect(rows(wrapper)).toHaveLength(0)
  })

  it('visar ett fel när flödena inte kan hämtas', async () => {
    backend({...twoContents(), fail: 'c1'})

    const {wrapper} = await renderApp('/material')

    expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte hämta materialet.')
    expect(rows(wrapper)).toHaveLength(0)
  })

  it('sidomenyn har en länk till Material', async () => {
    backend()
    const {wrapper, router} = await renderApp('/')

    const link = wrapper.find('[data-slot="sidebar"] a[href="/material"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('Material')

    await link.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('material')
    expect(router.currentRoute.value.path).toBe('/material')
  })
})
