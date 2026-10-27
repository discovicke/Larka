import {flushPromises} from '@vue/test-utils'
import {afterEach, describe, expect, it, vi} from 'vitest'

import {mockApi, requests} from '@/test/mockApi'
import {renderApp} from '@/test/renderApp'

afterEach(() => {
  vi.useRealTimers()
})

const devops = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const databases = {id: 'c2', schoolId: 's1', code: 'DB', title: 'Databaser'}
const group1 = {id: 'g1', schoolId: 's1', type: 'Class', name: 'Lag 1', term: 'HT26'}
const group2 = {id: 'g2', schoolId: 's1', type: 'Committee', name: 'Lag 2', term: 'HT26'}
const teacher = {id: 'u1', schoolId: 's1', name: 'Anna Lärare', email: 'anna@example.com', role: 'Teacher'}

function news(
  linkId: string,
  contentId: string,
  title: string,
  publishAt: string,
  extra: Record<string, unknown> = {},
) {
  return {
    linkId,
    contentId,
    type: 'News',
    title,
    bodyMd: `${title} – brödtext`,
    authorId: 'u1',
    groupId: null,
    courseId: null,
    moduleId: null,
    publishAt,
    dueAt: null,
    isPinned: false,
    ...extra,
  }
}

interface Feeds {
  courses?: Record<string, unknown[]>
  groups?: Record<string, unknown[]>
  fail?: string
}

