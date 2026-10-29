import {flushPromises} from '@vue/test-utils'
import {afterEach, describe, expect, it, vi} from 'vitest'

import type {Schema} from '@/lib/api'
import {mockApi, requests, respond} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

afterEach(() => {
  vi.useRealTimers()
})

const devops: Schema<'CourseDto'> = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps', description: '', points: null, startsOn: null, endsOn: null}
const databases: Schema<'CourseDto'> = {id: 'c2', schoolId: 's1', code: 'DB', title: 'Databaser', description: '', points: null, startsOn: null, endsOn: null}
const group1: Schema<'GroupDto'> = {id: 'g1', schoolId: 's1', type: 'Class', name: 'Lag 1', term: 'HT26', memberCount: 0}
const group2: Schema<'GroupDto'> = {id: 'g2', schoolId: 's1', type: 'Committee', name: 'Lag 2', term: 'HT26', memberCount: 0}
const teacher: Schema<'UserDto'> = {id: 'u1', schoolId: 's1', name: 'Anna Lärare', email: 'anna@example.com', role: 'Teacher'}

function news(
  linkId: string,
  contentId: string,
  title: string,
  publishAt: string,
  extra: Partial<Schema<'ContentFeedItem'>> = {},
): Schema<'ContentFeedItem'> {
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

function notification(
  id: string,
  kind: Schema<'NotificationKind'>,
  extra: Partial<Schema<'NotificationDto'>> = {},
): Schema<'NotificationDto'> {
  return {
    id,
    kind,
    contentId: null,
    submissionId: null,
    linkId: null,
    title: null,
    courseId: null,
    courseTitle: null,
    groupName: null,
    level: null,
    createdAt: '2026-10-19T10:00:00Z',
    readAt: null,
    ...extra,
  }
}

interface Feeds {
  courses?: Record<string, Schema<'ContentFeedItem'>[]>
  groups?: Record<string, Schema<'ContentFeedItem'>[]>
  notifications?: Schema<'NotificationDto'>[]
  fail?: string
}

function backend(feeds: Feeds = {}) {
  mockApi((method, path) => {
    if (path === '/api/me') {
      return respond<'/api/me', 'get'>(meAs('Teacher', ['c1', 'c2']))
    }
    if (path === '/api/courses') {
      return respond<'/api/courses', 'get'>([devops, databases])
    }
    if (path === '/api/groups') {
      return respond<'/api/groups', 'get'>([group1, group2])
    }
    if (path === '/api/users') {
      return respond<'/api/users', 'get'>([teacher])
    }
    const courseFeed = path.match(/^\/api\/courses\/([^/]+)\/content$/)
    if (courseFeed) {
      if (feeds.fail === courseFeed[1]) {
        return {status: 500, body: 'Server error'}
      }
      return respond<'/api/courses/{id}/content', 'get'>(feeds.courses?.[courseFeed[1]] ?? [])
    }
    const groupFeed = path.match(/^\/api\/groups\/([^/]+)\/content$/)
    if (groupFeed) {
      if (feeds.fail === groupFeed[1]) {
        return {status: 500, body: 'Server error'}
      }
      return respond<'/api/groups/{id}/content', 'get'>(feeds.groups?.[groupFeed[1]] ?? [])
    }
    if (path === '/api/notifications') {
      const items = feeds.notifications ?? []
      return respond<'/api/notifications', 'get'>({
        items,
        unreadCount: items.filter((item) => item.readAt === null).length,
      })
    }
    if (path === '/api/notifications/unread-count') {
      return respond<'/api/notifications/unread-count', 'get'>({
        count: (feeds.notifications ?? []).filter((item) => item.readAt === null).length,
      })
    }
    if (method === 'POST' && path.startsWith('/api/notifications/')) {
      return {status: 204}
    }
    if (method === 'DELETE') {
      return {status: 204}
    }
    return {status: 404}
  })
}

function rowTitles(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']): string[] {
  return wrapper.findAll('[data-test="news-item"], [data-test="notification-item"]')
    .map((item) => item.find('.font-medium, .font-semibold').text())
}

// Märket vid Inkorg i sidomenyn. Att bedöma har ett eget märke, så det söks inom raden.
function inboxBadge(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  const item = wrapper.findAll('[data-slot="sidebar-menu-item"]')
    .find((candidate) => candidate.text().startsWith('Inkorg'))
  const badge = item?.find('[data-slot="sidebar-menu-badge"]')
  return badge?.exists()
    ? badge
    : undefined
}

function tabLabels(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']): string[] {
  return wrapper.findAll('[role="tab"]').map((tab) => tab.text())
}

async function openTab(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], label: string) {
  const tab = wrapper.findAll('[role="tab"]').find((candidate) => candidate.text().startsWith(label))
  await tab!.trigger('click')
  await flushPromises()
}