function backend(feeds: Feeds = {}) {
  mockApi((method, path) => {
    if (path === '/api/courses') {
      return {status: 200, body: [devops, databases]}
    }
    if (path === '/api/groups') {
      return {status: 200, body: [group1, group2]}
    }
    if (path === '/api/users') {
      return {status: 200, body: [teacher]}
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

function titles(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']): string[] {
  return wrapper.findAll('[data-test="news-item"]')
    .map((item) => item.find('.font-medium').text())
}

async function openNewsMenu(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  await wrapper.find('[data-test="news-menu"]').trigger('keydown', {key: 'Enter'})
  await flushPromises()
}

function newsAction(label: string): HTMLElement {
  const item = [...document.querySelectorAll('[role="menuitem"]')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!item) {
    throw new Error(`No menu item: ${label}`)
  }
  return item as HTMLElement
}

function dialogButton(label: string): HTMLElement {
  const button = [...document.querySelectorAll('[role="dialog"] button')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!button) {
    throw new Error(`No dialog button: ${label}`)
  }
  return button as HTMLElement
}

describe('InboxView', () => {
  it('kombinerar nyheterna från kurser och grupper', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Välkommen till kursen', '2026-10-01T10:00:00Z', {courseId: 'c1'})],
        c2: [news('l2', 'x2', 'Inlämning flyttad', '2026-10-10T10:00:00Z', {courseId: 'c2'})],
      },
      groups: {
        g1: [news('l3', 'x3', 'Studiebesök', '2026-10-19T10:00:00Z', {groupId: 'g1'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    expect(titles(wrapper)).toEqual(['Studiebesök', 'Inlämning flyttad', 'Välkommen till kursen'])
  })

  it('har flikarna Alla och Nyheter med Alla vald', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    const tabs = wrapper.findAll('[role="tab"]')
    expect(tabs.map((tab) => tab.text())).toEqual(['Alla', 'Nyheter'])
    expect(tabs[0].attributes('aria-selected')).toBe('true')
    expect(tabs[1].attributes('aria-selected')).toBe('false')
  })

  it('det går inte att skapa en nyhet från inkorgen', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    expect(wrapper.find('[data-test="new-news"]').exists()).toBe(false)
  })

  it('visar mål och rubrik i detaljen', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    const detail = wrapper.find('[aria-label="Nyhet"]')
    expect(detail.text()).toContain('DevOps · Nyhet')
    expect(detail.find('h1').text()).toBe('Studiebesök')
  })

  it('visar nyhetens text formaterad som markdown', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1', bodyMd: 'Ligger under **Kurser**'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    expect(wrapper.find('[aria-label="Nyhet"] strong').text()).toBe('Kurser')
  })

  it('visar en nyhet som är publicerad i både en kurs och en grupp bara en gång, med båda namnen', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
      groups: {
        g1: [news('l2', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {groupId: 'g1'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    const items = wrapper.findAll('[data-test="news-item"]')
    expect(items).toHaveLength(1)
    expect(items[0].text()).toContain('DevOps · Lag 1')
    expect(wrapper.find('[aria-label="Nyhet"]').text()).toContain('DevOps')
    expect(wrapper.find('[aria-label="Nyhet"]').text()).toContain('Lag 1')
  })

  it('sorterar kronologiskt och markerar fästa nyheter', async () => {
    backend({
      courses: {
        c1: [
          news('l1', 'x1', 'Välkommen till kursen', '2026-10-10T10:00:00Z', {courseId: 'c1'}),
          news('l2', 'x2', 'Studiebesök', '2026-10-05T10:00:00Z', {courseId: 'c1', isPinned: true}),
        ],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    expect(titles(wrapper)).toEqual(['Välkommen till kursen', 'Studiebesök'])
    const items = wrapper.findAll('[data-test="news-item"]')
    expect(items[0].text()).not.toContain('Fäst')
    expect(items[1].text()).toContain('Fäst')
  })

  it('delar in nyheterna under Idag, Igår och Tidigare', async () => {
    vi.useFakeTimers({toFake: ['Date']})
    vi.setSystemTime(new Date('2026-10-20T12:00:00'))

    backend({
      courses: {
        c1: [
          news('l1', 'x1', 'Idag-nyhet', '2026-10-20T09:15:00'),
          news('l2', 'x2', 'Igår-nyhet', '2026-10-19T15:30:00'),
          news('l3', 'x3', 'Äldre nyhet', '2026-10-01T10:00:00'),
        ],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    expect(wrapper.findAll('[data-test="news-day"]').map((heading) => heading.text()))
      .toEqual(['Idag', 'Igår', 'Tidigare'])
    expect(titles(wrapper)).toEqual(['Idag-nyhet', 'Igår-nyhet', 'Äldre nyhet'])

    const items = wrapper.findAll('[data-test="news-item"]')
    expect(items[0].text()).toContain('09:15')
    expect(items[1].text()).toContain('15:30')
    expect(items[2].text()).toContain('tors 1 okt')
  })

  it('byter nyhet och sparar valet i adressen', async () => {
    backend({
      courses: {
        c1: [
          news('l1', 'x1', 'Välkommen till kursen', '2026-10-01T10:00:00Z', {courseId: 'c1'}),
          news('l2', 'x2', 'Studiebesök', '2026-10-10T10:00:00Z', {courseId: 'c1'}),
        ],
      },
    })

    const {wrapper, router} = await renderApp('/inbox')

    expect(wrapper.find('[aria-label="Nyhet"] h1').text()).toBe('Studiebesök')

    await wrapper.findAll('[data-test="news-item"]')[1].trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.post).toBe('x1')
    expect(wrapper.find('[aria-label="Nyhet"] h1').text()).toBe('Välkommen till kursen')
  })

  it('raderar nyheten från alla mål när den är publicerad i flera', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
      groups: {
        g1: [news('l2', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {groupId: 'g1'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    await openNewsMenu(wrapper)
    newsAction('Ta bort').click()
    await flushPromises()
    expect(document.body.textContent)
      .toContain('Nyheten tas bort från alla kurser och grupper den är publicerad i.')

    dialogButton('Ta bort').click()
    await flushPromises()
    await flushPromises()

    expect(requests
      .filter((request) => request.method === 'DELETE')
      .map((request) => request.path)).toEqual([
      '/api/content/x1/links/l1',
      '/api/content/x1/links/l2',
    ])
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })

  it('visar ett fel när ett flöde inte kan hämtas', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Välkommen till kursen', '2026-10-01T10:00:00Z', {courseId: 'c1'})],
      },
      fail: 'g2',
    })

    const {wrapper} = await renderApp('/inbox')

    expect(wrapper.text()).toContain('Kunde inte hämta nyheterna.')
    expect(wrapper.findAll('[data-test="news-item"]')).toHaveLength(0)
  })

  it('sidomenyn länkar till Inkorg och har ingen separat nyhetssida', async () => {
    backend()

    const {wrapper, router} = await renderApp('/')

    expect(wrapper.find('a[href="/news"]').exists()).toBe(false)

    const link = wrapper.findAll('a')
      .find((candidate) => candidate.text().trim() === 'Inkorg')
    expect(link).toBeDefined()

    await link!.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('inbox')
    expect(router.currentRoute.value.path).toBe('/inbox')
  })
})