const gradeNotification = notification('n-grade', 'SubmissionGraded', {
  title: 'Laboration: CI/CD',
  level: 'Pass',
  courseId: 'c1',
  courseTitle: 'DevOps',
  submissionId: 's1',
  createdAt: '2026-10-21T11:20:00Z',
})
const needsWorkNotification = notification('n-work', 'SubmissionNeedsWork', {
  title: 'Testplan',
  courseId: 'c1',
  courseTitle: 'DevOps',
  createdAt: '2026-10-20T09:00:00Z',
})
const deadlineNotification = notification('n-deadline', 'DeadlineSoon', {
  title: 'E2E-tester',
  courseId: 'c1',
  courseTitle: 'DevOps',
  createdAt: '2026-10-18T09:00:00Z',
  readAt: '2026-10-18T10:00:00Z',
})

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

  it('har flikarna Alla, Olästa, Betyg och Nyheter med Alla vald', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    const tabs = wrapper.findAll('[role="tab"]')
    expect(tabs.map((tab) => tab.text())).toEqual(['Alla', 'Olästa', 'Betyg', 'Nyheter'])
    expect(tabs[0].attributes('aria-selected')).toBe('true')
    expect(tabs[1].attributes('aria-selected')).toBe('false')
  })

  it('blandar nyheter och notiser, nyast först, och visar rubriken på betyget', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
      notifications: [gradeNotification, needsWorkNotification, deadlineNotification],
    })

    const {wrapper} = await renderApp('/inbox')

    expect(rowTitles(wrapper)).toEqual([
      'Laboration: CI/CD har fått betyget G',
      'Testplan behöver kompletteras',
      'Studiebesök',
      'E2E-tester ska lämnas in inom ett dygn',
    ])
    expect(wrapper.findAll('[data-test="notification-item"]')[0].text()).toContain('DevOps · Betyg')
  })

  it('markerar olästa rader och räknar dem på fliken Olästa', async () => {
    backend({
      courses: {
        c1: [
          news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'}),
          news('l2', 'x2', 'Äldre nyhet', '2026-10-01T10:00:00Z', {courseId: 'c1'}),
        ],
      },
      notifications: [
        gradeNotification,
        deadlineNotification,
        notification('n-news', 'NewsPublished', {contentId: 'x1', createdAt: '2026-10-19T10:00:00Z'}),
      ],
    })

    const {wrapper} = await renderApp('/inbox')

    expect(tabLabels(wrapper)).toEqual(['Alla', 'Olästa 2', 'Betyg', 'Nyheter'])
    const unread = wrapper.findAll('[aria-label="Oläst"]')
    expect(unread).toHaveLength(2)

    await openTab(wrapper, 'Olästa')

    expect(rowTitles(wrapper)).toEqual(['Laboration: CI/CD har fått betyget G', 'Studiebesök'])
  })

  it('filtrerar på Betyg och Nyheter', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
      notifications: [gradeNotification, needsWorkNotification, deadlineNotification],
    })

    const {wrapper} = await renderApp('/inbox')

    await openTab(wrapper, 'Betyg')
    expect(rowTitles(wrapper)).toEqual([
      'Laboration: CI/CD har fått betyget G',
      'Testplan behöver kompletteras',
    ])

    await openTab(wrapper, 'Nyheter')
    expect(rowTitles(wrapper)).toEqual(['Studiebesök'])
  })

  it('visar en tom flik med en egen text när inget passar filtret', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
    })

    const {wrapper} = await renderApp('/inbox')

    await openTab(wrapper, 'Olästa')
    expect(wrapper.find('[data-test="tab-empty"]').text()).toBe('Du har läst allt.')

    await openTab(wrapper, 'Betyg')
    expect(wrapper.find('[data-test="tab-empty"]').text()).toBe('Inga betyg än.')

    await openTab(wrapper, 'Nyheter')
    expect(wrapper.find('[data-test="tab-empty"]').exists()).toBe(false)
  })

  it('visar att inkorgen är tom när det varken finns nyheter eller notiser', async () => {
    backend()

    const {wrapper} = await renderApp('/inbox')

    expect(wrapper.text()).toContain('Inkorgen är tom')
    expect(wrapper.findAll('[role="tab"]')).toHaveLength(0)
  })

  it('markerar nyhetens notis som läst när nyheten öppnas', async () => {
    backend({
      courses: {
        c1: [
          news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'}),
          news('l2', 'x2', 'Äldre nyhet', '2026-10-01T10:00:00Z', {courseId: 'c1'}),
        ],
      },
      notifications: [notification('n-news', 'NewsPublished', {contentId: 'x1'})],
    })

    const {wrapper} = await renderApp('/inbox')
    expect(wrapper.findAll('[aria-label="Oläst"]')).toHaveLength(1)
    expect(requests.filter((request) => request.method === 'POST')).toEqual([])

    await wrapper.findAll('[data-test="news-item"]')[0].trigger('click')
    await flushPromises()

    expect(requests.filter((request) => request.method === 'POST').map((request) => request.path))
      .toEqual(['/api/notifications/n-news/read'])
    expect(wrapper.findAll('[aria-label="Oläst"]')).toHaveLength(0)
    expect(tabLabels(wrapper)).toContain('Olästa')
  })

  it('markerar en nyhet som läst när den öppnas via en länk med ?post=', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
      notifications: [notification('n-news', 'NewsPublished', {contentId: 'x1'})],
    })

    await renderApp('/inbox?post=x1')

    expect(requests.filter((request) => request.method === 'POST').map((request) => request.path))
      .toEqual(['/api/notifications/n-news/read'])
  })

  it('markerar betyget som läst och går till studieplanen när raden öppnas', async () => {
    backend({notifications: [gradeNotification]})

    const {wrapper, router} = await renderApp('/inbox')

    await wrapper.find('[data-test="notification-item"]').trigger('click')
    await flushPromises()

    expect(requests.filter((request) => request.method === 'POST').map((request) => request.path))
      .toEqual(['/api/notifications/n-grade/read'])
    expect(router.currentRoute.value.name).toBe('grades')
  })

  it('går till kursen när en ny uppgift öppnas', async () => {
    backend({
      notifications: [notification('n-new', 'AssignmentPublished', {title: 'Ny labb', courseId: 'c1', courseTitle: 'DevOps'})],
    })

    const {wrapper, router} = await renderApp('/inbox')

    expect(rowTitles(wrapper)).toEqual(['Ny uppgift: Ny labb'])
    await wrapper.find('[data-test="notification-item"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/courses/c1/content')
  })

  it('Markera alla som lästa skickar ett anrop och nollställer olästa', async () => {
    backend({
      courses: {
        c1: [news('l1', 'x1', 'Studiebesök', '2026-10-19T10:00:00Z', {courseId: 'c1'})],
      },
      notifications: [
        gradeNotification,
        needsWorkNotification,
        notification('n-news', 'NewsPublished', {contentId: 'x1'}),
      ],
    })

    const {wrapper} = await renderApp('/inbox')
    expect(tabLabels(wrapper)).toContain('Olästa 3')
    const button = wrapper.find('[data-test="mark-all-read"]')
    expect(button.text()).toBe('Markera alla som lästa')

    await button.trigger('click')
    await flushPromises()

    expect(requests.filter((request) => request.method === 'POST').map((request) => request.path))
      .toEqual(['/api/notifications/read-all'])
    expect(wrapper.findAll('[aria-label="Oläst"]')).toHaveLength(0)
    expect(tabLabels(wrapper)).toContain('Olästa')
    expect(wrapper.find('[data-test="mark-all-read"]').attributes('disabled')).toBeDefined()
  })

  it('sidomenyns Inkorg visar antalet olästa och inget märke när alla är lästa', async () => {
    backend({notifications: [gradeNotification, needsWorkNotification, deadlineNotification]})

    const first = await renderApp('/')
    expect(inboxBadge(first.wrapper)?.text()).toBe('2')

    backend({notifications: [deadlineNotification]})
    const second = await renderApp('/')

    expect(inboxBadge(second.wrapper)).toBeUndefined()
  })

  it('sidomenyns märke sjunker när notiser markeras som lästa', async () => {
    backend({notifications: [gradeNotification, needsWorkNotification]})

    const {wrapper} = await renderApp('/inbox')
    expect(inboxBadge(wrapper)?.text()).toBe('2')

    await wrapper.find('[data-test="mark-all-read"]').trigger('click')
    await flushPromises()

    expect(inboxBadge(wrapper)).toBeUndefined()
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
